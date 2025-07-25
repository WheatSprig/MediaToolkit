using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MediaToolkit.Desktop
{
    public class ConsoleNotifier : IProcessNotifier
    {
        public void OnStarted(string toolName, string args) =>
            Console.WriteLine($"[开始] {toolName} 参数: {args}");

        public void OnOutput(string line) =>
            Console.WriteLine($"[输出] {line}");

        public void OnError(string line) =>
            Console.WriteLine($"[错误] {line}");

        public void OnProgress(string toolName, double progress) =>
            Console.WriteLine($"[进度] {toolName}: {progress:P0}");

        public void OnCompleted(string toolName, bool success, string? message = null) =>
            Console.WriteLine($"[完成] {toolName} {(success ? "成功" : "失败")}，{message}");
    }
}
