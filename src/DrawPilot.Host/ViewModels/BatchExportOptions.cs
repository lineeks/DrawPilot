using DrawPilot.Core.Models;

namespace DrawPilot.Host.ViewModels
{
    public sealed class BatchExportFormatOption
    {
        public BatchExportFormatOption(BatchExportFormat format, string label)
        {
            Format = format;
            Label = label;
        }

        public BatchExportFormat Format { get; }

        public string Label { get; }

        public override string ToString()
        {
            return Label;
        }
    }

    public sealed class BatchExportPageRangeOption
    {
        public BatchExportPageRangeOption(BatchExportPageRange pageRange, string label)
        {
            PageRange = pageRange;
            Label = label;
        }

        public BatchExportPageRange PageRange { get; }

        public string Label { get; }

        public override string ToString()
        {
            return Label;
        }
    }

    public sealed class BatchExportDpiOption
    {
        public BatchExportDpiOption(int dpi, string label)
        {
            Dpi = dpi;
            Label = label;
        }

        public int Dpi { get; }

        public string Label { get; }

        public override string ToString()
        {
            return Label;
        }
    }
}
