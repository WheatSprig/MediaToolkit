using MediaToolkit.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MediaToolkit.Adapters.Bento4
{
    /// <summary>
    /// Bento4中mp4decrypt工具的适配器
    /// 用于解密加密的MP4文件
    /// </summary>
    public class Mp4DecryptAdapter : Bento4ToolBase
    {
        /// <summary>
        /// 解密密钥信息
        /// </summary>
        public class DecryptionKey
        {
            /// <summary>
            /// 轨道ID（十进制）或128位KID（十六进制）
            /// - 普通加密：使用轨道ID（如1、2）
            /// - Marlin IPMP/ACGK：使用0作为轨道ID
            /// - MPEG-CENC：使用KID（32个十六进制字符）
            /// </summary>
            public string Id { get; set; }

            /// <summary>
            /// 128位解密密钥（32个十六进制字符）
            /// </summary>
            public string Key { get; set; }
        }

        /// <summary>
        /// 初始化mp4decrypt工具适配器
        /// </summary>
        /// <param name="toolSetPath">Bento4工具集目录（可选）</param>
        public Mp4DecryptAdapter(string toolSetPath = null)
            : base("mp4decrypt", toolSetPath)
        {
        }

        /// <summary>
        /// 解密MP4文件（支持多轨道/多密钥）
        /// </summary>
        /// <param name="inputFile">加密的MP4文件路径</param>
        /// <param name="outputFile">解密后的MP4文件路径</param>
        /// <param name="keys">解密密钥列表（至少需要一个）</param>
        /// <param name="showProgress">是否显示进度详情</param>
        /// <param name="fragmentsInfo">片段信息文件路径（可选）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>工具执行结果</returns>
        public Task<ToolResult> DecryptAsync(
            string inputFile,
            string outputFile,
            IEnumerable<DecryptionKey> keys,
            bool showProgress = false,
            string fragmentsInfo = null,
            CancellationToken cancellationToken = default)
        {
            // 验证核心参数
            if (string.IsNullOrEmpty(inputFile))
                throw new ArgumentNullException(nameof(inputFile));
            
            if (!File.Exists(inputFile))
                throw new FileNotFoundException("输入文件不存在", inputFile);
            
            if (string.IsNullOrEmpty(outputFile))
                throw new ArgumentNullException(nameof(outputFile));
            
            if (keys == null || !keys.GetEnumerator().MoveNext())
                throw new ArgumentException("至少需要指定一个解密密钥", nameof(keys));

            // 验证密钥格式
            foreach (var key in keys)
            {
                if (string.IsNullOrEmpty(key.Id))
                    throw new ArgumentException("密钥ID（轨道ID或KID）不能为空", nameof(keys));
                
                if (string.IsNullOrEmpty(key.Key) || key.Key.Length != 32)
                    throw new ArgumentException($"ID为{key.Id}的密钥必须是32个字符的十六进制字符串", nameof(keys));
            }

            // 创建输出目录
            var outputDir = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // 构建命令参数
            var argsBuilder = new ArgumentBuilder();

            // 显示进度
            argsBuilder.Append("--show-progress", showProgress);

            // 片段信息文件
            if (!string.IsNullOrEmpty(fragmentsInfo))
            {
                argsBuilder.Append("--fragments-info", fragmentsInfo);
            }

            // 添加所有密钥（工具要求使用--key而非-k）
            foreach (var key in keys)
            {
                argsBuilder.Append("--key", $"{key.Id}:{key.Key}");
            }

            // 输入输出文件
            argsBuilder.Append(inputFile);
            argsBuilder.Append(outputFile);

            // 执行命令
            return ExecuteAsync(
                argsBuilder.ToString(),
                cancellationToken,
                outputDir);
        }

        /// <summary>
        /// 解密MP4文件（单密钥简化版，适用于单轨道加密）
        /// </summary>
        /// <param name="inputFile">加密的MP4文件路径</param>
        /// <param name="outputFile">解密后的MP4文件路径</param>
        /// <param name="trackId">轨道ID（默认1）</param>
        /// <param name="key">128位解密密钥（32个十六进制字符）</param>
        /// <param name="showProgress">是否显示进度详情</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>工具执行结果</returns>
        public Task<ToolResult> DecryptAsync(
            string inputFile,
            string outputFile,
            int trackId = 1,
            string key = null,
            bool showProgress = false,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(key))
                throw new ArgumentNullException(nameof(key));

            // 包装为单密钥列表，调用重载方法
            return DecryptAsync(
                inputFile,
                outputFile,
                new List<DecryptionKey>
                {
                    new DecryptionKey
                    {
                        Id = trackId.ToString(), // 轨道ID为十进制字符串
                        Key = key
                    }
                },
                showProgress,
                null,
                cancellationToken);
        }
    }
}
    