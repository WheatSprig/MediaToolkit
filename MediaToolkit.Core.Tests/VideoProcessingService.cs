// 业务逻辑项目
using MediaToolkit.Core;
public class VideoProcessingService
{
    private readonly IMediaToolAdapter _toolAdapter;

    public VideoProcessingService(IMediaToolAdapter toolAdapter)
    {
        _toolAdapter = toolAdapter;
    }

    public async Task CreateThumbnailAsync(string videoPath, string thumbPath)
    {
        // 从视频第5秒截取一帧作为缩略图
        var arguments = $"-ss 00:00:05 -i \"{videoPath}\" -frames:v 1 \"{thumbPath}\"";
        var result = await _toolAdapter.ExecuteAsync(arguments);
        if (result.ExitCode != 0)
        {
            throw new Exception("Failed to create thumbnail");
        }
    }
}