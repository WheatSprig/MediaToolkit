using System;
using System.IO;

namespace MediaToolkit.Adapters.Bento4
{
    internal static class PathHelper
    {
        /// <summary>
        /// 为路径添加双引号，处理已包含引号的情况
        /// </summary>
        public static string AddQuotes(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // 如果已经包含引号，则返回原路径
            if (path.StartsWith("\"") && path.EndsWith("\""))
                return path;

            // 否则添加引号，同时转义路径中可能存在的引号
            return $"\"{path.Replace("\"", "\\\"")}\"";
        }
    }
}
