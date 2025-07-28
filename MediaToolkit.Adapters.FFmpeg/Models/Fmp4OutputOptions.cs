namespace MediaToolkit.Adapters.FFmpeg.Models
{
    public class Fmp4OutputOptions
    {
        /// <summary>
        /// 输入文件路径。
        /// </summary>
        public string InputFile { get; set; }

        /// <summary>
        /// 输出的单文件 FMP4 路径 (例如 "output.m4s" 或 "output.mp4")。
        /// </summary>
        public string OutputFile { get; set; }

        /// <summary>
        /// 视频流配置。如果为 null，则视频流将复制或不处理。
        /// 对于单文件 FMP4，通常只指定一个视频配置。
        /// </summary>
        public VideoStreamProfile VideoProfile { get; set; }

        /// <summary>
        /// 音频流配置。如果为 null，则音频流将复制或不处理。
        /// </summary>
        public AudioStreamProfile AudioProfile { get; set; }

        /// <summary>
        /// 是否将视频和音频分离到不同的文件中。
        /// 如果为 true，OutputFilePath 将作为视频输出的基础，音频将生成在同目录下的单独文件中。
        /// </summary>
        public bool SeparateAudioVideo { get; set; } = false;

        /// <summary>
        /// 自定义 FFmpeg 选项，例如 `-preset fast -crf 22`。
        /// </summary>
        public string CustomOptions { get; set; }
    }
}