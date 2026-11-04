using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorelDRAWTool.Core.Models;

namespace CorelDRAWTool.Core.Batch
{
    /// <summary>
    /// 纯计划器：只枚举文件和计算输出路径，不访问 CorelDRAW，也不创建目录。
    /// </summary>
    public static class BatchExportPlanner
    {
        public const string SourcePattern = "*.cdr";

        public static BatchExportPlan CreatePlan(BatchExportRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!Directory.Exists(request.InputFolder))
            {
                throw new DirectoryNotFoundException("输入文件夹不存在：" + request.InputFolder);
            }

            string[] sourceFiles = Directory
                .GetFiles(request.InputFolder, SourcePattern, SearchOption.TopDirectoryOnly)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var items = new List<BatchExportItem>();
            var skipped = new List<string>();

            foreach (string sourcePath in sourceFiles)
            {
                string outputPath = BuildOutputPath(sourcePath, request.OutputFolder, request.Format);
                bool outputExists = File.Exists(outputPath);

                if (outputExists && !request.OverwriteExisting)
                {
                    skipped.Add(sourcePath);
                    continue;
                }

                items.Add(new BatchExportItem(sourcePath, outputPath, outputExists));
            }

            return new BatchExportPlan(items, skipped);
        }

        public static string BuildOutputPath(
            string sourcePath,
            string outputFolder,
            BatchExportFormat format)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("源文件路径不能为空。", nameof(sourcePath));
            }

            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                throw new ArgumentException("输出文件夹不能为空。", nameof(outputFolder));
            }

            return Path.Combine(
                outputFolder,
                Path.GetFileNameWithoutExtension(sourcePath) + GetExtension(format));
        }

        private static string GetExtension(BatchExportFormat format)
        {
            switch (format)
            {
                case BatchExportFormat.Pdf:
                    return ".pdf";
                default:
                    throw new ArgumentOutOfRangeException(nameof(format));
            }
        }
    }
}
