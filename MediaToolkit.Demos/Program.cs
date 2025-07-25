using MediaToolkit.Adapters.FFmpeg;

namespace MediaToolkit.Demos
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("MediaToolkit .NET Standard 2.0 Demo");

            try
            {
                // 假设你的 ffmpeg.exe 放在了 PATH 环境变量指向的目录中
                var ffmpeg = new FFmpegAdapter();
                Console.WriteLine($"成功找到 FFmpeg: {ffmpeg.ExecutablePath}");

                ffmpeg.LogReceived += (s, e) => Console.WriteLine($"[LOG] {e}");
                ffmpeg.ProgressChanged += (s, e) => Console.WriteLine($"[PROGRESS] {e.Progress:P2}");

                // 创建一个假的输入文件用于演示
                var inputFile = "input.tmp";
                File.WriteAllText(inputFile, "This is a dummy file.");

                var outputFile = "output.tmp";

                Console.WriteLine("开始执行FFmpeg (这个命令会因为输入文件无效而失败，用于演示错误处理)...");

                await ffmpeg.ConvertAsync(inputFile, outputFile, "-c copy");

                Console.WriteLine("命令执行完毕！");
            }
            catch (FileNotFoundException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"错误: {ex.Message}");
                Console.ResetColor();
                Console.WriteLine("请确保 ffmpeg 在你的系统 PATH 中，或设置 FFMPEG_PATH 环境变量。");
            }
            catch (InvalidOperationException ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"命令执行失败 (符合预期): {ex.Message}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"未知错误: {ex.ToString()}");
                Console.ResetColor();
            }
        }
    }
}
