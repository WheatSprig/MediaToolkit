using MediaToolkit.Core;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MediaToolkit.Adapters.Bento4
{
    /// <summary>
    /// Bento4中mp4split工具的适配器
    /// 用于将MP4文件分割为初始化片段(init segment)和媒体片段(media segments)
    /// </summary>
    public class Mp4SplitAdapter : Bento4ToolBase
    {
        /// <summary>
        /// 片段文件名模式参数选项
        /// </summary>
        [Flags]
        public enum PatternParameters
        {
            /// <summary>
            /// 包含轨道ID (I)
            /// </summary>
            TrackId = 1 << 0,
            /// <summary>
            /// 包含片段编号 (N)
            /// </summary>
            SegmentNumber = 1 << 1,
            /// <summary>
            /// 默认参数 (I + N)
            /// </summary>
            Default = TrackId | SegmentNumber
        }

        /// <summary>
        /// 初始化mp4split工具适配器
        /// </summary>
        /// <param name="toolSetPath">Bento4工具集目录（可选）</param>
        public Mp4SplitAdapter(string toolSetPath = null)
            : base("mp4split", toolSetPath)
        {
        }

        /// <summary>
        /// 将MP4文件分割为初始化片段和媒体片段
        /// </summary>
        /// <param name="inputFile">输入MP4文件路径</param>
        /// <param name="initSegment">初始化片段输出路径（默认：init.mp4）</param>
        /// <param name="mediaSegmentPattern">媒体片段文件名模式（默认：segment-%llu.%04llu.m4s）</param>
        /// <param name="initOnly">是否只输出初始化片段（不输出媒体片段）</param>
        /// <param name="startNumber">片段起始编号（默认：1）</param>
        /// <param name="patternParams">文件名模式包含的参数</param>
        /// <param name="trackIds">要包含的轨道ID（逗号分隔字符串，如"1,2"；为null则包含所有轨道）</param>
        /// <param name="audioOnly">是否只包含音频轨道</param>
        /// <param name="videoOnly">是否只包含视频轨道</param>
        /// <param name="verbose">是否输出详细信息</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>工具执行结果</returns>
        public Task<ToolResult> SplitAsync(
            string inputFile,
            string initSegment = null,
            string mediaSegmentPattern = null,
            bool initOnly = false,
            int startNumber = 1,
            PatternParameters patternParams = PatternParameters.Default,
            string trackIds = null,
            bool audioOnly = false,
            bool videoOnly = false,
            bool verbose = false,
            CancellationToken cancellationToken = default)
        {
            // 验证核心参数
            if (string.IsNullOrEmpty(inputFile))
                throw new ArgumentNullException(nameof(inputFile));
            
            if (!File.Exists(inputFile))
                throw new FileNotFoundException("输入文件不存在", inputFile);
            
            if (startNumber < 1)
                throw new ArgumentOutOfRangeException(nameof(startNumber), "起始编号必须大于等于1");
            
            if (audioOnly && videoOnly)
                throw new ArgumentException("不能同时指定只包含音频和只包含视频", nameof(audioOnly));

            // 确定工作目录（优先使用媒体片段目录，其次使用初始化片段目录）
            string workingDir = null;
            if (!string.IsNullOrEmpty(mediaSegmentPattern))
            {
                workingDir = Path.GetDirectoryName(mediaSegmentPattern);
            }
            else if (!string.IsNullOrEmpty(initSegment))
            {
                workingDir = Path.GetDirectoryName(initSegment);
            }

            // 创建工作目录（如果需要）
            if (!string.IsNullOrEmpty(workingDir) && !Directory.Exists(workingDir))
            {
                Directory.CreateDirectory(workingDir);
            }

            // 构建命令参数
            var argsBuilder = new ArgumentBuilder();

            // 详细输出
            argsBuilder.Append("--verbose", verbose);

            // 初始化片段路径
            if (!string.IsNullOrEmpty(initSegment))
            {
                argsBuilder.Append("--init-segment", initSegment);
            }

            // 只输出初始化片段
            argsBuilder.Append("--init-only", initOnly);

            // 媒体片段文件名模式
            if (!string.IsNullOrEmpty(mediaSegmentPattern))
            {
                argsBuilder.Append("--media-segment", mediaSegmentPattern);
            }

            // 起始编号
            if (startNumber != 1) // 只在非默认值时添加
            {
                argsBuilder.Append("--start-number", startNumber.ToString());
            }

            // 文件名模式参数
            if (patternParams != PatternParameters.Default)
            {
                var paramsStr = string.Empty;
                if (patternParams.HasFlag(PatternParameters.TrackId))
                    paramsStr += "I";
                if (patternParams.HasFlag(PatternParameters.SegmentNumber))
                    paramsStr += "N";
                
                if (!string.IsNullOrEmpty(paramsStr))
                {
                    argsBuilder.Append("--pattern-parameters", paramsStr);
                }
            }

            // 轨道ID筛选
            if (!string.IsNullOrEmpty(trackIds))
            {
                argsBuilder.Append("--track-id", trackIds);
            }

            // 音频/视频筛选
            argsBuilder.Append("--audio", audioOnly);
            argsBuilder.Append("--video", videoOnly);

            // 输入文件
            argsBuilder.Append(inputFile);

            // 执行命令
            return ExecuteAsync(
                argsBuilder.ToString(),
                cancellationToken,
                workingDir);
        }
    }
}
