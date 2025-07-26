using System;
using System.Threading;
using System.Threading.Tasks;

namespace MediaToolkit.Core
{
    public interface IMediaToolAdapter
    {
        string ToolName { get; }
        string ExecutablePath { get; }
        event EventHandler<string> LogReceived;
        Task<ToolResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default, string workingDirectory = null);

    }
}