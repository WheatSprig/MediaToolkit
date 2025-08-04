using MediaToolkit.Core;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MediaToolkit.Adapters.Bento4
{
    /// <summary>
    /// Bento4中mp4dump工具的适配器
    /// 用于 dump MP4文件的底层结构
    /// </summary>
    public class Mp4DumpAdapter : Bento4ToolBase
    {
        /// <summary>
        /// 初始化mp4dump工具适配器
        /// </summary>
        /// <param name="toolSetPath">Bento4工具集目录（可选）</param>
        public Mp4DumpAdapter(string toolSetPath = null)
            : base("mp4dump", toolSetPath)
        {
        }

        /// <summary>
        /// 执行mp4dump命令
        /// </summary>
        /// <param name="inputFile">MP4文件路径</param>
        /// <param name="hexOutput">是否以十六进制格式输出</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>工具执行结果</returns>
        public Task<ToolResult> DumpAsync(
            string inputFile,
            bool hexOutput = false,
            CancellationToken cancellationToken = default)
        {
            var arguments = $"{(hexOutput ? "-x " : "")}\"{inputFile}\"";
            return ExecuteAsync(arguments, cancellationToken, Path.GetDirectoryName(inputFile));
        }
    }
}
