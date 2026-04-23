using System.Diagnostics;
using System.Text.Json;
namespace MzRubyConvGui;
public partial class MainForm
{
    private sealed class AppSettings
    {
        public string? RubyPath { get; set; }
        public string? ScriptPath { get; set; }
        public string? InputPath { get; set; }
        public string? OutputDir { get; set; }
        public string? BaseName { get; set; }
        public List<string>? InputHistory { get; set; }
        public List<string>? OutputDirHistory { get; set; }
        public List<string>? BaseNameHistory { get; set; }
        public string? Mode { get; set; }
        public string? Fixed { get; set; }
        public string? Layout { get; set; }
        public string? Resize { get; set; }
        public string? Method { get; set; }
        public decimal Strength { get; set; }
        public string? Distance { get; set; }
        public string? Remove { get; set; }
        public string? Sort { get; set; }
        public bool PngOnly { get; set; }
        public bool PreviewDisplayAspect { get; set; }
    }
}
