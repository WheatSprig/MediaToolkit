using System;

namespace MediaToolkit.Adapters.FFmpeg.Models
{
    public class VideoStreamInfo
    {
        public string Codec { get; internal set; }
        public string Resolution { get; internal set; }
        public double Fps { get; internal set; }

        public double BitrateKbps { get; set; }   // 比特率，单位为 Kbps
        public string BitrateMode { get; set; }   // "CBR", "VBR" 或 null
    }

    public class AudioStreamInfo
    {
        public string Codec { get; internal set; }
        public string SampleRate { get; internal set; }
        public string Channels { get; internal set; }

        public double? BitrateKbps { get; set; }  // 音频有的文件没给码率，可为 null
        public string BitrateMode { get; set; }
    }

    public class Metadata
    {
        public TimeSpan Duration { get; internal set; }
        public VideoStreamInfo VideoStream { get; internal set; }
        public AudioStreamInfo AudioStream { get; internal set; }
        
        // 媒体的总码率
        public double OverallBitrateKbps { get; set; }
    }
}