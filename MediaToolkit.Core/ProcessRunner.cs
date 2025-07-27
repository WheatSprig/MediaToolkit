using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MediaToolkit.Core
{
    public class ProcessRunner
    {
        private readonly string _executablePath;

        /// <summary>
        /// 当进程接收到任何一行输出时触发（包括标准输出和标准错误）。
        /// </summary>
        public event EventHandler<string> LogReceived;

        public ProcessRunner(string executablePath)
        {
            this._executablePath = executablePath;
        }

        public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default, string workingDirectory = null)
        {
            var tcs = new TaskCompletionSource<ToolResult>();

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _executablePath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory ?? Path.GetTempPath()  // 使用临时目录作为更安全的后备
                },
                EnableRaisingEvents = true
            };

            var stdOutput = new StringBuilder();
            var stdError = new StringBuilder();

            // 使用 TaskCompletionSource 来处理进程退出事件，这是 .NET Standard 2.0 中最高效的异步等待方式
            process.Exited += (sender, args) =>
            {
                // 等待异步流读取完成
                process.WaitForExit();
                var result = new ToolResult(process.ExitCode, stdOutput.ToString(), stdError.ToString());
                tcs.TrySetResult(result);
            };

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data == null) return;
                stdOutput.AppendLine(e.Data);

                // 触发日志接收事件
                LogReceived?.Invoke(this, e.Data);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data == null) return;
                stdError.AppendLine(e.Data);

                // 触发日志接收事件
                LogReceived?.Invoke(this, e.Data);
            };

            // 注册CancellationToken
            cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch (InvalidOperationException) { /* 进程可能已经退出，忽略 */}
                tcs.TrySetCanceled();
            });

            if (!process.Start())
            {
                tcs.TrySetException(new InvalidOperationException("Failed to start the process."));
                return tcs.Task;
            }

            ProcessRegistry.Register(process); // 注册进程以便跟踪和管理

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return tcs.Task;
        }
    }
}