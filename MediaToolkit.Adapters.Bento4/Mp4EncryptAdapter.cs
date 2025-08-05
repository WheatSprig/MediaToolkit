using MediaToolkit.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MediaToolkit.Adapters.Bento4
{
    /// <summary>
    /// Bento4中mp4encrypt工具的适配器
    /// 用于加密MP4文件
    /// </summary>
    public class Mp4EncryptAdapter : Bento4ToolBase
    {
        /// <summary>
        /// 加密方法（工具要求的必须参数）
        /// </summary>
        public enum EncryptionMethod
        {
            /// <summary>
            /// OMA-PDCF-CBC加密
            /// </summary>
            [System.ComponentModel.Description("OMA-PDCF-CBC")]
            OmaPdcfCbc,
            /// <summary>
            /// OMA-PDCF-CTR加密
            /// </summary>
            [System.ComponentModel.Description("OMA-PDCF-CTR")]
            OmaPdcfCtr,
            /// <summary>
            /// MARLIN-IPMP-ACBC加密
            /// </summary>
            [System.ComponentModel.Description("MARLIN-IPMP-ACBC")]
            MarlinIpmpAcbc,
            /// <summary>
            /// MARLIN-IPMP-ACGK加密
            /// </summary>
            [System.ComponentModel.Description("MARLIN-IPMP-ACGK")]
            MarlinIpmpAcgk,
            /// <summary>
            /// ISMA-IAEC加密
            /// </summary>
            [System.ComponentModel.Description("ISMA-IAEC")]
            IsmaIaec,
            /// <summary>
            /// PIFF-CBC加密
            /// </summary>
            [System.ComponentModel.Description("PIFF-CBC")]
            PiffCbc,
            /// <summary>
            /// PIFF-CTR加密
            /// </summary>
            [System.ComponentModel.Description("PIFF-CTR")]
            PiffCtr,
            /// <summary>
            /// MPEG-CENC加密（通用加密标准）
            /// </summary>
            [System.ComponentModel.Description("MPEG-CENC")]
            MpegCenc,
            /// <summary>
            /// MPEG-CBC1加密
            /// </summary>
            [System.ComponentModel.Description("MPEG-CBC1")]
            MpegCbc1,
            /// <summary>
            /// MPEG-CENS加密
            /// </summary>
            [System.ComponentModel.Description("MPEG-CENS")]
            MpegCens,
            /// <summary>
            /// MPEG-CBCS加密
            /// </summary>
            [System.ComponentModel.Description("MPEG-CBCS")]
            MpegCbcs
        }

        /// <summary>
        /// 轨道密钥信息
        /// </summary>
        public class TrackKey
        {
            /// <summary>
            /// 轨道ID（0表示组密钥，仅MARLIN-IPMP-ACGK方法使用）
            /// </summary>
            public int TrackId { get; set; }

            /// <summary>
            /// 128位密钥（32个十六进制字符），或使用"random"生成随机密钥
            /// </summary>
            public string Key { get; set; }

            /// <summary>
            /// 初始化向量（IV），16或32个十六进制字符，或使用"random"生成随机值
            /// 具体长度取决于加密方法
            /// </summary>
            public string Iv { get; set; }
        }

        /// <summary>
        /// PSSH原子信息
        /// </summary>
        public class PsshEntry
        {
            /// <summary>
            /// 系统ID
            /// </summary>
            public string SystemId { get; set; }

            /// <summary>
            /// 负载文件路径（可为空）
            /// </summary>
            public string PayloadFile { get; set; }
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
        /// <param name="method">加密方法（必须指定）</param>
        /// <param name="trackKeys">轨道密钥信息（至少需要一个）</param>
        /// <param name="showProgress">是否显示进度</param>
        /// <param name="fragmentsInfo">片段信息文件路径（可选）</param>
        /// <param name="strict">是否在警告时失败</param>
        /// <param name="properties">轨道属性（可选，格式：轨道ID:名称:值）</param>
        /// <param name="globalOptions">全局选项（可选，格式：名称:值）</param>
        /// <param name="psshEntries">PSSH原子信息（可选）</param>
        /// <param name="psshV1Entries">版本1的PSSH原子信息（可选）</param>
        /// <param name="kmsUri">KMS URI（仅ISMA-IAEC方法使用）</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>工具执行结果</returns>
        public Task<ToolResult> EncryptAsync(
            string inputFile,
            string outputFile,
            EncryptionMethod method,
            IEnumerable<TrackKey> trackKeys,
            bool showProgress = false,
            string fragmentsInfo = null,
            bool strict = false,
            IEnumerable<string> properties = null,
            IEnumerable<string> globalOptions = null,
            IEnumerable<PsshEntry> psshEntries = null,
            IEnumerable<PsshEntry> psshV1Entries = null,
            string kmsUri = null,
            CancellationToken cancellationToken = default)
        {
            // 验证核心参数
            if (string.IsNullOrEmpty(inputFile))
                throw new ArgumentNullException(nameof(inputFile));
            
            if (!File.Exists(inputFile))
                throw new FileNotFoundException("输入文件不存在", inputFile);
            
            if (string.IsNullOrEmpty(outputFile))
                throw new ArgumentNullException(nameof(outputFile));
            
            if (trackKeys == null || !trackKeys.GetEnumerator().MoveNext())
                throw new ArgumentException("至少需要指定一个轨道密钥", nameof(trackKeys));

            // 验证轨道密钥
            foreach (var key in trackKeys)
            {
                if (key.TrackId < 0)
                    throw new ArgumentException("轨道ID不能为负数", nameof(trackKeys));
                
                if (string.IsNullOrEmpty(key.Key) || 
                    !(key.Key.Equals("random", StringComparison.OrdinalIgnoreCase) || 
                      key.Key.Length == 32))
                {
                    throw new ArgumentException($"轨道{key.TrackId}的密钥必须是'random'或32个十六进制字符", nameof(trackKeys));
                }

                if (string.IsNullOrEmpty(key.Iv) || 
                    !(key.Iv.Equals("random", StringComparison.OrdinalIgnoreCase) || 
                      key.Iv.Length == 16 || key.Iv.Length == 32))
                {
                    throw new ArgumentException($"轨道{key.TrackId}的IV必须是'random'或16/32个十六进制字符", nameof(trackKeys));
                }
            }

            // 创建输出目录
            var outputDir = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // 构建命令参数
            var argsBuilder = new ArgumentBuilder();

            // 添加加密方法（必须参数）
            var methodName = GetEnumDescription(method);
            argsBuilder.Append("--method", methodName);

            // 显示进度
            argsBuilder.Append("--show-progress", showProgress);

            // 片段信息文件
            if (!string.IsNullOrEmpty(fragmentsInfo))
            {
                argsBuilder.Append("--fragments-info", fragmentsInfo);
            }

            // 轨道密钥
            foreach (var trackKey in trackKeys)
            {
                argsBuilder.Append("--key", $"{trackKey.TrackId}:{trackKey.Key}:{trackKey.Iv}");
            }

            // 严格模式
            argsBuilder.Append("--strict", strict);

            // 轨道属性
            if (properties != null)
            {
                foreach (var prop in properties)
                {
                    argsBuilder.Append("--property", prop);
                }
            }

            // 全局选项
            if (globalOptions != null)
            {
                foreach (var opt in globalOptions)
                {
                    argsBuilder.Append("--global-option", opt);
                }
            }

            // PSSH原子
            if (psshEntries != null)
            {
                foreach (var pssh in psshEntries)
                {
                    if (!string.IsNullOrEmpty(pssh.SystemId))
                    {
                        argsBuilder.Append("--pssh", $"{pssh.SystemId}:{pssh.PayloadFile ?? ""}");
                    }
                }
            }

            // 版本1的PSSH原子
            if (psshV1Entries != null)
            {
                foreach (var pssh in psshV1Entries)
                {
                    if (!string.IsNullOrEmpty(pssh.SystemId))
                    {
                        argsBuilder.Append("--pssh-v1", $"{pssh.SystemId}:{pssh.PayloadFile ?? ""}");
                    }
                }
            }

            // KMS URI（仅ISMA-IAEC方法使用）
            if (!string.IsNullOrEmpty(kmsUri))
            {
                argsBuilder.Append("--kms-uri", kmsUri);
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
        /// 获取枚举值的描述信息
        /// </summary>
        private string GetEnumDescription(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attributes = (System.ComponentModel.DescriptionAttribute[])field.GetCustomAttributes(
                typeof(System.ComponentModel.DescriptionAttribute), false);
            return attributes.Length > 0 ? attributes[0].Description : value.ToString();
        }
    }
}
