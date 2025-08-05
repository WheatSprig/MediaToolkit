namespace MediaToolkit.Core
{
    /// <summary>
    /// 通用进度报告接口，仅暴露百分比。
    /// UI、日志、进度条统一依赖此接口。
    /// </summary>
    public interface IProgressReport
    {
        /// <summary>
        /// 当前进度，范围 0.0 ~ 1.0
        /// </summary>
        double Progress { get; }
    }
}