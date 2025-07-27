using MediaToolkit.Core;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MediaToolkit.Adapters.FFmpeg
{
    public class HardwareEncoderSelector
    {
        public string SelectedVideoEncoder { get; private set; } = "libx264"; // 默认使用软件编码器
        public string Reason { get; private set; }

        private readonly FFmpegAdapter _adapter;

        public HardwareEncoderSelector(FFmpegAdapter adapter)
        {
            _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        public async Task DetectBestEncoderAsync(CancellationToken ct = default)
        {
            // 检测 FFmpeg 支持的编码器
            ToolResult result = await _adapter.ExecuteAsync("-hide_banner -encoders", ct);
            string encoderList = result.Output + result.Error;
            string nvencFailureReason = string.Empty;

            if (Supports(encoderList, "h264_nvenc"))
            {
                try
                {
                    // 将可能抛出异常的代码放入 try 块中
                    await _adapter.ExecuteAsync("-f lavfi -i testsrc=duration=1:size=1280x720:rate=30 -c:v h264_nvenc -f null -", ct);

                    // 如果代码能执行到这里，说明上面的命令成功了（没有抛出异常）
                    SelectedVideoEncoder = "h264_nvenc";
                    Reason = "使用 NVIDIA NVENC 硬编";
                }
                catch (Exception ex) // 捕获所有可能的异常
                {
                    // 如果执行失败，代码会跳转到这里
                    // 记录下失败的原因，这对于调试非常有用
                    nvencFailureReason = $"NVENC 可用但测试失败。错误: {ex.Message}";
                    Console.WriteLine(nvencFailureReason); // 在控制台打印错误，方便调试
                }
            }
            else if (Supports(encoderList, "h264_qsv"))
            {
                SelectedVideoEncoder = "h264_qsv";
                Reason = "使用 Intel QSV 硬编";
            }
            else if (Supports(encoderList, "h264_amf"))
            {
                SelectedVideoEncoder = "h264_amf";
                Reason = "使用 AMD AMF 硬编";
            }
            else
            {
                SelectedVideoEncoder = "libx24";
                // 如果之前 NVENC 失败了，就把失败原因附加到最终理由上
                Reason = "未检测到可用的硬件编码器，使用软件编码。"
                         + (!string.IsNullOrEmpty(nvencFailureReason) ? $" ({nvencFailureReason})" : "");
            }
        }

        private bool Supports(string encoderList, string encoderName)
        {
            return Regex.IsMatch(encoderList, @"\s+" + Regex.Escape(encoderName) + @"\s", RegexOptions.IgnoreCase);
        }
    }
}
