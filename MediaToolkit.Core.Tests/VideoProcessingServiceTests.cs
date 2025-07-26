using MediaToolkit.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace MediaToolkit.Core.Tests
{
    [TestClass]
    public class VideoProcessingServiceTests
    {
        [TestMethod]
        public async Task CreateThumbnailAsync_CallsAdapterWithCorrectArguments()
        {
            // Arrange
            var mockAdapter = new Mock<IMediaToolAdapter>();
            var service = new VideoProcessingService(mockAdapter.Object);
            var videoFile = "C:\\videos\\movie.mp4";
            var thumbFile = "C:\\thumbs\\thumb.jpg";

            // 设置模拟：当 ExecuteAsync 被调用时，返回一个成功的 ToolResult
            mockAdapter
                .Setup(a => a.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>(),null))
                .ReturnsAsync(new ToolResult(0, "Success", ""));

            // Act
            await service.CreateThumbnailAsync(videoFile, thumbFile);

            // Assert
            // 验证 ExecuteAsync 方法是否被调用过，并且其参数是否包含了我们期望的关键部分
            mockAdapter.Verify(a => a.ExecuteAsync(
                    It.Is<string>(args => args.Contains("-ss 00:00:05") && args.Contains(videoFile)),
                    It.IsAny<CancellationToken>(),null),
                Times.Once);
        }
    }
}
