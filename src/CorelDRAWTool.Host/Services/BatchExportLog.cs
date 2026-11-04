using System;
using System.IO;
using System.Text;

namespace CorelDRAWTool.Host.Services
{
    public sealed class BatchExportLog : IDisposable
    {
        private readonly StreamWriter _writer;

        public BatchExportLog(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _writer = new StreamWriter(path, false, new UTF8Encoding(false));
            _writer.AutoFlush = true;
        }

        public void Write(string message)
        {
            _writer.WriteLine(
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " +
                (message ?? string.Empty));
        }

        public void Dispose()
        {
            _writer.Dispose();
        }
    }
}
