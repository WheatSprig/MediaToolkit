using MediaToolkit.Adapters.FFmpeg.Models;
using MediaToolkit.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MediaToolkit.Adapters.FFmpeg
{
    public class FFmpegAdapter : IMediaToolAdapter
    {
        #region 常量和字段
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
        // 稍作修改以捕获比特率信息 (例如: 1234 kb/s) 和码率模式
        // 注意：FFmpeg 对 CBR/VBR 的直接标记不一致，通常需要推断或根据上下文判断。
        // 这里先尝试捕获码率。码率模式的解析可能更复杂，需要进一步的规则。
        // 例如: Stream #0:0: Video: h264 (High) (avc1 / 0x31637661), yuv420p(tv, bt709/bt709/iec61966-2-1), 1920x1080 [SAR 1:1 DAR 16:9], 3804 kb/s, 29.97 fps, 29.97 tbr, 30k tbn, 59.94 tbc (default)
        private static readonly Regex VideoStreamRegex = new Regex(
            @"Stream #\d:\d.*?: Video: ([\w\d]+).*?(\d{3,5}x\d{3,5}).*?(?:,\s*(\d+)\s*kb\/s)?.*?([\d\.]+)\s+fps",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        
        // 匹配: Stream #0:1(...): Audio: aac (LC), 48000 Hz, stereo, 128 kb/s
        private static readonly Regex AudioStreamRegex = new Regex(
            @"Stream #\d:\d.*?: Audio: ([\w\d]+).*?(\d+\s*Hz).*?(\w+)(?:,\s*(\d+)\s*kb\/s)?",
            RegexOptions.Compiled | RegexOptions.IgnoreCase
        );
        
        // 提取整体比特率，FFmpeg 通常会在 Duration 行报告
        private static readonly Regex OverallBitrateRegex = new Regex(@"bitrate: (\d+\.?\d*)\s*([kM]?b\/s)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        #endregion

        #region 构造函数
        public FFmpegAdapter(string ffmpegPath = null)
        {
            ExecutablePath = ToolFinder.FindExecutablePath(ToolName, ffmpegPath);
            _runner = new ProcessRunner(ExecutablePath);

            // 订阅底层Runner的原始日志事件
            _runner.LogReceived += OnRunnerLogReceived;
        }

        #endregion

        #region 事件处理
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
        #endregion

        #region 执行命令
        // 公开的 ExecuteAsync 现在只是一个简单的代理
        public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken ct = default, string workingDirectory = null)
        {
            return ExecuteCommandAsync(arguments, true, ct, workingDirectory);
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

        /// 异步执行 FFmpeg 命令，返回 ToolResult
        public async Task ConvertAsync(string inputFile, string outputFile, string options = "", int threads = 0, CancellationToken ct = default)
        {
            // 如果指定了线程数且大于0，则添加到 options 中
            string threadOption = threads > 0 ? $"-threads {threads}" : "";
            var arguments = $"-y -hide_banner -i \"{inputFile}\" {options}  {threadOption} \"{outputFile}\"";

            // 转码任务必须成功，所以 throwOnError: true
            await ExecuteCommandAsync(arguments, true, ct);
        }
        #endregion

        #region 生成 DASH 和 FMP4 流
        /// <summary>
        /// 异步生成 DASH（Dynamic Adaptive Streaming over HTTP）自适应码流。
        /// 根据给定的 <paramref name="options"/> 中的视频/音频配置，自动检测硬件编码器并一次性生成
        /// 多分辨率、多码率的 DASH 内容（MPD 清单 + 切片）。
        /// </summary>
        /// <param name="options">
        /// 输出配置，包括输入文件、输出目录、视频/音频编码参数、切片时长等。
        /// 必须至少指定一条视频配置，音频可选。
        /// </param>
        /// <param name="threads">
        /// 指定 FFmpeg 编码线程数。  
        /// 小于等于 0 时交由 FFmpeg 自动选择；大于 0 时使用指定值。
        /// </param>
        /// <param name="ct">
        /// 用于取消异步操作的令牌。
        /// </param>
        /// <returns>
        /// 一个 <see cref="Task"/>，表示整个 DASH 打包过程的完成。
        /// 完成后可在 <see cref="DashOutputOptions.OutputDirectory"/> 中找到 MPD 清单和所有切片文件。
        /// </returns>
        /// <exception cref="ArgumentException">
        /// 当 <paramref name="options"/> 或其 <see cref="DashOutputOptions.VideoProfiles"/> 为 <c>null</c> 或空时抛出。
        /// </exception>
        /// <remarks>
        /// 工作流程：
        /// <list type="number">
        ///   <item>自动检测最适合的硬件编码器（NVIDIA/AMD/Intel/QSV）。</item>
        ///   <item>根据提供的 <see cref="DashOutputOptions.VideoProfiles"/> 构建 <c>filter_complex</c>，
        ///       为每条 profile 生成对应的分辨率、帧率、码率输出。</item>
        ///   <item>如果提供了 <see cref="DashOutputOptions.AudioProfile"/>，则同时转码并映射音频。</item>
        ///   <item>调用 FFmpeg 生成符合 DASH-IF 规范的 MPD 清单、初始化切片（init）及媒体切片（chunk）。</item>
        ///   <item>所有输出文件均写入 <see cref="DashOutputOptions.OutputDirectory"/>。</item>
        /// </list>
        /// 调用者可通过 <paramref name="ct"/> 随时取消任务；取消后所有已生成的文件会被保留。
        /// </remarks>
        /// <example>
        /// <code>
        /// var options = new DashOutputOptions
        /// {
        ///     InputFile         = "source.mp4",
        ///     OutputDirectory   = @"C:\dash_output",
        ///     ManifestFileName  = "manifest.mpd",
        ///     SegmentDuration   = 4,
        ///     VideoProfiles     = new List<VideoStreamProfile>
        ///     {
        ///         new() { Resolution = "1920x1080", Bitrate = "5000k", Fps = 30 },
        ///         new() { Resolution = "1280x720",  Bitrate = "2500k", Fps = 30 },
        ///         new() { Resolution = "854x480",   Bitrate = "1000k", Fps = 30 }
        ///     },
        ///     AudioProfile = new AudioProfile { Codec = "aac", Bitrate = "128k" }
        /// };
        ///
        /// await adapter.GenerateDashAsync(options, threads: 0);
        /// </code>
        /// </example>
        public async Task GenerateDashAsync(DashOutputOptions options, int threads = 0, CancellationToken ct = default)
        {
            if (options?.VideoProfiles == null || !options.VideoProfiles.Any())
            {
                throw new ArgumentException("At least one video profile must be specified.", nameof(options));
            }

            if (!Directory.Exists(options.OutputDirectory))
            {
                Directory.CreateDirectory(options.OutputDirectory);
            }

            var selector = new HardwareEncoderSelector(this);
            await selector.DetectBestEncoderAsync();
            string videoEncoder = selector.SelectedVideoEncoder; // 使用自动检测的硬件编码器

            var argsBuilder = new List<string>
            {
                "-y",
                "-hide_banner",
                $"-i \"{options.InputFile}\""
            };

            // 构建 filter_complex
            var filterParts = new List<string>();
            var videoOutputs = new List<string>();

            // [0:v]split=N[v1][v2]...[vN]
            string splitOutputs = string.Join("", Enumerable.Range(1, options.VideoProfiles.Count).Select(i => $"[v{i}]"));
            filterParts.Add($"[0:v]split={options.VideoProfiles.Count}{splitOutputs}");

            for (int i = 0; i < options.VideoProfiles.Count; i++)
            {
                var profile = options.VideoProfiles[i];
                // 解析分辨率，支持 "1920x1080" 和 "1080" 两种格式
                string scale = profile.Resolution.Contains("x")
                    ? $"scale={profile.Resolution}"
                    : $"scale=-2:{profile.Resolution}";

                string fpsFilter = profile.Fps.HasValue ? $",fps={profile.Fps.Value}" : "";

                string outputTag = $"[vout{i + 1}]";
                filterParts.Add($"[v{i + 1}]{scale}{fpsFilter}{outputTag}");
                videoOutputs.Add(outputTag);
            }

            argsBuilder.Add($"-filter_complex \"{string.Join(";", filterParts)}\"");

            if (threads > 0)
            {
                argsBuilder.Add($"-threads {threads}");
            }

            // 映射视频流
            for (int i = 0; i < options.VideoProfiles.Count; i++)
            {
                var profile = options.VideoProfiles[i];
                argsBuilder.Add($"-map \"{videoOutputs[i]}\"");
                argsBuilder.Add($"-c:v:{i} {videoEncoder}");
                argsBuilder.Add($"-b:v:{i} {profile.Bitrate}");
                if (!string.IsNullOrEmpty(profile.MaxRate)) argsBuilder.Add($"-maxrate:v:{i} {profile.MaxRate}");
                if (!string.IsNullOrEmpty(profile.BufferSize)) argsBuilder.Add($"-bufsize:v:{i} {profile.BufferSize}");
            }

            // 映射音频流
            if (options.AudioProfile != null)
            {
                var profile = options.AudioProfile;
                argsBuilder.Add("-map a:0");
                argsBuilder.Add($"-c:a {profile.Codec}");
                argsBuilder.Add($"-b:a {profile.Bitrate}");
            }

            // 添加 DASH 参数
            string initSegName = "init-stream$RepresentationID$.m4s";
            string mediaSegName = "chunk-stream$RepresentationID$-$Number%05d$.m4s";

            argsBuilder.AddRange(new[]
            {
                "-f dash",
                $"-seg_duration {options.SegmentDuration}",
                "-use_template 1",
                "-use_timeline 1",
                $"-init_seg_name {initSegName}",
                $"-media_seg_name {mediaSegName}",
                $"-adaptation_sets \"id=0,streams=v id=1,streams=a\"", // 假设视频和音频在不同的 adaptation set
                $"-loglevel info",
                $"\"{Path.GetFileName(options.ManifestFileName)}\""
            });

            string fullCommand = string.Join(" ", argsBuilder);

            Console.WriteLine("动态生成的 FFmpeg DASH 命令:");
            Console.WriteLine(fullCommand);

            // 使用适配器自身的执行器，自动获得进度、日志等所有好处
            await this.ExecuteAsync(fullCommand, ct, workingDirectory: options.OutputDirectory);
        }

        /// <summary>
        /// 将视频转换为适用于网络流媒体的DASH格式 (单码率1080p)。
        /// </summary>
        /// <param name="inputFile">输入文件路径。</param>
        /// <param name="outputManifestPath">输出的MPD清单文件路径 (例如 "C:\dash\stream.mpd")。</param>
        /// <param name="ct">取消令牌。</param>
        public Task ConvertToDash1080pAsync(string inputFile, string outputManifestPath, int threads = 0, CancellationToken ct = default)
        {
            var options = new DashOutputOptions
            {
                InputFile = inputFile,
                OutputDirectory = Path.GetDirectoryName(outputManifestPath),
                ManifestFileName = Path.GetFileName(outputManifestPath),
                VideoProfiles = new List<VideoStreamProfile>
                {
                    new VideoStreamProfile
                    {
                        Resolution = "1080",
                        Bitrate = "5M",
                        MaxRate = "6M",
                        BufferSize = "10M",
                        Fps = 30
                    }
                },
                AudioProfile = new AudioStreamProfile
                {
                    Bitrate = "192k"
                }
            };

            return GenerateDashAsync(options, threads, ct);
        }

        #endregion

        #region 获取媒体元数据
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

            // 解析整体比特率
            var overallBitrateMatch = OverallBitrateRegex.Match(ffmpegOutput);
            if (overallBitrateMatch.Success && double.TryParse(overallBitrateMatch.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var overallBitrate))
            {
                metadata.OverallBitrateKbps = overallBitrate;
            }

            // 解析视频流
            var videoMatch = VideoStreamRegex.Match(ffmpegOutput);
            if (videoMatch.Success)
            {
                var videoStream = new VideoStreamInfo
                {
                    Codec = videoMatch.Groups[1].Value,
                    Resolution = videoMatch.Groups[2].Value,
                    Fps = double.TryParse(videoMatch.Groups[4].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var fps) ? fps : 0
                };

                // 尝试解析视频码率
                if (videoMatch.Groups[3].Success && double.TryParse(videoMatch.Groups[3].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var videoBitrate))
                {
                    videoStream.BitrateKbps = videoBitrate;
                    // 对于 BitrateMode，需要更高级的逻辑来推断，因为 FFmpeg 不直接输出 "CBR" 或 "VBR"。
                    // 暂时可以不设置或设置为 null。
                    // videoStream.BitrateMode = "VBR"; // 示例：如果编码器默认是 VBR
                }

                metadata.VideoStream = videoStream;
            }

            // 解析音频流
            var audioMatch = AudioStreamRegex.Match(ffmpegOutput);
            if (audioMatch.Success)
            {
                var audioStream = new AudioStreamInfo
                {
                    Codec = audioMatch.Groups[1].Value,
                    SampleRate = audioMatch.Groups[2].Value,
                    Channels = audioMatch.Groups[3].Value
                };

                // 尝试解析音频码率
                if (audioMatch.Groups[4].Success && double.TryParse(audioMatch.Groups[4].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var audioBitrate))
                {
                    audioStream.BitrateKbps = audioBitrate;
                    // 对于 BitrateMode，同视频流，暂时不设置或设置为 null。
                }
                else
                {
                    audioStream.BitrateKbps = null; // 如果没有找到码率，设置为 null
                }

                metadata.AudioStream = audioStream;
            }

            return metadata;
        }
        #endregion

        #region 生成 伪FMP4媒体 流
        /// <summary>
        /// 异步生成一个单文件的、碎片化的MP4（fMP4），适用于通过HTTP Range请求进行高效流式传输。
        /// 该文件将包含优化的 moov atom 和分段的 mdat atom，允许播放器在不下载整个文件的情况下开始播放。
        /// </summary>
        /// <param name="options">生成 fMP4 的配置选项，包括输入/输出文件、编码参数等。</param>
        /// <param name="threads">指定 FFmpeg 编码线程数。小于等于 0 时交由 FFmpeg 自动选择。</param>
        /// <param name="ct">用于取消异步操作的令牌。</param>
        /// <returns>一个表示异步操作完成的 Task。</returns>
        /// <exception cref="ArgumentException">当 options 或其关键属性（如 InputFile, OutputFile）为 null 或无效时抛出。</exception>
        /// <remarks>
        /// 此方法通过设置 `-movflags +faststart+frag_keyframe+separate_moof` 来实现。
        /// - `+faststart`: 将 moov atom（元数据）移动到文件开头，加速在线播放的启动。
        /// - `+frag_keyframe`: 创建基于关键帧的片段。
        /// - `+separate_moof`: 为每个片段（fragment）生成独立的 moof atom，这是实现 seek 和按需加载的关键。
        /// - `-f m4s`: 指定输出格式为 MPEG-4 Stream，这是生成 fMP4 的推荐格式。
        /// </remarks>
        /// <example>
        /// <code>
        /// var adapter = new FFmpegAdapter();
        /// var options = new FragmentedMp4Options
        /// {
        ///     InputFile = "source.mp4",
        ///     OutputFile = @"C:\output\streamable.m4s",
        ///     Crf = 23,
        ///     Preset = "medium",
        ///     Resolution = "1920x1080"
        /// };
        /// await adapter.GenerateFragmentedMp4Async(options);
        /// 
        /// // 生成一个只有音频的 fMP4 文件
        /// var audioOnlyOptions = new FragmentedMp4Options
        /// {
        ///     InputFile = "source.mp4",
        ///     OutputFile = @"C:\output\audio_only.m4s",
        ///     OmitVideo = true, // 忽略视频轨道
        ///     AudioBitrate = "192k"
        /// };
        /// await adapter.GenerateFragmentedMp4Async(audioOnlyOptions);
        /// </code>
        /// </example>
        public async Task GenerateFragmentedMp4Async(FragmentedMp4Options options, int threads = 0, CancellationToken ct = default)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (string.IsNullOrWhiteSpace(options.InputFile))
            {
                throw new ArgumentException("InputFile must be specified.", nameof(options.InputFile));
            }
            if (string.IsNullOrWhiteSpace(options.OutputFile))
            {
                throw new ArgumentException("OutputFile must be specified.", nameof(options.OutputFile));
            }
            if (options.OmitAudio && options.OmitVideo)
            {
                throw new ArgumentException("Cannot omit both audio and video streams.");
            }

            // 分离出输出目录和文件名
            var outputDirectory = Path.GetDirectoryName(options.OutputFile);
            var outputFileName = Path.GetFileName(options.OutputFile);

            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var argsBuilder = new List<string>
            {
                "-y",
                "-hide_banner",
                $"-i \"{options.InputFile}\""
            };

            // 视频编码设置
            if (!options.OmitVideo)
            {
                argsBuilder.Add($"-c:v {options.VideoCodec}");
                if (!string.IsNullOrWhiteSpace(options.Preset))
                {
                    argsBuilder.Add($"-preset {options.Preset}");
                }
                if (options.Crf.HasValue)
                {
                    argsBuilder.Add($"-crf {options.Crf.Value}");
                }
                if (!string.IsNullOrWhiteSpace(options.Resolution))
                {
                    argsBuilder.Add($"-s {options.Resolution}");
                }
                if (options.OutputFps.HasValue)
                {
                    argsBuilder.Add($"-vf \"fps={options.OutputFps.Value}\"");
                    // 更高级插值方案，如果需要使用 minterpolate 插件来插帧，插值滤镜 minterpolate 对 CPU 要求较高，适合高质量输出，可以取消注释以下行
                    //argsBuilder.Add($"-vf \"minterpolate='fps={options.OutputFps.Value}'\"");
                }
            }
            else
            {
                argsBuilder.Add("-vn"); // -vn 表示 no video
            }

            // 音频编码设置
            if (!options.OmitAudio)
            {
                argsBuilder.Add($"-c:a {options.AudioCodec}");
                if (!string.IsNullOrWhiteSpace(options.AudioBitrate))
                {
                    argsBuilder.Add($"-b:a {options.AudioBitrate}");
                }
            }
            else
            {
                argsBuilder.Add("-an"); // -an 表示 no audio
            }

            // 线程设置
            if (threads > 0)
            {
                argsBuilder.Add($"-threads {threads}");
            }

            // 核心的流媒体格式化参数
            // faststart: 将 moov box 移到文件开头，便于快速播放
            // frag_keyframe: 在每个关键帧处开始新的片段，提高 SEEK 精度
            // separate_moof: 将每个片段的 moof box 放置在片段数据之前，便于解析
            argsBuilder.Add("-movflags +faststart+frag_keyframe+separate_moof");

            // 输出格式和路径
            // 使用 .m4s 扩展名是常见的做法，以明确表示这是一个媒体段文件，
            // 但 .mp4 同样有效，因为其内部结构是兼容的。
            argsBuilder.Add("-f mp4");
            argsBuilder.Add($"\"{options.OutputFile}\"");

            string fullCommand = string.Join(" ", argsBuilder);

            Console.WriteLine("动态生成的 FFmpeg Fragmented MP4 命令:");
            Console.WriteLine(fullCommand);

            // 复用现有的执行逻辑，以获得进度报告和错误处理
            await this.ExecuteAsync(fullCommand, ct, workingDirectory: outputDirectory);
        }
        #endregion

        // 在 Dispose 或 Finalizer 中取消订阅事件是个好习惯，此处为简化省略
    }
}