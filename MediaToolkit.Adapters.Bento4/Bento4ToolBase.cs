using System;
using System.Threading;
using System.Threading.Tasks;
using MediaToolkit.Core;


// **************************************************************
// Bento4 适配器特性说明
// **************************************************************
// 1. 由于 Bento4 是 C/C++ 开发的程序，在默认非 Unicode 编码的 Windows 系统（如简体中文环境）中，
//    对含宽字符（如中文、特殊符号）的文件路径支持有限，可能出现"无法打开文件"（cannot open input file）等错误。
// 2. 解决方案：处理前将文件复制到纯英文路径（无空格/特殊字符），避免直接使用原路径。
// 3. 所有命令行参数必须通过 ArgumentBuilder 构建，确保格式正确。
// 注意：ArgumentBuilder 仅解决参数格式问题，无法解决 Bento4 对非 ASCII 字符的编码限制
// **************************************************************


namespace MediaToolkit.Adapters.Bento4
{
    /// <summary>
    /// Bento4工具集的抽象基类，所有Bento4工具适配器继承此类
    /// </summary>
    public abstract class Bento4ToolBase : IMediaToolAdapter
    {
        private readonly ProcessRunner processRunner;

        /// <summary>
        /// 初始化Bento4工具基类
        /// </summary>
        /// <param name="toolName">具体工具名称（如mp4info、mp4dump）</param>
        /// <param name="toolSetPath">Bento4工具集目录（可选，自动查找如果为null）</param>
        protected Bento4ToolBase(string toolName, string toolSetPath = null)
        {
            ToolName = toolName ?? throw new ArgumentNullException(nameof(toolName));
            ExecutablePath = Bento4ToolFinder.FindToolExecutable(toolName, toolSetPath);
            processRunner = new ProcessRunner(ExecutablePath);
            processRunner.LogReceived += (s, e) => LogReceived?.Invoke(this, e);
        }

        /// <inheritdoc />
        public string ToolName { get; }

        /// <inheritdoc />
        public string ExecutablePath { get; }

        /// <inheritdoc />
        public event EventHandler<string> LogReceived;

        /// <inheritdoc />
        public Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default, string workingDirectory = null)
        {
            return processRunner.ExecuteAsync(arguments, cancellationToken, workingDirectory);
        }
    }
}
