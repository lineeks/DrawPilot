using System;
using System.Globalization;
using System.IO;
using CorelDRAWTool.Core.Models;

namespace CorelDRAWTool.Core.Batch
{
    /// <summary>
    /// 批量导出的文件命名规则。计划器和 Corel 适配层共用，避免两处规则漂移。
    /// </summary>
    public static class BatchExportNaming
    {
        public static bool IsImage(BatchExportFormat format)
        {
            return format == BatchExportFormat.Png || format == BatchExportFormat.Tiff;
        }

        public static string GetExtension(BatchExportFormat format)
        {
            switch (format)
            {
                case BatchExportFormat.Pdf:
                    return ".pdf";
                case BatchExportFormat.Png:
                    return ".png";
                case BatchExportFormat.Tiff:
                    return ".tif";
                default:
                    throw new ArgumentOutOfRangeException(nameof(format));
            }
        }

        public static string GetOutputPath(
            string sourcePath,
            string outputFolder,
            BatchExportFormat format,
            BatchExportPageRange pageRange,
            int pageNumber)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("源文件路径不能为空。", nameof(sourcePath));
            }

            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                throw new ArgumentException("输出文件夹不能为空。", nameof(outputFolder));
            }

            string baseName = Path.GetFileNameWithoutExtension(sourcePath);
            string extension = GetExtension(format);

            if (format == BatchExportFormat.Pdf)
            {
                return Path.Combine(outputFolder, baseName + extension);
            }

            if (pageRange == BatchExportPageRange.AllPages)
            {
                if (pageNumber < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(pageNumber));
                }

                return Path.Combine(
                    outputFolder,
                    baseName + "-p" + pageNumber.ToString("00", CultureInfo.InvariantCulture) + extension);
            }

            return Path.Combine(outputFolder, baseName + extension);
        }

        public static string GetSearchPattern(
            string sourcePath,
            BatchExportFormat format,
            BatchExportPageRange pageRange)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("源文件路径不能为空。", nameof(sourcePath));
            }

            string baseName = Path.GetFileNameWithoutExtension(sourcePath);
            string extension = GetExtension(format);

            if (format == BatchExportFormat.Pdf)
            {
                return baseName + extension;
            }

            return pageRange == BatchExportPageRange.AllPages
                ? baseName + "-p*" + extension
                : baseName + extension;
        }
    }
}
