namespace MediaToolkit.Adapters.FFmpeg.Models
{
    public class VideoStreamProfile
    {
        public string Resolution { get; set; } // e.g., "1920x1080" or just "1080" for height
        public string Bitrate { get; set; } // e.g., "5M" or "5000k"
        public string MaxRate { get; set; } // e.g., "6M"
        public string BufferSize { get; set; } // e.g., "10M"
        public int? Fps { get; set; } // e.g., 30
    }
}
