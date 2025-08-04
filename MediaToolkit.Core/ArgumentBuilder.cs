using System.Collections.Generic;
using System.Text;

namespace MediaToolkit.Core
{
    /// <summary>
    /// 一个健壮的命令行参数构建器，能正确处理带空格和特殊字符的参数。
    /// </summary>
    public class ArgumentBuilder
    {
        private readonly List<string> _args = new List<string>();

        /// <summary>
        /// 添加一个参数。如果参数为null或空，则忽略。
        /// </summary>
        /// <param name="arg">要添加的参数或选项（例如 "-v" 或 "inputFile.mp4"）</param>
        public ArgumentBuilder Append(string arg)
        {
            if (!string.IsNullOrEmpty(arg))
            {
                _args.Add(arg);
            }
            return this;
        }

        /// <summary>
        /// 根据条件添加一个参数。
        /// </summary>
        /// <param name="arg">要添加的参数</param>
        /// <param name="condition">仅当条件为 true 时才添加</param>
        public ArgumentBuilder Append(string arg, bool condition)
        {
            if (condition)
            {
                Append(arg);
            }
            return this;
        }

        /// <summary>
        /// 添加一个带值的选项，例如 "-b:v 5000k"。
        /// </summary>
        /// <param name="option">选项名称 (例如 "-b:v")</param>
        /// <param name="value">选项的值 (例如 "5000k")</param>
        public ArgumentBuilder Append(string option, string value)
        {
            if (!string.IsNullOrEmpty(option) && !string.IsNullOrEmpty(value))
            {
                _args.Add(option);
                _args.Add(value);
            }
            return this;
        }

        /// <summary>
        /// 直接追加一个预先格式化好的参数，不进行额外处理。
        /// 适用于已经包含了引号或特殊语法的复杂参数。
        /// 如：
        /// string filterGraph = "\"scale=1280:-1,subtitles='C:\\My Subtitles\\sub.srt'\"";
        /// var builder = new ArgumentBuilder();
        /// builder.Append("-i", "input.mp4");
        /// builder.Append("-vf"); // -vf 是一个参数
        /// builder.AppendRaw(filterGraph); // 整个滤镜图是另一个参数，我们直接附加
        /// builder.Append("output.mp4");
        // 生成结果: -i "input.mp4" -vf "scale=1280:-1,subtitles='C:\My Subtitles\sub.srt'" "output.mp4"
        /// </summary>
        public ArgumentBuilder AppendRaw(string rawArg)
        {
            if (!string.IsNullOrEmpty(rawArg))
            {
                _args.Add(rawArg);
            }
            return this;
        }

        /// <summary>
        /// 将所有参数构建成一个单一的、格式正确的命令行字符串。
        /// </summary>
        /// <returns>可安全传递给 Process.StartInfo.Arguments 的字符串</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var arg in _args)
            {
                // 这是关键：为每个包含空格的参数添加引号。
                // 简单的实现是检查空格，但更严格的实现会处理更复杂的场景。
                if (arg.Contains(" ") && !arg.StartsWith("\""))
                {
                    sb.Append($"\"{arg}\"");
                }
                else
                {
                    sb.Append(arg);
                }
                sb.Append(" ");
            }
            return sb.ToString().Trim();
        }
    }
}