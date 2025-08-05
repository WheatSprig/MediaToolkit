using MediaToolkit.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        /// 输出格式选项
        /// </summary>
        public enum OutputFormat
        {
            /// <summary>
            /// 文本格式（默认）
            /// </summary>
            Text,
            /// <summary>
            /// JSON格式
            /// </summary>
            Json
        }

        /// <summary>
        /// 轨道信息
        /// </summary>
        public class TrackInfo
        {
            /// <summary>
            /// 轨道ID
            /// </summary>
            public int TrackId { get; set; }
            
            /// <summary>
            /// 解密密钥（128位十六进制）
            /// </summary>
            public string Key { get; set; }
        }

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
        /// <param name="verbosity">详细程度（0-3之间）</param>
        /// <param name="tracks">需要提取的轨道信息（可选）</param>
        /// <param name="format">输出格式（默认文本）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>工具执行结果</returns>
        public Task<ToolResult> DumpAsync(
            string inputFile,
            int verbosity = 0,
            IEnumerable<TrackInfo> tracks = null,
            OutputFormat format = OutputFormat.Text,
            CancellationToken cancellationToken = default)
        {
            // 验证输入参数
            if (string.IsNullOrEmpty(inputFile))
                throw new ArgumentNullException(nameof(inputFile));
            
            if (!File.Exists(inputFile))
                throw new FileNotFoundException("输入文件不存在", inputFile);
            
            if (verbosity < 0 || verbosity > 3)
                throw new ArgumentOutOfRangeException(nameof(verbosity), "详细程度必须在0到3之间");

            // 使用ArgumentBuilder构建命令参数
            var argumentBuilder = new ArgumentBuilder();

            // 添加详细程度参数
            if (verbosity > 0)
            {
                argumentBuilder.Append("--verbosity", verbosity.ToString());
            }

            // 添加轨道参数
            if (tracks?.Any() == true)
            {
                foreach (var track in tracks)
                {
                    if (track.TrackId <= 0)
                        throw new ArgumentException("轨道ID必须大于0", nameof(tracks));
                    
                    var trackValue = track.TrackId.ToString();
                    if (!string.IsNullOrEmpty(track.Key))
                    {
                        trackValue += $":{track.Key}";
                    }
                    argumentBuilder.Append("--track", trackValue);
                }
            }

            // 添加格式参数
            if (format != OutputFormat.Text)
            {
                argumentBuilder.Append("--format", format.ToString().ToLowerInvariant());
            }

            // 添加输入文件
            argumentBuilder.Append(inputFile);

            // 执行命令
            return ExecuteAsync(argumentBuilder.ToString(), cancellationToken, Path.GetDirectoryName(inputFile));
        }
    }
}
