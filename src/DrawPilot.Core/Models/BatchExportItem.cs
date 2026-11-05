namespace DrawPilot.Core.Models
{
    public sealed class BatchExportItem
    {
        public BatchExportItem(string sourcePath, string outputPath, bool overwritesExisting)
        {
            SourcePath = sourcePath;
            OutputPath = outputPath;
            OverwritesExisting = overwritesExisting;
        }

        public string SourcePath { get; }

        public string OutputPath { get; }

        public bool OverwritesExisting { get; }
    }
}
