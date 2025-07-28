using MediaToolkit.Adapters.FFmpeg;
using MediaToolkit.Adapters.FFmpeg.Models;
using MediaToolkit.Core;
using System;
using System.IO;
using System.Threading.Tasks;

internal class Program
{
    static async Task Main(string[] args)
    {
        // 确保在程序退出时清理所有注册的进程
        AppDomain.CurrentDomain.ProcessExit += (_, _) => ProcessRegistry.KillAll();

        Console.WriteLine("MediaToolkit .NET Standard 2.0 Demo");

        Console.WriteLine("请输入视频文件完整路径（可手动输入或右键粘贴）：");
        string? input = Console.ReadLine();
        string inputFile = input?.Trim('"') ?? throw new InvalidOperationException("未输入有效路径"); // 去掉自动添加的引号，并处理空输入
        string outputDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "MediaToolkit_Output");

        if (!File.Exists(inputFile))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n错误: 演示需要一个真实的输入文件，路径未找到: '{inputFile}'");
            Console.ResetColor();
            return;
        }

        // 确保输出目录存在
        Directory.CreateDirectory(outputDir);

        try
        {
            var ffmpeg = new FFmpegAdapter();
            Console.WriteLine($"成功找到 FFmpeg: {ffmpeg.ExecutablePath}");

            // --- 演示 1: 获取元数据 ---
            await DemoGetMetadataAsync(ffmpeg, inputFile);

            // --- 演示 2: 转换为DASH格式 ---
            await DemoGenerateFmp4StreamAsync(ffmpeg, inputFile, outputDir);
        }
        catch (FileNotFoundException ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n错误: {ex.Message}");
            Console.ResetColor();
            Console.WriteLine("请确保 ffmpeg 在你的系统 PATH 中，或设置 FFMPEG_PATH 环境变量。");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n发生未知错误: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static async Task DemoGetMetadataAsync(FFmpegAdapter ffmpeg, string inputFile)
    {
        Console.WriteLine("\n--- 演示1: 获取元数据 ---");
        var metadata = await ffmpeg.GetMetadataAsync(inputFile);

        // 增强检查：如果 Duration 为 0 或 null，认为解析失败
        if (metadata == null || metadata.Duration == TimeSpan.Zero)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  警告: 未能从文件中解析出元数据。可能是文件格式特殊或FFmpeg输出无法识别。");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"  时长: {metadata.Duration:c}");
        Console.WriteLine($"  整体码率: {metadata.OverallBitrateKbps:N0} Kbps");

        if (metadata.VideoStream != null)
        {
            Console.WriteLine("  视频信息:");
            Console.WriteLine($"    > 编码格式: {metadata.VideoStream.Codec}");
            Console.WriteLine($"    > 分辨率  : {metadata.VideoStream.Resolution}");
            Console.WriteLine($"    > 帧率    : {metadata.VideoStream.Fps:N2} fps");
            Console.WriteLine($"    > 码率    : {metadata.VideoStream.BitrateKbps:N0} Kbps ({metadata.VideoStream.BitrateMode ?? "未知"})");
        }
        else
        {
            Console.WriteLine("  视频信息: 未找到");
        }

        if (metadata.AudioStream != null)
        {
            Console.WriteLine("  视频信息:");

            Console.WriteLine($"    > 编码格式: {metadata.AudioStream.Codec}");
            Console.WriteLine($"    > 采样率  : {metadata.AudioStream.SampleRate}");
            Console.WriteLine($"    > 声道    : {metadata.AudioStream.Channels}");
            Console.WriteLine($"    > 码率    : {(metadata.AudioStream.BitrateKbps.HasValue ? $"{metadata.AudioStream.BitrateKbps:N0} Kbps" : "N/A")} ({metadata.AudioStream.BitrateMode ?? "未知"})");
        }
        else
        {
            Console.WriteLine("  音频信息: 未找到");
        }
    }

    private static async Task DemoConvertToDashAsync(FFmpegAdapter ffmpeg, string inputFile, string outputDir)
    {
        Console.WriteLine("\n--- 演示2: 转换为多种分辨率DASH流 (1080P+/1080P60/720P60/720P/480P/360P) ---");
        var manifestPath = Path.Combine(outputDir, "stream.mpd");

        // 构造 DashOutputOptions
        var options = new DashOutputOptions
        {
            InputFile = inputFile,
            OutputDirectory = outputDir,
            ManifestFileName = manifestPath,
            SegmentDuration = 4,                       // 4 秒一片
            VideoProfiles = new List<VideoStreamProfile>   // 6 条清晰度
            {
                new() { Resolution = "1920x1080", Bitrate = "5000k", Fps = 30 },
                new() { Resolution = "1920x1080", Bitrate = "6000k", Fps = 60 },
                new() { Resolution = "1280x720",  Bitrate = "2500k", Fps = 60 },
                new() { Resolution = "1280x720",  Bitrate = "2000k", Fps = 30 },
                new() { Resolution = "854x480",   Bitrate = "1000k", Fps = 30 },
                new() { Resolution = "640x360",   Bitrate = "600k",  Fps = 30 }
            },
            AudioProfile = new AudioStreamProfile
            {
                Codec = "aac",
                Bitrate = "128k"
            }
        };

        // 订阅进度事件
        ffmpeg.ProgressChanged += OnProgressChanged;

        Console.WriteLine($"  输出目录: {outputDir}");
        await ffmpeg.GenerateDashAsync(options, threads: 2);

        // 取消订阅，避免影响其他任务
        ffmpeg.ProgressChanged -= OnProgressChanged;

        Console.WriteLine("\n  DASH 转换完成!");
        Console.WriteLine($"  清单文件位于: {manifestPath}");
    }

    private static async Task DemoGenerateFmp4StreamAsync(FFmpegAdapter ffmpeg, string inputFile, string outputDir)
    {
        Console.WriteLine("\n--- 演示3: 生成单文件 FMP4 流 (1080P, 可选择分离音视频) ---");

        // --- 子演示 3.1: 合并音视频到单个 FMP4 文件 ---
        string outputFmp4Merged = Path.Combine(outputDir, "single_file_merged.mp4"); // Renamed
        Console.WriteLine("\n  > 3.1: 生成合并音视频的单文件 FMP4...");
        try
        {
            var optionsMerged = new Fmp4OutputOptions
            {
                InputFile = inputFile,
                OutputFile = outputFmp4Merged,
                VideoProfile = new VideoStreamProfile
                {
                    Resolution = "1920x1080",
                    Bitrate = "5M",
                    Fps = 30
                },
                AudioProfile = new AudioStreamProfile
                {
                    Bitrate = "192k",
                    Codec = "aac"
                },
                SeparateAudioVideo = false, // Not separating
                CustomOptions = "-preset fast -crf 22" // Example: fast preset and constant rate factor 22
            };

            // Subscribe to progress events for this specific task
            ffmpeg.ProgressChanged += OnProgressChanged;

            await ffmpeg.GenerateFmp4StreamAsync(optionsMerged, threads: 2);

            // Unsubscribe
            ffmpeg.ProgressChanged -= OnProgressChanged;

            Console.WriteLine($"\n  合并音视频的 FMP4 文件生成成功: {outputFmp4Merged}");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n  合并音视频的 FMP4 文件生成失败: {ex.Message}");
            Console.ResetColor();
        }

        // --- 子演示 3.2: 分离音视频到不同的 FMP4 文件 ---
        string outputFmp4Video = Path.Combine(outputDir, "single_file_video.mp4"); // Renamed
        string outputFmp4Audio = Path.Combine(outputDir, "single_file_video_audio.m4a"); // Automatically generated by SeparateAudioVideo=true
        Console.WriteLine("\n  > 3.2: 生成分离音视频的单文件 FMP4...");
        try
        {
            var optionsSeparated = new Fmp4OutputOptions
            {
                InputFile = inputFile,
                OutputFile = outputFmp4Video, // This will be the video file path
                VideoProfile = new VideoStreamProfile
                {
                    Resolution = "1280x720",
                    Bitrate = "2.5M",
                    Fps = 30
                },
                AudioProfile = new AudioStreamProfile
                {
                    Bitrate = "128k",
                    Codec = "aac"
                },
                SeparateAudioVideo = true, // Separating video and audio
                CustomOptions = "-preset medium -crf 23"
            };

            // Subscribe to progress events for this specific task
            ffmpeg.ProgressChanged += OnProgressChanged;

            await ffmpeg.GenerateFmp4StreamAsync(optionsSeparated, threads: 2);

            // Unsubscribe
            ffmpeg.ProgressChanged -= OnProgressChanged;

            Console.WriteLine($"\n  FMP4 视频文件生成成功: {outputFmp4Video}");
            Console.WriteLine($"  FMP4 音频文件生成成功: {outputFmp4Audio}");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n  分离音视频的 FMP4 文件生成失败: {ex.Message}");
            Console.ResetColor();
        }
    }


    private static void OnProgressChanged(object? sender, ProgressEventArgs e)
    {
        var percent = (int)(e.Progress * 100);
        Console.Write($"\r  处理中... [{new string('■', percent / 5).PadRight(20)}] {percent}%");
    }
}