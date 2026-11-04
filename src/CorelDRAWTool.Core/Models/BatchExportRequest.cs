using System;

namespace CorelDRAWTool.Core.Models
{
    public sealed class BatchExportRequest
    {
        public BatchExportRequest(
            string inputFolder,
            string outputFolder,
            BatchExportFormat format,
            bool overwriteExisting)
        {
            if (string.IsNullOrWhiteSpace(inputFolder))
            {
                throw new ArgumentException("输入文件夹不能为空。", nameof(inputFolder));
            }

            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                throw new ArgumentException("输出文件夹不能为空。", nameof(outputFolder));
            }

            InputFolder = inputFolder;
            OutputFolder = outputFolder;
            Format = format;
            OverwriteExisting = overwriteExisting;
        }

        public string InputFolder { get; }

        public string OutputFolder { get; }

        public BatchExportFormat Format { get; }

        public bool OverwriteExisting { get; }
    }
}
