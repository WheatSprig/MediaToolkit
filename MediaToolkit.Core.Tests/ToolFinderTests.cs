using Microsoft.VisualStudio.TestTools.UnitTesting;
using MediaToolkit.Core;
using System.IO;
using System;

namespace MediaToolkit.Core.Tests
{
    [TestClass]
    public class ToolFinderTests
    {
        [TestMethod]
        public void FindExecutablePath_WhenExplicitPathProvided_ReturnsExplicitPath()
        {
            // Arrange
            var dummyFile = Path.GetTempFileName();
            try
            {
                // Act
                var result = ToolFinder.FindExecutablePath("anytool", dummyFile);

                // Assert
                Assert.AreEqual(dummyFile, result);
            }
            finally
            {
                File.Delete(dummyFile);
            }
        }

        [TestMethod]
        public void FindExecutablePath_WhenEnvVarIsSet_ReturnsPathFromEnvVar()
        {
            var dummyFile = Path.GetTempFileName();
            var envVar = "TESTTOOL123_PATH";
            var toolName = "testtool123";
            try
            {
                // Arrange
                Environment.SetEnvironmentVariable(envVar, dummyFile);

                // Act
                var result = ToolFinder.FindExecutablePath(toolName);

                // Assert
                Assert.AreEqual(dummyFile, result);
            }
            finally
            {
                File.Delete(dummyFile);
                Environment.SetEnvironmentVariable(envVar, null); // 清理环境变量
            }
        }

        [TestMethod]
        public void FindExecutablePath_WhenNotFound_ThrowsFileNotFoundException()
        {
            // Arrange
            var nonexistentTool = Guid.NewGuid().ToString("N");

            // Act & Assert
            Assert.ThrowsException<FileNotFoundException>(() =>
            {
                ToolFinder.FindExecutablePath(nonexistentTool);
            });
        }
    }
}
