using MediaToolkit.Core;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MediaToolkit.Adapters.Bento4
{
    /// <summary>
    /// Bento4中mp4info工具的适配器
    /// 用于读取MP4文件的元信息
    /// </summary>
    public class Mp4InfoAdapter : Bento4ToolBase
    {
        /// <summary>
        /// 初始化mp4info工具适配器
        /// </summary>
        /// <param name="toolSetPath">Bento4工具集目录（可选）</param>
        public Mp4InfoAdapter(string toolSetPath = null)
            : base("mp4info", toolSetPath)
        {
        }

        /// <summary>
        /// 执行mp4info命令获取文件信息
        /// </summary>
        /// <param name="inputFile">MP4文件路径</param>
        /// <param name="detailed">是否显示详细信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>工具执行结果</returns>
        public Task<ToolResult> GetInfoAsync(
            string inputFile,
            bool detailed = false,
            CancellationToken cancellationToken = default)
        {
            //var arguments = $"{(detailed ? "-v " : "")}\"{inputFile}\"";
            //return ExecuteAsync(arguments, cancellationToken, Path.GetDirectoryName(inputFile));
            // 使用core中的 ArgumentBuilder
            ArgumentBuilder builder = new ArgumentBuilder();

            builder.Append("--verbose", detailed); // 如果 detailed 为 true, 则添加 -verbose
            builder.Append(inputFile);      // 添加输入文件路径，ArgumentBuilder 会自动处理空格

            return ExecuteAsync(builder.ToString(), cancellationToken);
        }
    }
}
