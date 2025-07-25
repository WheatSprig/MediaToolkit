using System;

namespace MediaToolkit.Adapters.FFmpeg
{
    public class VideoStreamInfo
    {
        public string Codec { get; internal set; }
        public string Resolution { get; internal set; }
        public double Fps { get; internal set; }
    }

    public class AudioStreamInfo
    {
        public string Codec { get; internal set; }
        public string SampleRate { get; internal set; }
        public string Channels { get; internal set; }
    }

    public class Metadata
    {
        public TimeSpan Duration { get; internal set; }
        public VideoStreamInfo VideoStream { get; internal set; }
        public AudioStreamInfo AudioStream { get; internal set; }
    }
}