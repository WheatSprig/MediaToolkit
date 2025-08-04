using MediaToolkit.Adapters.Bento4;
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
            //await DemoGetMetadataAsync(ffmpeg, inputFile);

            // --- 演示 2: 转换为DASH切片格式 (多文件) ---
            // 注意: 此演示会生成多个文件，耗时较长。如果只想测试演示3，可以注释掉下面这行。
            //await DemoConvertToDashAsync(ffmpeg, inputFile, outputDir);

            // --- 演示 3: 生成单文件 fMP4 流 ---
            //await DemoGenerateFragmentedMp4Async(ffmpeg, inputFile, outputDir);

            // Bento4演示
            try
            {
                // 验证Bento4是否可用（通过任意工具检测）
                var testBento4 = new Mp4InfoAdapter();
                Console.WriteLine($"\n成功找到 Bento4 工具集: {Path.GetDirectoryName(testBento4.ExecutablePath)}");

                // --- Bento4 演示（可按需注释） ---
                await DemoBento4Mp4InfoAsync(inputFile);
                await DemoBento4Mp4FragmentAsync(inputFile, outputDir);
                await DemoBento4Mp4SplitAsync(inputFile, outputDir);
                await DemoBento4EncryptDecryptAsync(inputFile, outputDir);
            }
            catch (FileNotFoundException ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\nBento4 工具未找到: {ex.Message}");
                Console.WriteLine("请安装Bento4并配置BENTO4_PATH环境变量以启用Bento4演示");
                Console.ResetColor();
            }

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

    #region FFmpeg 演示函数
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

    private static async Task DemoGenerateFragmentedMp4Async(FFmpegAdapter ffmpeg, string inputFile, string outputDir)
    {
        Console.WriteLine("\n--- 演示3: 生成单文件 FMP4 流 (分离音视频 到 单文件 HTTP 伪流) ---");

        ffmpeg.ProgressChanged += OnProgressChanged;

        // --- 子演示 3.1: 合并音视频到单个 fMP4 文件 ---
        string outputFmp4Merged = Path.Combine(outputDir, "fmp4_merged.mp4");
        Console.WriteLine("\n  > 3.1: 生成合并音视频的单文件 FMP4 (1080p)...");
        try
        {
            var optionsMerged = new FragmentedMp4Options
            {
                InputFile = inputFile,
                OutputFile = outputFmp4Merged,
                Resolution = "1920x1080",
                Preset = "fast",
                Crf = 22,
                MaxBitrate = "5000k",
                MinBitrate = "1000k",
                OutputFps = 30,
                AudioBitrate = "192k",
                GopSize = 48,
                Profile = "main",
                Level = "4.0",
                SceneCutThreshold = 0
            };

            await ffmpeg.GenerateFragmentedMp4Async(optionsMerged, threads: 2);
            Console.WriteLine($"\n  合并音视频的 fMP4 文件生成成功: {outputFmp4Merged}");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n  合并音视频的 fMP4 文件生成失败: {ex.Message}");
            Console.ResetColor();
        }

        // --- 子演示 3.2: 分别生成纯视频和纯音频的 fMP4 文件 ---
        string outputFmp4Video = Path.Combine(outputDir, "fmp4_video_only.mp4");
        string outputFmp4Audio = Path.Combine(outputDir, "fmp4_audio_only.m4a");
        Console.WriteLine("\n  > 3.2: 分离生成纯视频与纯音频的 FMP4 文件...");

        // **步骤 A: 生成纯视频文件**
        Console.WriteLine("    -> 正在生成纯视频文件 (720p)...");
        try
        {
            var optionsVideoOnly = new FragmentedMp4Options
            {
                InputFile = inputFile,
                OutputFile = outputFmp4Video,
                Resolution = "1280x720",
                Preset = "medium",
                Crf = 23,
                GopSize = 48,
                SceneCutThreshold = 0,
                OmitAudio = true // 关键：忽略音频轨道
            };
            await ffmpeg.GenerateFragmentedMp4Async(optionsVideoOnly, threads: 2);
            Console.WriteLine($"\n    fMP4 纯视频文件生成成功: {outputFmp4Video}");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n    fMP4 纯视频文件生成失败: {ex.Message}");
            Console.ResetColor();
        }

        // **步骤 B: 生成纯音频文件**
        Console.WriteLine("    -> 正在生成纯音频文件...");
        try
        {
            var optionsAudioOnly = new FragmentedMp4Options
            {
                InputFile = inputFile,
                OutputFile = outputFmp4Audio,
                AudioBitrate = "128k",
                OmitVideo = true // 关键：忽略视频轨道
            };
            await ffmpeg.GenerateFragmentedMp4Async(optionsAudioOnly, threads: 2);
            Console.WriteLine($"\n    fMP4 纯音频文件生成成功: {outputFmp4Audio}");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n    fMP4 纯音频文件生成失败: {ex.Message}");
            Console.ResetColor();
        }

        ffmpeg.ProgressChanged -= OnProgressChanged;
    }
    #endregion

    #region Bento4 演示函数（新增）
    /// <summary>
    /// Bento4演示1: 使用mp4info查看媒体信息
    /// </summary>
    private static async Task DemoBento4Mp4InfoAsync(string inputFile)
    {
        Console.WriteLine("\n--- Bento4 演示1: 使用mp4info查看媒体信息 ---");
        var mp4Info = new Mp4InfoAdapter();
        mp4Info.LogReceived += (s, e) => Console.WriteLine($"  [信息] {e}");

        try
        {
            // 获取详细信息
            var result = await mp4Info.GetInfoAsync(inputFile, detailed: true);
            if (result.ExitCode == 0)
            {
                Console.WriteLine("  信息获取成功（部分输出如上）");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  执行失败: {result.Error}");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  错误: {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Bento4演示2: 使用mp4fragment生成片段化MP4
    /// </summary>
    private static async Task DemoBento4Mp4FragmentAsync(string inputFile, string outputDir)
    {
        Console.WriteLine("\n--- Bento4 演示2: 使用mp4fragment生成HLS兼容片段 ---");
        var fragmenter = new Mp4FragmentAdapter();
        fragmenter.LogReceived += (s, e) => Console.WriteLine($"  [片段化] {e}");

        string outputFile = Path.Combine(outputDir, "bento4_fragmented.mp4");
        try
        {
            // 生成适合HLS的片段化MP4
            var result = await fragmenter.FragmentForHlsAsync(inputFile, outputFile, fragmentDurationMs: 2000);

            if (result.ExitCode == 0)
            {
                Console.WriteLine($"  片段化完成: {outputFile}");
                Console.WriteLine($"  文件大小: {new FileInfo(outputFile).Length / 1024 / 1024:F2} MB");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  片段化失败: {result.Error}");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  错误: {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Bento4演示3: 使用mp4split分割视频片段
    /// </summary>
    private static async Task DemoBento4Mp4SplitAsync(string inputFile, string outputDir)
    {
        Console.WriteLine("\n--- Bento4 演示3: 使用mp4split分割视频 ---");
        var splitter = new Mp4SplitAdapter();
        splitter.LogReceived += (s, e) => Console.WriteLine($"  [分割] {e}");

        string outputFile = Path.Combine(outputDir, "bento4_segment.mp4");
        try
        {
            // 从第30秒开始，提取10秒片段
            var result = await splitter.ExtractSegmentAsync(inputFile, outputFile, startTime: 30, duration: 10);

            if (result.ExitCode == 0)
            {
                Console.WriteLine($"  片段提取完成: {outputFile}");
                Console.WriteLine($"  片段时长: 10秒（从第30秒开始）");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  分割失败: {result.Error}");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  错误: {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// Bento4演示4: 使用mp4encrypt/mp4decrypt进行加密解密
    /// </summary>
    private static async Task DemoBento4EncryptDecryptAsync(string inputFile, string outputDir)
    {
        Console.WriteLine("\n--- Bento4 演示4: 加密与解密视频 ---");

        // 准备路径
        string encryptedFile = Path.Combine(outputDir, "bento4_encrypted.mp4");
        string decryptedFile = Path.Combine(outputDir, "bento4_decrypted.mp4");
        string testKey = "112233445566778899aabbccddeeff00"; // 示例密钥（实际使用需安全管理）
        string testKeyId = "aabbccddeeff11223344556677889900";

        try
        {
            // 1. 加密文件
            var encryptor = new Mp4EncryptAdapter();
            encryptor.LogReceived += (s, e) => Console.WriteLine($"  [加密] {e}");
            Console.WriteLine("  > 正在加密文件...");

            var encryptResult = await encryptor.EncryptAsync(
                inputFile,
                encryptedFile,
                key: testKey,
                keyId: testKeyId);

            if (encryptResult.ExitCode != 0)
            {
                throw new Exception($"加密失败: {encryptResult.Error}");
            }

            // 2. 解密文件
            var decryptor = new Mp4DecryptAdapter();
            decryptor.LogReceived += (s, e) => Console.WriteLine($"  [解密] {e}");
            Console.WriteLine("  > 正在解密文件...");

            var decryptResult = await decryptor.DecryptAsync(
                encryptedFile,
                decryptedFile,
                key: testKey);

            if (decryptResult.ExitCode != 0)
            {
                throw new Exception($"解密失败: {decryptResult.Error}");
            }

            Console.WriteLine($"  加密文件: {encryptedFile}");
            Console.WriteLine($"  解密文件: {decryptedFile}");
            Console.WriteLine("  加密解密流程完成（可对比原文件与解密文件）");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  错误: {ex.Message}");
            Console.ResetColor();
        }
    }
    #endregion

    // 进度显示辅助函数（共用）
    private static void OnProgressChanged(object? sender, ProgressEventArgs e)
    {
        var percent = (int)(e.Progress * 100);
        Console.Write($"\r  处理中... [{new string('■', percent / 5).PadRight(20)}] {percent}%");
    }
}