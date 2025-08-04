using System;
using System.Threading;
using System.Threading.Tasks;
using MediaToolkit.Core;

namespace MediaToolkit.Adapters.Bento4
{
    /// <summary>
    /// Bento4中mp4split工具的适配器
    /// 用于分割MP4文件或提取部分内容
    /// </summary>
    public class Mp4SplitAdapter : Bento4ToolBase
    {
        /// <summary>
        /// 初始化mp4split工具适配器
        /// </summary>
        /// <param name="toolSetPath">Bento4工具集目录（可选）</param>
        public Mp4SplitAdapter(string toolSetPath = null)
            : base("mp4split", toolSetPath)
        {
        }

        /// <summary>
        /// 从MP4文件中提取指定时间段的内容
        /// </summary>
        /// <param name="inputFile">输入MP4文件路径</param>
        /// <param name="outputFile">输出的片段文件路径</param>
        /// <param name="startTime">开始时间（秒）</param>
        /// <param name="duration">持续时间（秒）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>工具执行结果</returns>
        public Task<ToolResult> ExtractSegmentAsync(
            string inputFile,
            string outputFile,
            double startTime,
            double duration,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(inputFile))
                throw new ArgumentNullException(nameof(inputFile));

            if (string.IsNullOrEmpty(outputFile))
                throw new ArgumentNullException(nameof(outputFile));

            if (startTime < 0)
                throw new ArgumentOutOfRangeException(nameof(startTime), "开始时间不能为负数");

            if (duration <= 0)
                throw new ArgumentOutOfRangeException(nameof(duration), "持续时间必须大于零");

            var argsBuilder = new System.Text.StringBuilder();

            // 添加时间范围参数
            argsBuilder.Append($"-start {startTime} -duration {duration} ");

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

        /// <summary>
        /// 将MP4文件按大小分割为多个片段
        /// </summary>
        /// <param name="inputFile">输入MP4文件路径</param>
        /// <param name="outputPattern">输出文件模式（如"segment_%d.mp4"）</param>
        /// <param name="segmentSizeMB">每个片段的大小（MB）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>工具执行结果</returns>
        public Task<ToolResult> SplitBySizeAsync(
            string inputFile,
            string outputPattern,
            int segmentSizeMB,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(inputFile))
                throw new ArgumentNullException(nameof(inputFile));

            if (string.IsNullOrEmpty(outputPattern))
                throw new ArgumentNullException(nameof(outputPattern));

            if (segmentSizeMB <= 0)
                throw new ArgumentOutOfRangeException(nameof(segmentSizeMB), "片段大小必须大于零");

            var argsBuilder = new System.Text.StringBuilder();

            // 转换为字节（MB到字节）
            long segmentSizeBytes = (long)segmentSizeMB * 1024 * 1024;

            // 添加大小参数
            argsBuilder.Append($"-split-size {segmentSizeBytes} ");

            // 添加输入文件和输出模式
            argsBuilder.Append($"\"{inputFile}\" \"{outputPattern}\"");

            // 使用输出目录作为工作目录
            var outputDir = System.IO.Path.GetDirectoryName(outputPattern);
            if (!string.IsNullOrEmpty(outputDir) && !System.IO.Directory.Exists(outputDir))
            {
                System.IO.Directory.CreateDirectory(outputDir);
            }

            return ExecuteAsync(
                argsBuilder.ToString(),
                cancellationToken,
                outputDir);
        }
    }
}
