using MediaToolkit.Core.Core;

namespace MediaToolkit.Desktop
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var runner = new ExternalToolRunner("/usr/bin/ffmpeg", new ConsoleNotifier());

            bool result = await runner.RunAsync(
                "-i input.mp4 -codec copy -f hls output.m3u8",
                "ffmpeg"
            );
            Console.WriteLine($"执行结果：{result}");
        }
    }
}
