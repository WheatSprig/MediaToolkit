namespace MediaToolkit.Adapters.FFmpeg.Models
{
    /// <summary>
    /// 为生成单文件、碎片化 MP4 (fMP4) 流媒体提供配置选项。
    /// </summary>
    public class FragmentedMp4Options
    {
        /// <summary>
        /// 获取或设置输入文件的路径。
        /// </summary>
        public string InputFile { get; set; }

        /// <summary>
        /// 获取或设置输出文件的路径 (例如 "output.m4s" 或 "output.mp4")。
        /// </summary>
        public string OutputFile { get; set; }

        /// <summary>
        /// 获取或设置视频编码器。如果为 null，将使用默认值 "libx264"。
        /// </summary>
        public string VideoCodec { get; set; } = "libx264";

        /// <summary>
        /// 获取或设置编码预设 (例如 "fast", "medium", "slow")。
        /// </summary>
        public string Preset { get; set; } = "fast";

        /// <summary>
        /// 获取或设置恒定速率因子 (Constant Rate Factor)。值越小，质量越高。
        /// 对于 libx264，典型范围是 18-28。
        /// </summary>
        public int? Crf { get; set; } = 22;

        /// <summary>
        /// 视频编码的 Profile，例如 baseline, main, high 等。
        /// </summary>
        public string Profile { get; set; }

        /// <summary>
        /// 视频编码的 Level，例如 3.1, 4.0, 4.2 等。
        /// </summary>
        public string Level { get; set; }

        /// <summary>
        /// 获取或设置最大视频码率 (例如 "5000k")，用于控制码率峰值。
        /// </summary>
        public string MaxBitrate { get; set; }

        /// <summary>
        /// 获取或设置最小视频码率 (例如 "1000k")。
        /// </summary>
        public string MinBitrate { get; set; }

        /// <summary>
        /// 获取或设置缓冲区大小 (例如 "10000k")，影响码率控制平滑度。
        /// </summary>
        public string BufferSize { get; set; }

        /// <summary>
        /// 获取或设置输出视频的分辨率 (例如 "1920x1080")。
        /// 如果为 null，将保持原始分辨率。
        /// </summary>
        public string Resolution { get; set; }

        /// <summary>
        /// 可选的输出帧率（如 "60"），用于升帧处理。如果为 null，则不更改帧率。
        /// 若为流式用途（如直播或点播首播），建议维持原始帧率，兼容性更好（即设置为null）。
        /// </summary>
        public int? OutputFps { get; set; }

        /// <summary>
        /// 获取或设置音频编码器。如果为 null，将使用默认值 "aac"。
        /// </summary>
        public string AudioCodec { get; set; } = "aac";

        /// <summary>
        /// 获取或设置音频码率 (例如 "128k")。
        /// </summary>
        public string AudioBitrate { get; set; } = "128k";

        /// <summary>
        /// 获取或设置音频采样率 (例如 "44100" 或 "48000")。
        /// </summary>
        public string AudioSampleRate { get; set; }

        /// <summary>
        /// 获取或设置音频声道数（例如 1=单声道，2=立体声）。
        /// </summary>
        public int? AudioChannels { get; set; }

        /// <summary>
        /// 设置关键帧间隔 (Group of Pictures size)。
        /// 对于流媒体，建议设置为帧率的1-2倍。例如，24fps视频可设为48。
        /// </summary>
        public int? GopSize { get; set; }

        /// <summary>
        /// 禁止在GOP内因为场景切换而插入额外的关键帧。
        /// 设置为0可禁用场景检测，确保严格的固定GOP。
        /// </summary>
        public int? SceneCutThreshold { get; set; }

        /// <summary>
        /// 是否将视频轨道分离（不包含视频）。
        /// </summary>
        public bool OmitVideo { get; set; } = false;

        /// <summary>
        /// 是否将音频轨道分离（不包含音频）。
        /// </summary>
        public bool OmitAudio { get; set; } = false;
    }
}