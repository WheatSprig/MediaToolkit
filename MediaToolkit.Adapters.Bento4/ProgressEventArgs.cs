using MediaToolkit.Core;
using System;

namespace MediaToolkit.Adapters.Bento4
{
    /// <summary>
    /// Bento4工具进度事件参数
    /// </summary>
    public class ProgressEventArgs : EventArgs, IProgressReport
    {
        /// <summary>
        /// 初始化进度事件参数
        /// </summary>
        /// <param name="processed">已处理时长</param>
        /// <param name="total">总时长</param>
        public ProgressEventArgs(TimeSpan processed, TimeSpan total)
        {
            ProcessedDuration = processed;
            TotalDuration = total;
        }

        /// <summary>
        /// 已处理的时长
        /// </summary>
        public TimeSpan ProcessedDuration { get; }

        /// <summary>
        /// 总时长
        /// </summary>
        public TimeSpan TotalDuration { get; }

        /// <summary>
        /// 进度百分比（0.0-1.0）
        /// </summary>
        public double Progress => TotalDuration > TimeSpan.Zero
            ? ProcessedDuration.TotalSeconds / TotalDuration.TotalSeconds
            : 0;
    }

    /// <summary>
    /// Bento4片段计数进度事件参数（适用于mp4split等按片段计数的工具）
    /// </summary>
    public class SegmentProgressEventArgs : EventArgs, IProgressReport
    {
        /// <summary>
        /// 初始化片段进度事件参数
        /// </summary>
        /// <param name="currentSegment">当前片段编号</param>
        /// <param name="totalSegments">总片段数</param>
        public SegmentProgressEventArgs(int currentSegment, int totalSegments)
        {
            CurrentSegment = currentSegment;
            TotalSegments = totalSegments;
        }

        /// <summary>
        /// 当前片段编号
        /// </summary>
        public int CurrentSegment { get; }

        /// <summary>
        /// 总片段数
        /// </summary>
        public int TotalSegments { get; }

        /// <summary>
        /// 进度百分比（0.0-1.0）
        /// </summary>
        public double Progress => TotalSegments > 0
            ? (double)CurrentSegment / TotalSegments
            : 0;
    }
}
