using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using CorelDRAWTool.Core.Batch;
using CorelDRAWTool.Core.Models;
using CorelDRAWTool.Corel;

namespace CorelDRAWTool.Host.ViewModels
{
    public sealed class BatchExportViewModel : ObservableObject, IDisposable
    {
        private static readonly TimeSpan AttachTimeout = TimeSpan.FromSeconds(10);

        private readonly RelayCommand _startCommand;
        private readonly RelayCommand _cancelCommand;
        private readonly RelayCommand _browseInputCommand;
        private readonly RelayCommand _browseOutputCommand;

        private string _inputFolder;
        private string _outputFolder;
        private bool _overwriteExisting;
        private bool _isRunning;
        private string _statusText = "选择输入文件夹和输出文件夹，然后开始导出。";
        private double _progressValue;
        private double _progressMaximum = 1;
        private CancellationTokenSource _cancelSource;
        private Thread _worker;
        private int _successCount;
        private int _failureCount;
        private int _skippedCount;

        public BatchExportViewModel()
        {
            Files = new ObservableCollection<BatchExportFileItemViewModel>();
            _startCommand = new RelayCommand(Start, () => !IsRunning);
            _cancelCommand = new RelayCommand(Cancel, () => IsRunning);
            _browseInputCommand = new RelayCommand(BrowseInput, () => !IsRunning);
            _browseOutputCommand = new RelayCommand(BrowseOutput, () => !IsRunning);
        }

        public ObservableCollection<BatchExportFileItemViewModel> Files { get; }

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
                dialog.Description = "选择 PDF 输出文件夹";
                dialog.SelectedPath = Directory.Exists(OutputFolder) ? OutputFolder : string.Empty;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    OutputFolder = dialog.SelectedPath;
                }
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

            BatchExportPlan plan;
            try
            {
                plan = BatchExportPlanner.CreatePlan(
                    new BatchExportRequest(
                        input,
                        output,
                        BatchExportFormat.Pdf,
                        OverwriteExisting));
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
                return;
            }

            StatusText = string.Format(
                "准备导出 {0} 个文件，跳过 {1} 个。",
                plan.Items.Count,
                plan.SkippedCount);

            IsRunning = true;
            _cancelSource = new CancellationTokenSource();
            var progress = new Progress<BatchExportProgress>(OnProgress);

            _worker = new Thread(() => RunBatch(plan, progress, _cancelSource.Token));
            _worker.SetApartmentState(ApartmentState.STA);
            _worker.IsBackground = true;
            _worker.Start();
        }

        private void RunBatch(
            BatchExportPlan plan,
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
                    progress.Report(BatchExportProgress.Started(i, completed, plan.Items.Count, fileName));

                    try
                    {
                        session.ExportDocumentToPdf(item.SourcePath, item.OutputPath);
                        completed++;
                        progress.Report(BatchExportProgress.Succeeded(
                            i,
                            completed,
                            plan.Items.Count,
                            fileName));
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
                    StatusText = string.Format(
                        "正在导出 {0}（{1}/{2}）…",
                        progress.FileName,
                        progress.ItemIndex + 1,
                        progress.Total);
                    break;

                case BatchExportProgressKind.Succeeded:
                    _successCount++;
                    UpdateFile(progress.ItemIndex, "成功", "已生成 PDF");
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

            if (_cancelSource != null)
            {
                _cancelSource.Dispose();
                _cancelSource = null;
            }

            _worker = null;
        }

        private void Cancel()
        {
            if (!IsRunning || _cancelSource == null)
            {
                return;
            }

            StatusText = "正在取消，当前文件完成后停止…";
            _cancelSource.Cancel();
        }

        public void Dispose()
        {
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
        }
    }
}
