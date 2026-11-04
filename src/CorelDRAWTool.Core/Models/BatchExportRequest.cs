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
            : this(
                inputFolder,
                outputFolder,
                format,
                BatchExportPageRange.AllPages,
                300,
                overwriteExisting)
        {
        }

        public BatchExportRequest(
            string inputFolder,
            string outputFolder,
            BatchExportFormat format,
            BatchExportPageRange pageRange,
            int imageDpi,
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

            if (imageDpi < 72 || imageDpi > 1200)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(imageDpi),
                    "图片分辨率必须在 72 到 1200 DPI 之间。");
            }

            InputFolder = inputFolder;
            OutputFolder = outputFolder;
            Format = format;
            PageRange = pageRange;
            ImageDpi = imageDpi;
            OverwriteExisting = overwriteExisting;
        }

        public string InputFolder { get; }

        public string OutputFolder { get; }

        public BatchExportFormat Format { get; }

        public BatchExportPageRange PageRange { get; }

        public int ImageDpi { get; }

        public bool OverwriteExisting { get; }
    }
}
