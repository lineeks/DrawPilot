using System;
using System.IO;
using System.Linq;
using CorelDRAWTool.Core.Batch;
using CorelDRAWTool.Core.Models;
using Xunit;

namespace CorelDRAWTool.Core.Tests
{
    public sealed class BatchExportPlannerTests
    {
        [Fact]
        public void CreatesPlanForTopLevelCdrFilesOnly()
        {
            using (var workspace = new TempWorkspace())
            {
                workspace.CreateFile("a.cdr");
                workspace.CreateFile("b.cdr");
                workspace.CreateFile("notes.txt");
                Directory.CreateDirectory(Path.Combine(workspace.Root, "sub"));
                workspace.CreateFile(Path.Combine("sub", "c.cdr"));

                var plan = BatchExportPlanner.CreatePlan(workspace.Request());

                Assert.Equal(2, plan.SourceFileCount);
                Assert.Equal(2, plan.Items.Count);
                Assert.Empty(plan.SkippedSourcePaths);
                Assert.Equal(new[] { "a.cdr", "b.cdr" }, plan.Items.Select(item => Path.GetFileName(item.SourcePath)));
                Assert.All(plan.Items, item => Assert.EndsWith(".pdf", item.OutputPath));
                Assert.All(plan.Items, item => Assert.False(item.OverwritesExisting));
            }
        }

        [Fact]
        public void SkipsExistingOutputsWhenNotOverwriting()
        {
            using (var workspace = new TempWorkspace())
            {
                workspace.CreateFile("a.cdr");
                workspace.CreateFile(Path.Combine("output", "a.pdf"));

                var plan = BatchExportPlanner.CreatePlan(workspace.Request(overwriteExisting: false));

                Assert.Empty(plan.Items);
                Assert.Single(plan.SkippedSourcePaths);
                Assert.Equal(1, plan.SourceFileCount);
                Assert.Equal(1, plan.SkippedCount);
                Assert.False(plan.HasWork);
            }
        }

        [Fact]
        public void IncludesExistingOutputsWhenOverwriting()
        {
            using (var workspace = new TempWorkspace())
            {
                workspace.CreateFile("a.cdr");
                workspace.CreateFile(Path.Combine("output", "a.pdf"));

                var plan = BatchExportPlanner.CreatePlan(workspace.Request(overwriteExisting: true));

                var item = Assert.Single(plan.Items);
                Assert.True(item.OverwritesExisting);
                Assert.Empty(plan.SkippedSourcePaths);
                Assert.True(plan.HasWork);
            }
        }

        [Fact]
        public void OrdersFilesByNameIgnoringCase()
        {
            using (var workspace = new TempWorkspace())
            {
                workspace.CreateFile("b.cdr");
                workspace.CreateFile("A.cdr");

                var plan = BatchExportPlanner.CreatePlan(workspace.Request());

                Assert.Equal(new[] { "A.cdr", "b.cdr" }, plan.Items.Select(item => Path.GetFileName(item.SourcePath)));
            }
        }

        [Fact]
        public void EmptyFolderProducesEmptyPlan()
        {
            using (var workspace = new TempWorkspace())
            {
                var plan = BatchExportPlanner.CreatePlan(workspace.Request());

                Assert.False(plan.HasWork);
                Assert.Equal(0, plan.SourceFileCount);
            }
        }

        [Fact]
        public void MissingInputFolderThrows()
        {
            using (var workspace = new TempWorkspace())
            {
                var request = new BatchExportRequest(
                    Path.Combine(workspace.Root, "missing"),
                    Path.Combine(workspace.Root, "out"),
                    BatchExportFormat.Pdf,
                    false);

                Assert.Throws<DirectoryNotFoundException>(
                    () => BatchExportPlanner.CreatePlan(request));
            }
        }

        [Fact]
        public void BuildOutputPathUsesSourceBaseName()
        {
            string output = BatchExportPlanner.BuildOutputPath(
                @"C:\input\示例.cdr",
                @"D:\output",
                BatchExportFormat.Pdf);

            Assert.Equal(@"D:\output\示例.pdf", output);
        }

        [Fact]
        public void BuildsPaddedPngPathsForAllPages()
        {
            using (var workspace = new TempWorkspace())
            {
                workspace.CreateFile("a.cdr");

                var plan = BatchExportPlanner.CreatePlan(workspace.Request(
                    format: BatchExportFormat.Png,
                    pageRange: BatchExportPageRange.AllPages));

                var item = Assert.Single(plan.Items);
                Assert.Equal("a-p01.png", Path.GetFileName(item.OutputPath));
            }
        }

        [Fact]
        public void BuildsPngPathForCurrentPage()
        {
            using (var workspace = new TempWorkspace())
            {
                workspace.CreateFile("a.cdr");

                var plan = BatchExportPlanner.CreatePlan(workspace.Request(
                    format: BatchExportFormat.Png,
                    pageRange: BatchExportPageRange.CurrentPage));

                var item = Assert.Single(plan.Items);
                Assert.Equal("a.png", Path.GetFileName(item.OutputPath));
            }
        }

        [Fact]
        public void BuildsTiffPathForAllPages()
        {
            using (var workspace = new TempWorkspace())
            {
                workspace.CreateFile("a.cdr");

                var plan = BatchExportPlanner.CreatePlan(workspace.Request(
                    format: BatchExportFormat.Tiff,
                    pageRange: BatchExportPageRange.AllPages));

                var item = Assert.Single(plan.Items);
                Assert.Equal("a-p01.tif", Path.GetFileName(item.OutputPath));
            }
        }

        [Fact]
        public void SkipsWhenAnyPageImageExists()
        {
            using (var workspace = new TempWorkspace())
            {
                workspace.CreateFile("a.cdr");
                workspace.CreateFile(Path.Combine("output", "a-p03.png"));

                var plan = BatchExportPlanner.CreatePlan(workspace.Request(
                    format: BatchExportFormat.Png,
                    pageRange: BatchExportPageRange.AllPages,
                    overwriteExisting: false));

                Assert.Empty(plan.Items);
                Assert.Single(plan.SkippedSourcePaths);
            }
        }

        [Fact]
        public void IncludesWhenImageOutputExistsAndOverwriteIsSet()
        {
            using (var workspace = new TempWorkspace())
            {
                workspace.CreateFile("a.cdr");
                workspace.CreateFile(Path.Combine("output", "a-p03.png"));

                var plan = BatchExportPlanner.CreatePlan(workspace.Request(
                    format: BatchExportFormat.Png,
                    pageRange: BatchExportPageRange.AllPages,
                    overwriteExisting: true));

                var item = Assert.Single(plan.Items);
                Assert.True(item.OverwritesExisting);
            }
        }

        [Fact]
        public void RejectsInvalidImageDpi()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BatchExportRequest(
                @"C:\input",
                @"C:\output",
                BatchExportFormat.Png,
                BatchExportPageRange.AllPages,
                10,
                false));
        }

        private sealed class TempWorkspace : IDisposable
        {
            public TempWorkspace()
            {
                Root = Path.Combine(
                    Path.GetTempPath(),
                    "CorelDRAWTool.Tests." + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Root);
            }

            public string Root { get; }

            public void CreateFile(string relativePath)
            {
                string fullPath = Path.Combine(Root, relativePath);
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(fullPath, "test");
            }

            public BatchExportRequest Request(
                BatchExportFormat format = BatchExportFormat.Pdf,
                BatchExportPageRange pageRange = BatchExportPageRange.AllPages,
                int imageDpi = 300,
                bool overwriteExisting = false)
            {
                return new BatchExportRequest(
                    Root,
                    Path.Combine(Root, "output"),
                    format,
                    pageRange,
                    imageDpi,
                    overwriteExisting);
            }

            public void Dispose()
            {
                try
                {
                    Directory.Delete(Root, true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
