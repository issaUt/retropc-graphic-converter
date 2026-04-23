using System.Diagnostics;
using System.Text.Json;
namespace MzRubyConvGui;
public partial class MainForm
{
    private sealed class OutputImageItem
    {
        public OutputImageItem(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public override string ToString()
        {
            return System.IO.Path.GetFileName(Path);
        }
    }
}
