using System;
using System.Threading;
using System.Threading.Tasks;
using MediaToolkit.Core;

namespace MediaToolkit.Adapters.Bento4
{
    /// <summary>
    /// Bento4中mp4fragment工具的适配器（完全匹配官方参数）
    /// </summary>
    public class Mp4FragmentAdapter : Bento4ToolBase
    {
        /// <summary>
        /// I-Frame同步模式
        /// </summary>
        public enum ForceIFrameSyncMode
        {
            None,
            Auto,
            All
        }

        /// <summary>
        /// 初始化mp4fragment工具适配器
        /// </summary>
        public Mp4FragmentAdapter(string toolSetPath = null)
            : base("mp4fragment", toolSetPath)
        {
        }

        /// <summary>
        /// 片段化MP4文件（严格遵循官方参数规范）
        /// </summary>
        /// <param name="inputFile">输入文件路径</param>
        /// <param name="outputFile">输出文件路径</param>
        /// <param name="fragmentDurationMs">片段持续时间（毫秒，默认自动）</param>
        /// <param name="timescale">时间尺度（例如10000000用于Smooth Streaming）</param>
        /// <param name="trackFilter">轨道过滤（轨道ID、'audio'、'video'或'subtitles'）</param>
        /// <param name="forceIFrameSync">I-Frame同步模式</param>
        /// <param name="createIndex">是否创建片段索引</param>
        /// <param name="trimExcess">是否修剪过长轨道的多余媒体</param>
        /// <param name="noTfdt">不添加tfdt boxes（兼容旧版Smooth Streaming客户端）</param>
        /// <param name="tfdtStartTime">第一个tfdt时间戳（秒）</param>
        /// <param name="sequenceNumberStart">起始序列号（默认1）</param>
        /// <param name="copyUdta">复制moov/udta原子</param>
        /// <param name="noZeroElst">不将最后一个编辑列表项设置为0时长</param>
        /// <param name="trunVersionZero">使用trun box版本0（默认版本1）</param>
        /// <param name="verbosity">详细程度（0-3，默认0）</param>
        /// <param name="debug">启用调试信息</param>
        /// <param name="quiet">不输出通知消息</param>
        /// <param name="cancellationToken">取消令牌</param>
        public Task<ToolResult> FragmentAsync(
            string inputFile,
            string outputFile,
            int? fragmentDurationMs = null,
            int? timescale = null,
            string trackFilter = null,
            ForceIFrameSyncMode forceIFrameSync = ForceIFrameSyncMode.None,
            bool createIndex = false,
            bool trimExcess = false,
            bool noTfdt = false,
            double? tfdtStartTime = null,
            int? sequenceNumberStart = null,
            bool copyUdta = false,
            bool noZeroElst = false,
            bool trunVersionZero = false,
            int verbosity = 0,
            bool debug = false,
            bool quiet = false,
            CancellationToken cancellationToken = default)
        {
            // 参数验证
            if (string.IsNullOrEmpty(inputFile))
                throw new ArgumentNullException(nameof(inputFile));
            if (string.IsNullOrEmpty(outputFile))
                throw new ArgumentNullException(nameof(outputFile));
            if (!System.IO.File.Exists(inputFile))
                throw new System.IO.FileNotFoundException("输入文件不存在", inputFile);
            if (verbosity < 0 || verbosity > 3)
                throw new ArgumentOutOfRangeException(nameof(verbosity), "详细程度必须在0-3之间");
            if (fragmentDurationMs.HasValue && fragmentDurationMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(fragmentDurationMs), "片段持续时间必须大于0");
            if (timescale.HasValue && timescale <= 0)
                throw new ArgumentOutOfRangeException(nameof(timescale), "时间尺度必须大于0");
            if (sequenceNumberStart.HasValue && sequenceNumberStart <= 0)
                throw new ArgumentOutOfRangeException(nameof(sequenceNumberStart), "起始序列号必须大于0");

            // 为路径添加双引号，处理路径中的特殊字符和空格
            string quotedInput = PathHelper.AddQuotes(inputFile);
            string quotedOutput = PathHelper.AddQuotes(outputFile);

            var argsBuilder = new System.Text.StringBuilder();

            // 通用参数
            if (verbosity > 0)
                argsBuilder.Append($"--verbosity {verbosity} ");
            if (debug)
                argsBuilder.Append("--debug ");
            if (quiet)
                argsBuilder.Append("--quiet ");

            // 片段化核心参数
            if (fragmentDurationMs.HasValue)
                argsBuilder.Append($"--fragment-duration {fragmentDurationMs} ");  // 单位：毫秒
            if (timescale.HasValue)
                argsBuilder.Append($"--timescale {timescale} ");
            if (!string.IsNullOrEmpty(trackFilter))
                argsBuilder.Append($"--track {trackFilter} ");
            if (createIndex)
                argsBuilder.Append("--index ");
            if (trimExcess)
                argsBuilder.Append("--trim ");
            if (noTfdt)
                argsBuilder.Append("--no-tfdt ");
            if (tfdtStartTime.HasValue)
                argsBuilder.Append($"--tfdt-start {tfdtStartTime} ");
            if (sequenceNumberStart.HasValue)
                argsBuilder.Append($"--sequence-number-start {sequenceNumberStart} ");
            if (forceIFrameSync != ForceIFrameSyncMode.None)
                argsBuilder.Append($"--force-i-frame-sync {forceIFrameSync.ToString().ToLowerInvariant()} ");
            if (copyUdta)
                argsBuilder.Append("--copy-udta ");
            if (noZeroElst)
                argsBuilder.Append("--no-zero-elst ");
            if (trunVersionZero)
                argsBuilder.Append("--trun-version-zero ");

            // 输入输出文件（使用带引号的路径）
            argsBuilder.Append($"{quotedInput} {quotedOutput}");

            string fullCommand = $"{ExecutablePath} {argsBuilder.ToString()}";
            Console.WriteLine($"  [调试] 执行命令: {fullCommand}");

            // 工作目录处理
            var workingDir = System.IO.Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(workingDir) && !System.IO.Directory.Exists(workingDir))
            {
                System.IO.Directory.CreateDirectory(workingDir);
            }

            return ExecuteAsync(
                argsBuilder.ToString(),
                cancellationToken,
                workingDir);
        }

        /// <summary>
        /// 生成适合HLS的片段化MP4（预配置参数）
        /// </summary>
        /// <param name="inputFile">输入文件</param>
        /// <param name="outputFile">输出文件</param>
        /// <param name="fragmentDurationMs">片段持续时间（毫秒，推荐2000-10000）</param>
        public Task<ToolResult> FragmentForHlsAsync(
            string inputFile,
            string outputFile,
            int fragmentDurationMs = 2000, // 2秒片段（HLS推荐）
            CancellationToken cancellationToken = default)
        {
            return FragmentAsync(
                inputFile,
                outputFile,
                fragmentDurationMs: fragmentDurationMs,
                timescale: 90000, // HLS常用时间尺度
                createIndex: true, // 创建索引便于流式传输
                trimExcess: true,
                forceIFrameSync: ForceIFrameSyncMode.Auto,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 生成适合Smooth Streaming的片段化MP4
        /// </summary>
        public Task<ToolResult> FragmentForSmoothStreamingAsync(
            string inputFile,
            string outputFile,
            int fragmentDurationMs = 2000,
            CancellationToken cancellationToken = default)
        {
            return FragmentAsync(
                inputFile,
                outputFile,
                fragmentDurationMs: fragmentDurationMs,
                timescale: 10000000, // Smooth Streaming推荐时间尺度
                noTfdt: false,
                trunVersionZero: true,
                cancellationToken: cancellationToken);
        }
    }
}
