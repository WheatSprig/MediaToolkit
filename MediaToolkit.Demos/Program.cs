// 在你的 Demos 项目的 Program.cs 中
using MediaToolkit.Adapters.FFmpeg;
using System;
using System.IO;
using System.Threading.Tasks;

internal class Program
{
    static async Task Main(string[] args)
    {
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
            await DemoConvertToDashAsync(ffmpeg, inputFile, outputDir);
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

        // --- 增强了检查，确保元数据和其中的流信息都存在 ---
        if (metadata?.Duration == TimeSpan.Zero)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  警告: 未能从文件中解析出元数据。可能是文件格式特殊或FFmpeg输出无法识别。");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"  时长: {metadata.Duration}");
        if (metadata.VideoStream != null)
        {
            Console.WriteLine("  视频信息:");
            Console.WriteLine($"    > 格式: {metadata.VideoStream.Codec}");
            Console.WriteLine($"    > 分辨率: {metadata.VideoStream.Resolution}");
            Console.WriteLine($"    > 帧率: {metadata.VideoStream.Fps} fps");
        }
        else
        {
            Console.WriteLine("  视频信息: 未找到");
        }

        if (metadata.AudioStream != null)
        {
            Console.WriteLine("  音频信息:");
            Console.WriteLine($"    > 格式: {metadata.AudioStream.Codec}");
            Console.WriteLine($"    > 采样率: {metadata.AudioStream.SampleRate}");
            Console.WriteLine($"    > 声道: {metadata.AudioStream.Channels}");
        }
        else
        {
            Console.WriteLine("  音频信息: 未找到");
        }
    }

    private static async Task DemoConvertToDashAsync(FFmpegAdapter ffmpeg, string inputFile, string outputDir)
    {
        Console.WriteLine("\n--- 演示2: 转换为DASH流 (1080p) ---");
        var manifestPath = Path.Combine(outputDir, "stream.mpd");

        // 订阅进度事件
        ffmpeg.ProgressChanged += OnProgressChanged;

        Console.WriteLine($"  输出目录: {outputDir}");
        await ffmpeg.ConvertToDash1080pAsync(inputFile, manifestPath);

        // 取消订阅，避免影响其他任务
        ffmpeg.ProgressChanged -= OnProgressChanged;

        Console.WriteLine("\n  DASH 转换完成!");
        Console.WriteLine($"  清单文件位于: {manifestPath}");
    }

    private static void OnProgressChanged(object? sender, ProgressEventArgs e)
    {
        var percent = (int)(e.Progress * 100);
        Console.Write($"\r  处理中... [{new string('■', percent / 5).PadRight(20)}] {percent}%");
    }
}