using System;
using System.Threading;
using System.Threading.Tasks;
using MediaToolkit.Core;

namespace MediaToolkit.Adapters.Bento4
{
    /// <summary>
    /// Bento4中mp4decrypt工具的适配器
    /// 用于解密加密的MP4文件
    /// </summary>
    public class Mp4DecryptAdapter : Bento4ToolBase
    {
        /// <summary>
        /// 初始化mp4decrypt工具适配器
        /// </summary>
        /// <param name="toolSetPath">Bento4工具集目录（可选）</param>
        public Mp4DecryptAdapter(string toolSetPath = null)
            : base("mp4decrypt", toolSetPath)
        {
        }

        /// <summary>
        /// 解密MP4文件
        /// </summary>
        /// <param name="inputFile">加密的MP4文件路径</param>
        /// <param name="outputFile">解密后的MP4文件路径</param>
        /// <param name="key">解密密钥（16字节）</param>
        /// <param name="removeProtection">是否完全移除保护信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>工具执行结果</returns>
        public Task<ToolResult> DecryptAsync(
            string inputFile,
            string outputFile,
            string key,
            bool removeProtection = true,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(inputFile))
                throw new ArgumentNullException(nameof(inputFile));

            if (string.IsNullOrEmpty(outputFile))
                throw new ArgumentNullException(nameof(outputFile));

            if (string.IsNullOrEmpty(key) || key.Length != 32) // 16字节的十六进制表示
                throw new ArgumentException("密钥必须是32个字符的十六进制字符串", nameof(key));

            var argsBuilder = new System.Text.StringBuilder();

            // 添加密钥
            argsBuilder.Append($"-k {key} ");

            // 是否移除保护信息
            if (removeProtection)
                argsBuilder.Append("-r ");

            // 添加输入输出文件
            argsBuilder.Append($"\"{inputFile}\" \"{outputFile}\"");

            // 使用输出目录作为工作目录
            var workingDir = System.IO.Path.GetDirectoryName(outputFile);
            if (!System.IO.Directory.Exists(workingDir))
            {
                System.IO.Directory.CreateDirectory(workingDir);
            }

            return ExecuteAsync(
                argsBuilder.ToString(),
                cancellationToken,
                workingDir);
        }
    }
}
