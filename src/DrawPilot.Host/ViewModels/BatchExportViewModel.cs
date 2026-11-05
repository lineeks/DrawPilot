using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using DrawPilot.Core.Batch;
using DrawPilot.Core.Models;
using DrawPilot.Core.Settings;
using DrawPilot.Corel;
using DrawPilot.Host.Services;

namespace DrawPilot.Host.ViewModels
{
    public sealed class BatchExportViewModel : ObservableObject, IDisposable
    {
        private static readonly TimeSpan AttachTimeout = TimeSpan.FromSeconds(10);

        private readonly RelayCommand _startCommand;
        private readonly RelayCommand _cancelCommand;
        private readonly RelayCommand _browseInputCommand;
        private readonly RelayCommand _browseOutputCommand;
        private readonly RelayCommand _openLogFolderCommand;

        private readonly BatchExportFormatOption[] _formatOptions;
        private readonly BatchExportPageRangeOption[] _pageRangeOptions;
        private readonly BatchExportDpiOption[] _dpiOptions;

        private string _inputFolder;
        private string _outputFolder;
        private BatchExportFormatOption _selectedFormatOption;
        private BatchExportPageRangeOption _selectedPageRangeOption;
        private BatchExportDpiOption _selectedDpiOption;
        private bool _overwriteExisting;
        private bool _isRunning;
        private string _statusText = "选择输入文件夹和输出文件夹，然后开始导出。";
        private string _logFilePath;
        private double _progressValue;
        private double _progressMaximum = 1;
        private CancellationTokenSource _cancelSource;
        private Thread _worker;
        private BatchExportLog _log;
        private int _successCount;
        private int _failureCount;
        private int _skippedCount;

        public BatchExportViewModel()
        {
            Files = new ObservableCollection<BatchExportFileItemViewModel>();

            _formatOptions = new[]
            {
                new BatchExportFormatOption(BatchExportFormat.Pdf, "PDF"),
                new BatchExportFormatOption(BatchExportFormat.Png, "PNG"),
                new BatchExportFormatOption(BatchExportFormat.Tiff, "TIFF")
            };

            _pageRangeOptions = new[]
            {
                new BatchExportPageRangeOption(BatchExportPageRange.AllPages, "全部页"),
                new BatchExportPageRangeOption(BatchExportPageRange.CurrentPage, "当前页")
            };

            _dpiOptions = new[]
            {
                new BatchExportDpiOption(150, "150 DPI"),
                new BatchExportDpiOption(300, "300 DPI"),
                new BatchExportDpiOption(600, "600 DPI")
            };

            _selectedFormatOption = _formatOptions[0];
            _selectedPageRangeOption = _pageRangeOptions[0];
            _selectedDpiOption = _dpiOptions[1];

            _startCommand = new RelayCommand(Start, () => !IsRunning);
            _cancelCommand = new RelayCommand(Cancel, () => IsRunning);
            _browseInputCommand = new RelayCommand(BrowseInput, () => !IsRunning);
            _browseOutputCommand = new RelayCommand(BrowseOutput, () => !IsRunning);
            _openLogFolderCommand = new RelayCommand(
                OpenLogFolder,
                () => Directory.Exists(AppPaths.LogDirectory));

            LoadSettings();
        }

        public ObservableCollection<BatchExportFileItemViewModel> Files { get; }

        public IReadOnlyList<BatchExportFormatOption> FormatOptions
        {
            get { return _formatOptions; }
        }

        public IReadOnlyList<BatchExportPageRangeOption> PageRangeOptions
        {
            get { return _pageRangeOptions; }
        }

        public IReadOnlyList<BatchExportDpiOption> DpiOptions
        {
            get { return _dpiOptions; }
        }

        public BatchExportFormatOption SelectedFormatOption
        {
            get { return _selectedFormatOption; }
            set
            {
                if (SetProperty(ref _selectedFormatOption, value))
                {
                    OnPropertyChanged(nameof(IsImageFormat));
                    OnPropertyChanged(nameof(IsDpiEnabled));
                }
            }
        }

        public BatchExportPageRangeOption SelectedPageRangeOption
        {
            get { return _selectedPageRangeOption; }
            set { SetProperty(ref _selectedPageRangeOption, value); }
        }

        public BatchExportDpiOption SelectedDpiOption
        {
            get { return _selectedDpiOption; }
            set { SetProperty(ref _selectedDpiOption, value); }
        }

        public bool IsImageFormat
        {
            get
            {
                return SelectedFormatOption != null &&
                       SelectedFormatOption.Format != BatchExportFormat.Pdf;
            }
        }

        public bool IsDpiEnabled
        {
            get { return IsImageFormat; }
        }

        public string InputFolder
        {
            get { return _inputFolder; }
            set
            {
                if (SetProperty(ref _inputFolder, value) &&
                    string.IsNullOrWhiteSpace(_outputFolder) &&
                    !string.IsNullOrWhiteSpace(value))
                {
                    OutputFolder = Path.Combine(value, "PDF导出");
                }
            }
        }

        public string OutputFolder
        {
            get { return _outputFolder; }
            set { SetProperty(ref _outputFolder, value); }
        }

        public bool OverwriteExisting
        {
            get { return _overwriteExisting; }
            set { SetProperty(ref _overwriteExisting, value); }
        }

        public bool IsRunning
        {
            get { return _isRunning; }
            private set
            {
                if (SetProperty(ref _isRunning, value))
                {
                    _startCommand.RaiseCanExecuteChanged();
                    _cancelCommand.RaiseCanExecuteChanged();
                    _browseInputCommand.RaiseCanExecuteChanged();
                    _browseOutputCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string StatusText
        {
            get { return _statusText; }
            private set { SetProperty(ref _statusText, value); }
        }

        public string LogFilePath
        {
            get { return _logFilePath; }
            private set { SetProperty(ref _logFilePath, value); }
        }

        public double ProgressValue
        {
            get { return _progressValue; }
            private set { SetProperty(ref _progressValue, value); }
        }

        public double ProgressMaximum
        {
            get { return _progressMaximum; }
            private set { SetProperty(ref _progressMaximum, value); }
        }

        public ICommand StartCommand
        {
            get { return _startCommand; }
        }

        public ICommand CancelCommand
        {
            get { return _cancelCommand; }
        }

        public ICommand BrowseInputCommand
        {
            get { return _browseInputCommand; }
        }

        public ICommand BrowseOutputCommand
        {
            get { return _browseOutputCommand; }
        }

        public ICommand OpenLogFolderCommand
        {
            get { return _openLogFolderCommand; }
        }

        private void BrowseInput()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择包含 .cdr 文件的文件夹";
                dialog.SelectedPath = Directory.Exists(InputFolder) ? InputFolder : string.Empty;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    InputFolder = dialog.SelectedPath;
                }
            }
        }

        private void BrowseOutput()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "选择导出输出文件夹";
                dialog.SelectedPath = Directory.Exists(OutputFolder) ? OutputFolder : string.Empty;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    OutputFolder = dialog.SelectedPath;
                }
            }
        }

        private void OpenLogFolder()
        {
            try
            {
                string directory = AppPaths.LogDirectory;
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                Process.Start("explorer.exe", directory);
            }
            catch (Exception ex)
            {
                StatusText = "无法打开日志文件夹：" + ex.Message;
            }
        }

        private void Start()
        {
            string input = (InputFolder ?? string.Empty).Trim();
            string output = (OutputFolder ?? string.Empty).Trim();

            if (!Directory.Exists(input))
            {
                StatusText = "输入文件夹不存在。";
                return;
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                StatusText = "请选择输出文件夹。";
                return;
            }

            try
            {
                Directory.CreateDirectory(output);
            }
            catch (Exception ex)
            {
                StatusText = "无法创建输出文件夹：" + ex.Message;
                return;
            }

            BatchExportFormat format = SelectedFormatOption != null
                ? SelectedFormatOption.Format
                : BatchExportFormat.Pdf;
            BatchExportPageRange pageRange = SelectedPageRangeOption != null
                ? SelectedPageRangeOption.PageRange
                : BatchExportPageRange.AllPages;
            int imageDpi = SelectedDpiOption != null
                ? SelectedDpiOption.Dpi
                : 300;

            BatchExportRequest request;
            try
            {
                request = new BatchExportRequest(
                    input,
                    output,
                    format,
                    pageRange,
                    imageDpi,
                    OverwriteExisting);
            }
            catch (Exception ex)
            {
                StatusText = "导出参数无效：" + ex.Message;
                return;
            }

            BatchExportPlan plan;
            try
            {
                plan = BatchExportPlanner.CreatePlan(request);
            }
            catch (Exception ex)
            {
                StatusText = "生成导出计划失败：" + ex.Message;
                return;
            }

            Files.Clear();
            _successCount = 0;
            _failureCount = 0;
            _skippedCount = plan.SkippedCount;

            foreach (BatchExportItem item in plan.Items)
            {
                Files.Add(new BatchExportFileItemViewModel(
                    item.SourcePath,
                    item.OutputPath,
                    "等待中",
                    string.Empty));
            }

            foreach (string skipped in plan.SkippedSourcePaths)
            {
                Files.Add(new BatchExportFileItemViewModel(
                    skipped,
                    null,
                    "已跳过",
                    "输出文件已存在"));
            }

            ProgressMaximum = Math.Max(1, plan.Items.Count);
            ProgressValue = 0;

            if (plan.SourceFileCount == 0)
            {
                StatusText = "输入文件夹中没有 .cdr 文件。";
                return;
            }

            if (plan.Items.Count == 0)
            {
                StatusText = string.Format(
                    "没有需要导出的文件，跳过 {0} 个已存在输出。",
                    plan.SkippedCount);
                SaveSettings();
                return;
            }

            StatusText = string.Format(
                "准备导出 {0} 个文件，跳过 {1} 个。",
                plan.Items.Count,
                plan.SkippedCount);

            SaveSettings();
            StartLog(request, plan);

            IsRunning = true;
            _cancelSource = new CancellationTokenSource();
            var progress = new Progress<BatchExportProgress>(OnProgress);

            _worker = new Thread(() => RunBatch(plan, request, progress, _cancelSource.Token));
            _worker.SetApartmentState(ApartmentState.STA);
            _worker.IsBackground = true;
            _worker.Start();
        }

        private void StartLog(BatchExportRequest request, BatchExportPlan plan)
        {
            try
            {
                LogFilePath = AppPaths.CreateLogPath();
                _log = new BatchExportLog(LogFilePath);
                _log.Write("DrawPilot 批量导出开始");
                _log.Write("输入: " + request.InputFolder);
                _log.Write("输出: " + request.OutputFolder);
                _log.Write(string.Format(
                    "格式: {0}; 页面: {1}; DPI: {2}; 覆盖: {3}",
                    FormatLabel(request.Format),
                    PageRangeLabel(request.PageRange),
                    request.ImageDpi,
                    request.OverwriteExisting ? "是" : "否"));
                _log.Write(string.Format(
                    "计划: 导出 {0} 个, 跳过 {1} 个",
                    plan.Items.Count,
                    plan.SkippedCount));
                _openLogFolderCommand.RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                _log = null;
                LogFilePath = null;
                StatusText = StatusText + "（日志不可用：" + ex.Message + "）";
            }
        }

        private void RunBatch(
            BatchExportPlan plan,
            BatchExportRequest request,
            IProgress<BatchExportProgress> progress,
            CancellationToken token)
        {
            ICorelDrawSession session = null;
            int completed = 0;

            try
            {
                session = CorelDrawSession.Attach(AttachTimeout);

                for (int i = 0; i < plan.Items.Count; i++)
                {
                    if (token.IsCancellationRequested)
                    {
                        progress.Report(BatchExportProgress.Canceled(completed, plan.Items.Count));
                        return;
                    }

                    BatchExportItem item = plan.Items[i];
                    string fileName = Path.GetFileName(item.SourcePath);
                    progress.Report(BatchExportProgress.Started(
                        i,
                        completed,
                        plan.Items.Count,
                        fileName));

                    try
                    {
                        DocumentExportResult result = session.ExportDocument(item, request);
                        completed++;
                        progress.Report(BatchExportProgress.Succeeded(
                            i,
                            completed,
                            plan.Items.Count,
                            fileName,
                            result.OutputPaths,
                            result.Warnings));
                    }
                    catch (Exception ex)
                    {
                        completed++;
                        progress.Report(BatchExportProgress.Failed(
                            i,
                            completed,
                            plan.Items.Count,
                            fileName,
                            ex.Message));
                    }
                }

                progress.Report(BatchExportProgress.Completed(completed, plan.Items.Count));
            }
            catch (Exception ex)
            {
                progress.Report(BatchExportProgress.Fatal(
                    ex.Message,
                    completed,
                    plan.Items.Count));
            }
            finally
            {
                if (session != null)
                {
                    session.Dispose();
                }
            }
        }

        private void OnProgress(BatchExportProgress progress)
        {
            switch (progress.Kind)
            {
                case BatchExportProgressKind.Started:
                    UpdateFile(progress.ItemIndex, "导出中", string.Empty);
                    _log?.Write(string.Format(
                        "开始 [{0}/{1}] {2}",
                        progress.ItemIndex + 1,
                        progress.Total,
                        progress.FileName));
                    StatusText = string.Format(
                        "正在导出 {0}（{1}/{2}）…",
                        progress.FileName,
                        progress.ItemIndex + 1,
                        progress.Total);
                    break;

                case BatchExportProgressKind.Succeeded:
                    _successCount++;
                    UpdateFile(
                        progress.ItemIndex,
                        "成功",
                        FormatSuccessDetail(progress.OutputPaths, progress.Warnings));
                    _log?.Write(string.Format(
                        "成功 {0} -> {1}",
                        progress.FileName,
                        string.Join("; ", progress.OutputPaths)));
                    foreach (string warning in progress.Warnings)
                    {
                        _log?.Write("  警告: " + warning);
                    }

                    ProgressValue = progress.CompletedCount;
                    StatusText = string.Format(
                        "已完成 {0}/{1}：{2}",
                        progress.CompletedCount,
                        progress.Total,
                        progress.FileName);
                    break;

                case BatchExportProgressKind.Failed:
                    _failureCount++;
                    UpdateFile(progress.ItemIndex, "失败", progress.Message);
                    _log?.Write(string.Format(
                        "失败 {0}: {1}",
                        progress.FileName,
                        progress.Message));
                    ProgressValue = progress.CompletedCount;
                    StatusText = string.Format(
                        "失败 {0}：{1}",
                        progress.FileName,
                        progress.Message);
                    break;

                case BatchExportProgressKind.Completed:
                    ProgressValue = progress.CompletedCount;
                    FinishBatch(string.Format(
                        "导出完成：成功 {0}，失败 {1}，跳过 {2}。",
                        _successCount,
                        _failureCount,
                        _skippedCount));
                    break;

                case BatchExportProgressKind.Canceled:
                    MarkPending("已取消", "批量任务已取消");
                    FinishBatch(string.Format(
                        "已取消：成功 {0}，失败 {1}，跳过 {2}。",
                        _successCount,
                        _failureCount,
                        _skippedCount));
                    break;

                case BatchExportProgressKind.Fatal:
                    MarkPending("未执行", "批量任务中断");
                    FinishBatch("批量导出中断：" + progress.Message);
                    break;
            }
        }

        private static string FormatSuccessDetail(
            IReadOnlyList<string> outputPaths,
            IReadOnlyList<string> warnings)
        {
            string detail;
            if (outputPaths == null || outputPaths.Count == 0)
            {
                detail = "已生成";
            }
            else if (outputPaths.Count == 1)
            {
                detail = Path.GetFileName(outputPaths[0]);
            }
            else
            {
                detail = string.Format("生成 {0} 个文件", outputPaths.Count);
            }

            if (warnings != null && warnings.Count > 0)
            {
                detail += string.Format("（{0} 页跳过）", warnings.Count);
            }

            return detail;
        }

        private void UpdateFile(int index, string status, string detail)
        {
            if (index >= 0 && index < Files.Count)
            {
                Files[index].Status = status;
                Files[index].Detail = detail;
            }
        }

        private void MarkPending(string status, string detail)
        {
            foreach (BatchExportFileItemViewModel file in Files)
            {
                if (file.Status == "等待中" || file.Status == "导出中")
                {
                    file.Status = status;
                    file.Detail = detail;
                }
            }
        }

        private void FinishBatch(string message)
        {
            IsRunning = false;
            StatusText = message;
            SaveSettings();

            if (_log != null)
            {
                try
                {
                    _log.Write(message);
                    _log.Dispose();
                }
                catch
                {
                }

                _log = null;
            }

            if (_cancelSource != null)
            {
                _cancelSource.Dispose();
                _cancelSource = null;
            }

            _worker = null;
            _openLogFolderCommand.RaiseCanExecuteChanged();
        }

        private void Cancel()
        {
            if (!IsRunning || _cancelSource == null)
            {
                return;
            }

            StatusText = "正在取消，当前文件完成后停止…";
            _log?.Write("用户请求取消，等待当前文件完成。");
            _cancelSource.Cancel();
        }

        private void LoadSettings()
        {
            BatchExportSettings settings = BatchExportSettingsStore.Load(AppPaths.SettingsFile);
            SelectedFormatOption = FindFormat(settings.Format);
            SelectedPageRangeOption = FindPageRange(settings.PageRange);
            SelectedDpiOption = FindDpi(settings.ImageDpi);
            OutputFolder = settings.OutputFolder;
            InputFolder = settings.InputFolder;
            OverwriteExisting = settings.OverwriteExisting;
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new BatchExportSettings
                {
                    InputFolder = InputFolder,
                    OutputFolder = OutputFolder,
                    Format = SelectedFormatOption != null
                        ? SelectedFormatOption.Format
                        : BatchExportFormat.Pdf,
                    PageRange = SelectedPageRangeOption != null
                        ? SelectedPageRangeOption.PageRange
                        : BatchExportPageRange.AllPages,
                    ImageDpi = SelectedDpiOption != null
                        ? SelectedDpiOption.Dpi
                        : 300,
                    OverwriteExisting = OverwriteExisting
                };

                BatchExportSettingsStore.Save(AppPaths.SettingsFile, settings);
            }
            catch
            {
            }
        }

        private BatchExportFormatOption FindFormat(BatchExportFormat format)
        {
            foreach (BatchExportFormatOption option in _formatOptions)
            {
                if (option.Format == format)
                {
                    return option;
                }
            }

            return _formatOptions[0];
        }

        private BatchExportPageRangeOption FindPageRange(BatchExportPageRange pageRange)
        {
            foreach (BatchExportPageRangeOption option in _pageRangeOptions)
            {
                if (option.PageRange == pageRange)
                {
                    return option;
                }
            }

            return _pageRangeOptions[0];
        }

        private BatchExportDpiOption FindDpi(int dpi)
        {
            foreach (BatchExportDpiOption option in _dpiOptions)
            {
                if (option.Dpi == dpi)
                {
                    return option;
                }
            }

            return _dpiOptions[1];
        }

        private static string FormatLabel(BatchExportFormat format)
        {
            switch (format)
            {
                case BatchExportFormat.Pdf:
                    return "PDF";
                case BatchExportFormat.Png:
                    return "PNG";
                case BatchExportFormat.Tiff:
                    return "TIFF";
                default:
                    return format.ToString();
            }
        }

        private static string PageRangeLabel(BatchExportPageRange pageRange)
        {
            return pageRange == BatchExportPageRange.CurrentPage ? "当前页" : "全部页";
        }

        public void Dispose()
        {
            SaveSettings();

            if (_cancelSource != null)
            {
                try
                {
                    _cancelSource.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            if (_log != null)
            {
                try
                {
                    _log.Write("Host 关闭，日志结束。");
                    _log.Dispose();
                }
                catch
                {
                }

                _log = null;
            }
        }
    }
}
