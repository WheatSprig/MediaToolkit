namespace MediaToolkit.Core
{
    public class ToolResult
    {
        public ToolResult(int exitCode, string output, string error)
        {
            this.ExitCode = exitCode;
            this.Output = output;
            this.Error = error;
        }

        public int ExitCode { get; }
        public string Output { get; }
        public string Error { get; }
    }
}
