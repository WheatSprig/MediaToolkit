using System;
using System.Collections.Generic;
using System.Text;

namespace MediaToolkit.Adapters.FFmpeg
{
    // 定义一个进度事件参数类
    public class ProgressEventArgs : EventArgs
    {
        /// <summary>
        /// 为进度报告事件提供数据。
        /// </summary>
        public ProgressEventArgs(TimeSpan processed, TimeSpan total)
        {
            TotalDuration = total;
            ProcessedDuration = processed;
        }

        /// <summary>
        /// 媒体文件的总时长。
        /// </summary>
        public TimeSpan TotalDuration { get; }

        /// <summary>
        /// 当前已处理的时长。
        /// </summary>
        public TimeSpan ProcessedDuration { get; }

        /// <summary>
        /// 计算出的进度百分比 (从 0.0 到 1.0)。
        /// </summary>
        public double Progress => TotalDuration > TimeSpan.Zero
            ? ProcessedDuration.TotalSeconds / TotalDuration.TotalSeconds
            : 0;
    }
}
