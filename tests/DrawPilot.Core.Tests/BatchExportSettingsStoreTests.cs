using System;
using System.IO;
using DrawPilot.Core.Models;
using DrawPilot.Core.Settings;
using Xunit;

namespace DrawPilot.Core.Tests
{
    public sealed class BatchExportSettingsStoreTests
    {
        [Fact]
        public void RoundTripPreservesSettings()
        {
            using (var workspace = new TempWorkspace())
            {
                var settings = new BatchExportSettings
                {
                    InputFolder = @"C:\input",
                    OutputFolder = @"C:\output",
                    Format = BatchExportFormat.Png,
                    PageRange = BatchExportPageRange.CurrentPage,
                    ImageDpi = 600,
                    OverwriteExisting = true
                };

                BatchExportSettingsStore.Save(workspace.SettingsPath, settings);
                var loaded = BatchExportSettingsStore.Load(workspace.SettingsPath);

                Assert.Equal(settings.InputFolder, loaded.InputFolder);
                Assert.Equal(settings.OutputFolder, loaded.OutputFolder);
                Assert.Equal(settings.Format, loaded.Format);
                Assert.Equal(settings.PageRange, loaded.PageRange);
                Assert.Equal(settings.ImageDpi, loaded.ImageDpi);
                Assert.Equal(settings.OverwriteExisting, loaded.OverwriteExisting);
            }
        }

        [Fact]
        public void MissingFileReturnsDefaults()
        {
            using (var workspace = new TempWorkspace())
            {
                var loaded = BatchExportSettingsStore.Load(workspace.SettingsPath);

                Assert.Equal(BatchExportFormat.Pdf, loaded.Format);
                Assert.Equal(BatchExportPageRange.AllPages, loaded.PageRange);
                Assert.Equal(300, loaded.ImageDpi);
                Assert.False(loaded.OverwriteExisting);
            }
        }

        [Fact]
        public void InvalidJsonReturnsDefaults()
        {
            using (var workspace = new TempWorkspace())
            {
                File.WriteAllText(workspace.SettingsPath, "not json");

                var loaded = BatchExportSettingsStore.Load(workspace.SettingsPath);

                Assert.Equal(BatchExportFormat.Pdf, loaded.Format);
                Assert.Equal(BatchExportPageRange.AllPages, loaded.PageRange);
                Assert.Equal(300, loaded.ImageDpi);
            }
        }

        private sealed class TempWorkspace : IDisposable
        {
            public TempWorkspace()
            {
                Root = Path.Combine(
                    Path.GetTempPath(),
                    "DrawPilot.SettingsTests." + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Root);
                SettingsPath = Path.Combine(Root, "settings.json");
            }

            public string Root { get; }

            public string SettingsPath { get; }

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
