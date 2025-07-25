using MediaToolkit.Core;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MediaToolkit.Adapters.FFmpeg
{
    // 定义一个进度事件参数类
    public class ProgressEventArgs : EventArgs
    {
        public ProgressEventArgs(TimeSpan processed, TimeSpan total)
        {
            TotalDuration = total;
            ProcessedDuration = processed;
        }

        public TimeSpan TotalDuration { get; }
        public TimeSpan ProcessedDuration { get; }
        public double Progress => TotalDuration > TimeSpan.Zero ? ProcessedDuration.TotalSeconds / TotalDuration.TotalSeconds : 0;
    }

    public class FFmpegAdapter : IMediaToolAdapter
    {
        public string ToolName => "ffmpeg";
        public string ExecutablePath { get; }

        public event EventHandler<string> LogReceived;
        public event EventHandler<ProgressEventArgs> ProgressChanged;

        private readonly ProcessRunner _runner;
        private static readonly Regex DurationRegex = new Regex(@"Duration: ([^,]+),", RegexOptions.Compiled);
        private static readonly Regex ProgressRegex = new Regex(@"time=([^ ]+)", RegexOptions.Compiled);
        private TimeSpan _totalDuration;

        public FFmpegAdapter(string ffmpegPath = null)
        {
            ExecutablePath = ToolFinder.FindExecutablePath(ToolName, ffmpegPath);
            _runner = new ProcessRunner(ExecutablePath);
            _runner.LogReceived += OnRunnerLogReceived;
        }

        private void OnRunnerLogReceived(object sender, string log)
        {
            LogReceived?.Invoke(this, log);
            ParseProgress(log);
        }

        public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
        {
            // 在执行前重置时长，以便为下一次运行做准备
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

        // 在 Dispose 或 Finalizer 中取消订阅事件是个好习惯，此处为简化省略
    }
}