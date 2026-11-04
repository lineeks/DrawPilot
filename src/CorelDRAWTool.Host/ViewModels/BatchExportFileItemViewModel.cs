using System.IO;

namespace CorelDRAWTool.Host.ViewModels
{
    public sealed class BatchExportFileItemViewModel : ObservableObject
    {
        private string _status;
        private string _detail;

        public BatchExportFileItemViewModel(
            string sourcePath,
            string outputPath,
            string status,
            string detail)
        {
            SourcePath = sourcePath ?? string.Empty;
            OutputPath = outputPath ?? string.Empty;
            FileName = string.IsNullOrEmpty(SourcePath) ? string.Empty : Path.GetFileName(SourcePath);
            _status = status ?? string.Empty;
            _detail = detail ?? string.Empty;
        }

        public string FileName { get; }

        public string SourcePath { get; }

        public string OutputPath { get; }

        public string Status
        {
            get { return _status; }
            set { SetProperty(ref _status, value); }
        }

        public string Detail
        {
            get { return _detail; }
            set { SetProperty(ref _detail, value); }
        }
    }
}
