using System;
using System.IO;
using System.Runtime.InteropServices;

namespace MediaToolkit.Core
{
    public static class ToolFinder
    {
        public static string FindExecutablePath(string toolName, string explicitPath = null)
        {
            // 1. 检查用户指定的路径
            if (!string.IsNullOrEmpty(explicitPath) && File.Exists(explicitPath))
            {
                return explicitPath;
            }

            // 2. 检查环境变量 (例如 FFMPEG_PATH)
            var envVarName = $"{toolName.ToUpperInvariant()}_PATH";
            var envPath = Environment.GetEnvironmentVariable(envVarName);
            if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
            {
                return envPath;
            }

            // 3. 在系统 PATH 中搜索
            var systemPath = Environment.GetEnvironmentVariable("PATH");
            if (systemPath != null)
            {
                var exeName = toolName + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : "");
                foreach (var path in systemPath.Split(Path.PathSeparator))
                {
                    var fullPath = Path.Combine(path, exeName);
                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }
                }
            }

            // 如果所有方法都失败，抛出友好异常
            throw new FileNotFoundException(
                $"Could not find '{toolName}' executable. " +
                $"Please add it to your system's PATH, set the '{envVarName}' environment variable, " +
                "or provide the full path explicitly.");
        }
    }
}