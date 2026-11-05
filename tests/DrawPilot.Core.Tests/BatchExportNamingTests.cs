using System;
using DrawPilot.Core.Batch;
using DrawPilot.Core.Models;
using Xunit;

namespace DrawPilot.Core.Tests
{
    public sealed class BatchExportNamingTests
    {
        [Fact]
        public void PdfUsesSinglePdfPath()
        {
            string path = BatchExportNaming.GetOutputPath(
                @"C:\input\示例.cdr",
                @"D:\output",
                BatchExportFormat.Pdf,
                BatchExportPageRange.AllPages,
                3);

            Assert.Equal(@"D:\output\示例.pdf", path);
        }

        [Fact]
        public void PngAllPagesUsesPaddedPageNumber()
        {
            string path = BatchExportNaming.GetOutputPath(
                @"C:\input\示例.cdr",
                @"D:\output",
                BatchExportFormat.Png,
                BatchExportPageRange.AllPages,
                3);

            Assert.Equal(@"D:\output\示例-p03.png", path);
        }

        [Fact]
        public void PngCurrentPageUsesSinglePath()
        {
            string path = BatchExportNaming.GetOutputPath(
                @"C:\input\示例.cdr",
                @"D:\output",
                BatchExportFormat.Png,
                BatchExportPageRange.CurrentPage,
                3);

            Assert.Equal(@"D:\output\示例.png", path);
        }

        [Fact]
        public void TiffAllPagesUsesTifExtension()
        {
            string path = BatchExportNaming.GetOutputPath(
                @"C:\input\示例.cdr",
                @"D:\output",
                BatchExportFormat.Tiff,
                BatchExportPageRange.AllPages,
                1);

            Assert.Equal(@"D:\output\示例-p01.tif", path);
        }

        [Fact]
        public void SearchPatternMatchesAllPageImages()
        {
            string pattern = BatchExportNaming.GetSearchPattern(
                @"C:\input\示例.cdr",
                BatchExportFormat.Png,
                BatchExportPageRange.AllPages);

            Assert.Equal(@"示例-p*.png", pattern);
        }

        [Fact]
        public void SearchPatternMatchesCurrentPageImage()
        {
            string pattern = BatchExportNaming.GetSearchPattern(
                @"C:\input\示例.cdr",
                BatchExportFormat.Tiff,
                BatchExportPageRange.CurrentPage);

            Assert.Equal(@"示例.tif", pattern);
        }

        [Fact]
        public void InvalidPageNumberThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BatchExportNaming.GetOutputPath(
                @"C:\input\示例.cdr",
                @"D:\output",
                BatchExportFormat.Png,
                BatchExportPageRange.AllPages,
                0));
        }
    }
}
