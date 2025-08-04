using System;
using System.Threading;
using System.Threading.Tasks;
using MediaToolkit.Core;

namespace MediaToolkit.Adapters.Bento4
{
    /// <summary>
    /// Bento4中mp4encrypt工具的适配器
    /// 用于加密MP4文件
    /// </summary>
    public class Mp4EncryptAdapter : Bento4ToolBase
    {
        /// <summary>
        /// 加密模式
        /// </summary>
        public enum EncryptionMode
        {
            /// <summary>
            /// 通用加密模式（默认）
            /// </summary>
            CommonEncryption,
            /// <summary>
            /// 苹果FairPlay加密模式
            /// </summary>
            FairPlay,
            /// <summary>
            /// Widevine加密模式
            /// </summary>
            Widevine,
            /// <summary>
            /// PlayReady加密模式
            /// </summary>
            PlayReady
        }

        /// <summary>
        /// 初始化mp4encrypt工具适配器
        /// </summary>
        /// <param name="toolSetPath">Bento4工具集目录（可选）</param>
        public Mp4EncryptAdapter(string toolSetPath = null)
            : base("mp4encrypt", toolSetPath)
        {
        }

        /// <summary>
        /// 加密MP4文件
        /// </summary>
        /// <param name="inputFile">输入MP4文件路径</param>
        /// <param name="outputFile">输出加密后的MP4文件路径</param>
        /// <param name="key">加密密钥（16字节）</param>
        /// <param name="keyId">密钥ID（16字节）</param>
        /// <param name="mode">加密模式</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>工具执行结果</returns>
        public Task<ToolResult> EncryptAsync(
            string inputFile,
            string outputFile,
            string key,
            string keyId,
            EncryptionMode mode = EncryptionMode.CommonEncryption,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(inputFile))
                throw new ArgumentNullException(nameof(inputFile));

            if (string.IsNullOrEmpty(outputFile))
                throw new ArgumentNullException(nameof(outputFile));

            if (string.IsNullOrEmpty(key) || key.Length != 32) // 16字节的十六进制表示
                throw new ArgumentException("密钥必须是32个字符的十六进制字符串", nameof(key));

            if (string.IsNullOrEmpty(keyId) || keyId.Length != 32) // 16字节的十六进制表示
                throw new ArgumentException("密钥ID必须是32个字符的十六进制字符串", nameof(keyId));

            var argsBuilder = new System.Text.StringBuilder();

            // 根据加密模式添加参数
            switch (mode)
            {
                case EncryptionMode.FairPlay:
                    argsBuilder.Append($"-f {key}:{keyId} ");
                    break;
                case EncryptionMode.Widevine:
                    argsBuilder.Append($"-w {key}:{keyId} ");
                    break;
                case EncryptionMode.PlayReady:
                    argsBuilder.Append($"-p {key}:{keyId} ");
                    break;
                default: // CommonEncryption
                    argsBuilder.Append($"-k {key}:{keyId} ");
                    break;
            }

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
