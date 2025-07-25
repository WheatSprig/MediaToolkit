using MediaToolkit.Core;
using System;
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

        public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
        {
            // 在执行前重置时长，以便为下一次运行做准备，以防上一次运行的干扰
            _totalDuration = TimeSpan.Zero;
            return _runner.ExecuteAsync(arguments, cancellationToken);
        }

        public async Task ConvertAsync(string inputFile, string outputFile, string options = "", CancellationToken ct = default)
        {
            var arguments = $"-y -hide_banner -i \"{inputFile}\" {options} \"{outputFile}\"";
            var result = await ExecuteAsync(arguments, ct);

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"FFmpeg failed with exit code {result.ExitCode}: {result.Error}");
            }
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
        public async Task ConvertToDash1080pAsync(string inputFile, string outputManifestPath, CancellationToken ct = default)
        {
            string outputDir = Path.GetDirectoryName(outputManifestPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // 这个参数集是生成DASH的关键，我们来逐一解释：
            string[] arguments = new string[]
            {
                //"-y",                             // 覆盖输出文件
                //"-hide_banner",
                //$"-i \"{inputFile}\"",            // 输入文件
                "-c:v libx264",                   // 使用x264视频编码器
                "-preset veryfast",               // 编码速度预设，平衡速度和质量
                "-profile:v high",                // H.264 profile
                "-crf 22",                        // 恒定质量因子，22是很好的平衡点
                "-b:v 5M",                        // 目标视频比特率: 5 Mbps
                "-maxrate 6M",                    // 最大视频比特率: 6 Mbps
                "-bufsize 10M",                   // 视频缓冲区大小
                "-vf \"scale=-2:1080,fps=30\"",   // 视频滤镜: 缩放到1080p高度, 帧率30fps. -2确保宽度是偶数
                "-c:a aac",                       // 使用AAC音频编码器
                "-b:a 192k",                      // 音频比特率: 192 kbps
                "-f dash",                        // 输出格式为DASH
                "-seg_duration 4",                // 每个分片时长4秒
                "-use_template 1",                // 使用模板命名分片
                "-use_timeline 1",                // 使用timeline
                //$"\"{outputManifestPath}\""       // 输出的MPD清单文件
            };

            string command = string.Join(" ", arguments);
            await ConvertAsync(inputFile, outputManifestPath, command.Substring(command.IndexOf("-c:v"))); // 重新利用ConvertAsync的逻辑，但传递完整的命令
        }

        /// <summary>
        /// 异步获取媒体文件的元数据。
        /// </summary>
        public async Task<Metadata> GetMetadataAsync(string inputFile, CancellationToken ct = default)
        {
            var arguments = $"-i \"{inputFile}\" -hide_banner";

            try
            {
                // 当只提供-i参数时，ffmpeg会打印元数据到stderr然后因缺少输出文件而失败退出。
                // 我们期望它抛出异常，然后从异常信息中解析元数据。
                await this.ExecuteAsync(arguments, ct);

                // 理论上不会执行到这里，因为上面的调用会因ffmpeg非零退出码而抛出异常
                return null;
            }
            catch (InvalidOperationException ex)
            {
                // 这正是我们期望的，异常信息中包含了元数据
                return ParseMetadata(ex.Message);
            }
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