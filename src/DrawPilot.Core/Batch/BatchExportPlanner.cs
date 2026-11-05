using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DrawPilot.Core.Models;

namespace DrawPilot.Core.Batch
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
                string outputPath = BatchExportNaming.GetOutputPath(
                    sourcePath,
                    request.OutputFolder,
                    request.Format,
                    request.PageRange,
                    1);

                bool outputExists = OutputExists(request, sourcePath, outputPath);

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
            return BatchExportNaming.GetOutputPath(
                sourcePath,
                outputFolder,
                format,
                BatchExportPageRange.AllPages,
                1);
        }

        private static bool OutputExists(
            BatchExportRequest request,
            string sourcePath,
            string outputPath)
        {
            if (request.Format == BatchExportFormat.Pdf)
            {
                return File.Exists(outputPath);
            }

            if (!Directory.Exists(request.OutputFolder))
            {
                return false;
            }

            string pattern = BatchExportNaming.GetSearchPattern(
                sourcePath,
                request.Format,
                request.PageRange);

            return Directory
                .EnumerateFiles(request.OutputFolder, pattern, SearchOption.TopDirectoryOnly)
                .Any();
        }
    }
}
