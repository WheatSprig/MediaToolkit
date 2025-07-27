using MediaToolkit.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MediaToolkit.Adapters.FFmpeg
{
    public class FFmpegAdapter : IMediaToolAdapter
    {
        public string ToolName => "ffmpeg";
        public string ExecutablePath { get; }

        /// <summary>
        /// 当转码进度更新时触发。
        /// </summary>
        public event EventHandler<string> LogReceived;
        public event EventHandler<ProgressEventArgs> ProgressChanged;

        private readonly ProcessRunner _runner;
        private TimeSpan _totalDuration; // 用于存储当前任务的总时长

        // 定义用于解析FFmpeg输出进度的正则表达式
        // 匹配 "Duration: 00:00:25.88, start:..."
        private static readonly Regex DurationRegex = new Regex(@"Duration: ([^,]+),", RegexOptions.Compiled);
        // 匹配 "frame=... time=00:00:12.34..."
        private static readonly Regex ProgressRegex = new Regex(@"time=([^ ]+)", RegexOptions.Compiled);

        // 用于解析元数据的正则表达式
        // 匹配: Stream #0:0(...): Video: h264 (...), yuv420p, 1920x1080, 29.97 fps
        private static readonly Regex VideoStreamRegex = new Regex(@"Stream #\d:\d.*: Video: ([\w\d]+).*?(\d{3,5}x\d{3,5}).*?([\d\.]+)\s+fps", RegexOptions.Compiled);
        // 匹配: Stream #0:1(...): Audio: aac (LC), 48000 Hz, stereo
        private static readonly Regex AudioStreamRegex = new Regex(@"Stream #\d:\d.*: Audio: ([\w\d]+).*?(\d+\s*Hz).*?(\w+)", RegexOptions.Compiled);

        public FFmpegAdapter(string ffmpegPath = null)
        {
            ExecutablePath = ToolFinder.FindExecutablePath(ToolName, ffmpegPath);
            _runner = new ProcessRunner(ExecutablePath);

            // 订阅底层Runner的原始日志事件
            _runner.LogReceived += OnRunnerLogReceived;
        }

        /// <summary>
        /// 实现事件处理器，在这里进行解析
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="log"></param>
        private void OnRunnerLogReceived(object sender, string log)
        {
            // 将原始日志也冒泡出去
            LogReceived?.Invoke(this, log);

            if (string.IsNullOrEmpty(log)) return;

            // 尝试解析总时长 (只在第一次找到时有效)
            if (_totalDuration == TimeSpan.Zero)
            {
                var match = DurationRegex.Match(log);
                if (match.Success && TimeSpan.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var duration))
                {
                    _totalDuration = duration;
                }
            }

            // 尝试解析当前进度
            var progressMatch = ProgressRegex.Match(log);
            if (progressMatch.Success && TimeSpan.TryParse(progressMatch.Groups[1].Value, CultureInfo.InvariantCulture, out var processed))
            {
                // 如果总时长已知，则可以计算并触发进度事件
                if (_totalDuration > TimeSpan.Zero)
                {
                    var args = new ProgressEventArgs(processed, _totalDuration);
                    ProgressChanged?.Invoke(this, args);
                }
            }
        }

        // 将 ExecuteAsync 方法拆分为一个更通用的私有方法
        private async Task<ToolResult> ExecuteCommandAsync(string arguments, bool throwOnError, CancellationToken ct = default, string workingDirectory = null)
        {
            // 在每次执行前重置时长状态
            _totalDuration = TimeSpan.Zero;

            var result = await _runner.ExecuteAsync(arguments, ct, workingDirectory);
            if (throwOnError && result.ExitCode != 0)
            {
                throw new InvalidOperationException($"FFmpeg failed with exit code {result.ExitCode}: {result.Error}");
            }

            return result;
        }

        // 公开的 ExecuteAsync 现在只是一个简单的代理
        public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default, string workingDirectory = null)
        {
            return ExecuteCommandAsync(arguments, true, ct, workingDirectory);
        }

        public async Task ConvertAsync(string inputFile, string outputFile, string options = "", int threads = 0, CancellationToken ct = default)
        {
            // 如果指定了线程数且大于0，则添加到 options 中
            string threadOption = threads > 0 ? $"-threads {threads}" : "";
            var arguments = $"-y -hide_banner -i \"{inputFile}\" {options}  {threadOption} \"{outputFile}\"";

            // 转码任务必须成功，所以 throwOnError: true
            await ExecuteCommandAsync(arguments, true, ct);
        }

        private void ParseProgress(string log)
        {
            if (_totalDuration == TimeSpan.Zero)
            {
                var match = DurationRegex.Match(log);
                if (match.Success && TimeSpan.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var duration))
                {
                    _totalDuration = duration;
                }
            }

            var progressMatch = ProgressRegex.Match(log);
            if (progressMatch.Success && TimeSpan.TryParse(progressMatch.Groups[1].Value, CultureInfo.InvariantCulture, out var processed))
            {
                ProgressChanged?.Invoke(this, new ProgressEventArgs(processed, _totalDuration));
            }
        }

        /// <summary>
        /// 将视频转换为适用于网络流媒体的DASH格式 (单码率1080p)。
        /// </summary>
        /// <param name="inputFile">输入文件路径。</param>
        /// <param name="outputManifestPath">输出的MPD清单文件路径 (例如 "C:\dash\stream.mpd")。</param>
        /// <param name="ct">取消令牌。</param>
        // --- 重写 ConvertToDash1080pAsync 方法 ---
        public async Task ConvertToDash1080pAsync(string inputFile, string outputManifestPath, int threads = 0, CancellationToken ct = default)
        {
            string outputDir = Path.GetDirectoryName(outputManifestPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // 文件名不带路径，避免 MPD 写入绝对路径
            string initSegName = "init-stream$RepresentationID$.m4s";
            string mediaSegName = "chunk-stream$RepresentationID$-$Number%05d$.m4s";

            string threadOption = threads > 0 ? $"-threads {threads}" : "";

            // 在这里构建一个完整的、独立的命令行参数字符串
            string[] arguments = new string[]
            {
                "-y",                               // 覆盖输出文件
                "-hide_banner",
                $"-i \"{inputFile}\"",              // 输入文件
                "-c:v libx264",                     // 视频编码
                "-preset veryfast",                 // 速度预设
                "-profile:v high",
                threadOption,                       // 将线程参数加入到这里
                "-crf 22",                          // 质量因子
                "-b:v 5M",                          // 目标比特率
                "-maxrate 6M",                      // 最大比特率
                "-bufsize 10M",                     // 缓冲区
                "-vf \"scale=-2:1080,fps=30\"",     // 视频滤镜 (关键的引号)
                "-c:a aac",                         // 音频编码
                "-b:a 192k",                        // 音频比特率
                "-f dash",                          // **核心：指定输出格式为DASH**
                "-seg_duration 4",                  // 分片时长
                "-use_template 1",
                "-use_timeline 1",
                $"-init_seg_name {initSegName}",
                $"-media_seg_name {mediaSegName}",
                $"\"{Path.GetFileName(outputManifestPath)}\"",  // **核心：指定输出的清单文件**
                "-loglevel verbose"
            };

            string fullCommand = string.Join(" ", arguments);

            Console.WriteLine("FFmpeg 命令参数:");
            Console.WriteLine(fullCommand);

            // 直接调用底层的 ExecuteAsync，因为它需要成功，所以我们使用会抛出异常的版本
            await this.ExecuteAsync(fullCommand, ct, workingDirectory: outputDir);
        }

        public async Task ConvertToMultiDashAsync(string inputFile, string outputManifestPath, int threads = 0, CancellationToken ct = default)
        {
            string outputDir = Path.GetDirectoryName(outputManifestPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            string initSegName = "init-stream$RepresentationID$.m4s";
            string mediaSegName = "chunk-stream$RepresentationID$-$Number%05d$.m4s";

            var selector = new HardwareEncoderSelector(this);
            await selector.DetectBestEncoderAsync();

            string videoEncoder = selector.SelectedVideoEncoder;

            string filter = string.Join(";", new[]
            {
                "[0:v]split=6[v1][v2][v3][v4][v5][v6]",
                "[v1]scale=-2:1080,fps=30[vout1]",
                "[v2]scale=-2:1080,fps=60[vout2]",
                "[v3]scale=-2:720,fps=60[vout3]",
                "[v4]scale=-2:720,fps=30[vout4]",
                "[v5]scale=-2:480[vout5]",
                "[v6]scale=-2:360[vout6]"
            });

            var argsBuilder = new List<string>
            {
                "-y",
                "-hide_banner",
                $"-i \"{inputFile}\"",
                $"-filter_complex \"{filter}\"", // 复杂滤镜可能仍然在CPU上运行

                // 添加线程参数
                // 注意：-threads 通常放在输入文件之后，编码器参数之前，或者作为全局选项
                // 对于多路输出，可以尝试放在 -i 之后，或者针对每个编码器单独设置（如果编码器支持）
                // 这里我们作为全局选项添加
            };

            if (threads > 0)
            {
                argsBuilder.Add($"-threads {threads}");
            }

            argsBuilder.AddRange(new[]
            {
                "-map [vout1]", $"-c:v:0 {videoEncoder}", "-b:v:0 5M", "-maxrate:v:0 6M", "-bufsize:v:0 10M",
                "-map [vout2]", $"-c:v:1 {videoEncoder}", "-b:v:1 6M", "-maxrate:v:1 7M", "-bufsize:v:1 10M",
                "-map [vout3]", $"-c:v:2 {videoEncoder}", "-b:v:2 4M", "-maxrate:v:2 5M", "-bufsize:v:2 8M",
                "-map [vout4]", $"-c:v:3 {videoEncoder}", "-b:v:3 3M", "-maxrate:v:3 4M", "-bufsize:v:3 6M",
                "-map [vout5]", $"-c:v:4 {videoEncoder}", "-b:v:4 1M", "-maxrate:v:4 1.5M", "-bufsize:v:4 2M",
                "-map [vout6]", $"-c:v:5 {videoEncoder}", "-b:v:5 0.6M", "-maxrate:v:5 1M", "-bufsize:v:5 1.5M",

                "-map a:0", "-c:a aac", "-b:a 192k",

                "-f dash",
                "-seg_duration 4",
                "-use_template 1",
                "-use_timeline 1",
                $"-init_seg_name {initSegName}",
                $"-media_seg_name {mediaSegName}",
                $"\"{Path.GetFileName(outputManifestPath)}\"",
                "-loglevel info"
            });

            string fullCommand = string.Join(" ", argsBuilder);

            Console.WriteLine("FFmpeg 多规格 DASH 命令:");
            Console.WriteLine(fullCommand);

            await this.ExecuteAsync(fullCommand, ct, workingDirectory: outputDir);
        }


        /// <summary>
        /// 异步获取媒体文件的元数据。
        /// </summary>
        public async Task<Metadata> GetMetadataAsync(string inputFile, CancellationToken ct = default)
        {
            //quiet  → 完全不输出
            //panic  → 只有 panic 级别的致命错误
            //fatal  → 只有 fatal 错误
            //error  → 只有错误信息
            //warning → 错误 + 警告
            //info    → 错误 + 警告 + 普通信息（默认就是这个）
            //verbose → 在 info 基础上再详细一点
            //debug   → 调试信息（非常详细）
            //trace   → 比 debug 还详细，几乎逐帧逐字节

            // -f null -
            // -f null 表示不输出任何文件，只是获取元数据
            // - 表示丢弃输出（即不保存到文件）
            string arguments = $"-v info -i \"{inputFile}\" -hide_banner";

            // 调用新方法，并设置 throwOnError: false
            // 这样即使ffmpeg因没有输出文件而返回非零代码，我们也不会抛出异常
            ToolResult result = await ExecuteCommandAsync(arguments, false, ct);

            // 我们从 ToolResult.Error (即 stderr) 中解析元数据
            return ParseMetadata(result.Error);
        }

        private Metadata ParseMetadata(string ffmpegOutput)
        {
            var metadata = new Metadata();

            // 解析总时长
            var durationMatch = DurationRegex.Match(ffmpegOutput);
            if (durationMatch.Success && TimeSpan.TryParse(durationMatch.Groups[1].Value, CultureInfo.InvariantCulture, out var duration))
            {
                metadata.Duration = duration;
            }

            // 解析视频流
            var videoMatch = VideoStreamRegex.Match(ffmpegOutput);
            if (videoMatch.Success)
            {
                metadata.VideoStream = new VideoStreamInfo
                {
                    Codec = videoMatch.Groups[1].Value,
                    Resolution = videoMatch.Groups[2].Value,
                    Fps = double.TryParse(videoMatch.Groups[3].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var fps) ? fps : 0
                };
            }

            // 解析音频流
            var audioMatch = AudioStreamRegex.Match(ffmpegOutput);
            if (audioMatch.Success)
            {
                metadata.AudioStream = new AudioStreamInfo
                {
                    Codec = audioMatch.Groups[1].Value,
                    SampleRate = audioMatch.Groups[2].Value,
                    Channels = audioMatch.Groups[3].Value
                };
            }

            return metadata;
        }

        // 在 Dispose 或 Finalizer 中取消订阅事件是个好习惯，此处为简化省略
    }
}