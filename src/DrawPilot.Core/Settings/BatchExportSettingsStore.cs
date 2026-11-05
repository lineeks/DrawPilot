using System;
using System.IO;
using System.Runtime.Serialization.Json;
using DrawPilot.Core.Models;

namespace DrawPilot.Core.Settings
{
    public static class BatchExportSettingsStore
    {
        public static BatchExportSettings Load(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    return new BatchExportSettings();
                }

                using (var stream = File.OpenRead(path))
                {
                    var serializer = new DataContractJsonSerializer(typeof(BatchExportSettings));
                    var settings = serializer.ReadObject(stream) as BatchExportSettings;
                    if (settings == null)
                    {
                        return new BatchExportSettings();
                    }

                    Normalize(settings);
                    return settings;
                }
            }
            catch
            {
                return new BatchExportSettings();
            }
        }

        public static void Save(string path, BatchExportSettings settings)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("设置文件路径不能为空。", nameof(path));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            Normalize(settings);

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string tempPath = path + ".tmp";
            using (var stream = File.Create(tempPath))
            {
                var serializer = new DataContractJsonSerializer(typeof(BatchExportSettings));
                serializer.WriteObject(stream, settings);
            }

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            File.Move(tempPath, path);
        }

        private static void Normalize(BatchExportSettings settings)
        {
            if (!Enum.IsDefined(typeof(BatchExportFormat), settings.Format))
            {
                settings.Format = BatchExportFormat.Pdf;
            }

            if (!Enum.IsDefined(typeof(BatchExportPageRange), settings.PageRange))
            {
                settings.PageRange = BatchExportPageRange.AllPages;
            }

            if (settings.ImageDpi < 72 || settings.ImageDpi > 1200)
            {
                settings.ImageDpi = 300;
            }
        }
    }
}
