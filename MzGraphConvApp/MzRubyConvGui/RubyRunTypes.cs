using System.Diagnostics;
using System.Text.Json;
namespace MzRubyConvGui;
public partial class MainForm
{
    private sealed record RubyRunRequest(string RubyPath, string WorkingDirectory, IReadOnlyList<string> Arguments);

    private sealed record RubyRunResult(int ExitCode, string StdOut, string Log, bool Canceled);
}
