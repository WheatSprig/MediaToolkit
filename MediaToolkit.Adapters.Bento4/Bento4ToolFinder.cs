using System;
using System.IO;
using System.Runtime.InteropServices;
using MediaToolkit.Core;

namespace MediaToolkit.Adapters.Bento4
{
    /// <summary>
    /// 提供Bento4工具集的路径查找功能
    /// </summary>
    public static class Bento4ToolFinder
    {
        /// <summary>
        /// 查找Bento4工具集中具体工具的可执行文件路径
        /// </summary>
        /// <param name="toolName">工具名称（如mp4info）</param>
        /// <param name="explicitToolSetPath">显式指定的Bento4工具集目录（可选）</param>
        /// <returns>工具可执行文件的完整路径</returns>
        public static string FindToolExecutable(string toolName, string explicitToolSetPath = null)
        {
            // 1. 查找Bento4工具集根目录
            var toolSetPath = FindToolSetPath(explicitToolSetPath);

            // 2. 构建具体工具的可执行文件路径
            var exeExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "";
            var toolExecutable = Path.Combine(toolSetPath, $"{toolName}{exeExtension}");

            // 3. 验证文件是否存在
            if (!File.Exists(toolExecutable))
            {
                throw new FileNotFoundException(
                    $"Bento4工具 '{toolName}' 在工具集目录中未找到。",
                    toolExecutable);
            }

            return toolExecutable;
        }

        /// <summary>
        /// 查找Bento4工具集的根目录
        /// </summary>
        /// <param name="explicitPath">显式指定的路径（可选）</param>
        /// <returns>工具集根目录路径</returns>
        public static string FindToolSetPath(string explicitPath = null)
        {
            // 1. 检查显式指定的路径
            if (!string.IsNullOrEmpty(explicitPath) && Directory.Exists(explicitPath))
            {
                return explicitPath;
            }

            // 2. 检查环境变量 BENTO4_PATH
            var envPath = Environment.GetEnvironmentVariable("BENTO4_PATH");
            if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
            {
                return envPath;
            }

            // 3. 在系统PATH中查找任意Bento4工具，反推工具集目录
            var testTool = "mp4info" + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "");
            var systemPath = Environment.GetEnvironmentVariable("PATH");

            if (systemPath != null)
            {
                foreach (var path in systemPath.Split(Path.PathSeparator))
                {
                    var testPath = Path.Combine(path, testTool);
                    if (File.Exists(testPath))
                    {
                        return Path.GetDirectoryName(testPath);
                    }
                }
            }

            // 所有查找方式失败
            throw new DirectoryNotFoundException(
                "无法找到Bento4工具集目录。请：\n" +
                "1. 将BENTO4_PATH环境变量设置为工具集目录\n" +
                "2. 将工具集目录添加到系统PATH\n" +
                "3. 显式指定工具集目录");
        }
    }
}
