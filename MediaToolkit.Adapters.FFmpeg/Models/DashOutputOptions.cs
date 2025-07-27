using System.Collections.Generic;

namespace MediaToolkit.Adapters.FFmpeg.Models
{
    public class DashOutputOptions
    {
        public string InputFile { get; set; }
        public string OutputDirectory { get; set; }
        public string ManifestFileName { get; set; } = "manifest.mpd";
        public List<VideoStreamProfile> VideoProfiles { get; set; } = new List<VideoStreamProfile>();
        public AudioStreamProfile AudioProfile { get; set; }
        public int SegmentDuration { get; set; } = 4; // in seconds
    }
}
