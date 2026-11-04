using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using CorelDRAWTool.Core.Features;
using CorelDRAWTool.Core.Models;
using CorelDRAWTool.Corel;

namespace CorelDRAWTool.Host.ViewModels
{
    public sealed class MainViewModel : ObservableObject, IDisposable
    {
        private static readonly TimeSpan AttachTimeout = TimeSpan.FromSeconds(5);

        private ICorelDrawSession _session;
        private FeatureDescriptor _selectedFeature;
        private ApplicationSummary _applicationInfo;
        private DocumentSummary _documentInfo = DocumentSummary.None;
        private string _statusText = "正在连接 CorelDRAW…";
        private bool _isConnected;

        public MainViewModel(string initialFeatureId)
        {
            Features = new ObservableCollection<FeatureDescriptor>(FeatureCatalog.All);
            BatchExport = new BatchExportViewModel();
            _selectedFeature = FeatureCatalog.Find(initialFeatureId) ?? FeatureCatalog.Default;
            RefreshCommand = new RelayCommand(Refresh);
            InsertTestRectangleCommand = new RelayCommand(InsertTestRectangle, () => IsConnected);
        }

        public ObservableCollection<FeatureDescriptor> Features { get; }

        public BatchExportViewModel BatchExport { get; }

        public FeatureDescriptor SelectedFeature
        {
            get { return _selectedFeature; }
            set
            {
                if (SetProperty(ref _selectedFeature, value))
                {
                    OnPropertyChanged(nameof(IsDocumentInfoSelected));
                    OnPropertyChanged(nameof(IsBatchExportSelected));
                    OnPropertyChanged(nameof(IsPlaceholderVisible));
                    OnPropertyChanged(nameof(PlaceholderTitle));
                    OnPropertyChanged(nameof(PlaceholderText));
                }
            }
        }

        public ApplicationSummary ApplicationInfo
        {
            get { return _applicationInfo; }
            private set { SetProperty(ref _applicationInfo, value); }
        }

        public DocumentSummary DocumentInfo
        {
            get { return _documentInfo; }
            private set { SetProperty(ref _documentInfo, value); }
        }

        public string StatusText
        {
            get { return _statusText; }
            private set { SetProperty(ref _statusText, value); }
        }

        public bool IsConnected
        {
            get { return _isConnected; }
            private set
            {
                if (SetProperty(ref _isConnected, value))
                {
                    OnPropertyChanged(nameof(ConnectionLabel));
                    ((RelayCommand)InsertTestRectangleCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public string ConnectionLabel
        {
            get { return IsConnected ? "已连接 CorelDRAW" : "未连接"; }
        }

        public bool IsDocumentInfoSelected
        {
            get
            {
                return SelectedFeature != null &&
                       string.Equals(
                           SelectedFeature.Id,
                           FeatureCatalog.DocumentInfoId,
                           StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool IsBatchExportSelected
        {
            get
            {
                return SelectedFeature != null &&
                       string.Equals(
                           SelectedFeature.Id,
                           "batch-export",
                           StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool IsPlaceholderVisible
        {
            get { return !IsDocumentInfoSelected && !IsBatchExportSelected; }
        }

        public string PlaceholderTitle
        {
            get { return SelectedFeature == null ? string.Empty : SelectedFeature.Title; }
        }

        public string PlaceholderText
        {
            get
            {
                return SelectedFeature == null
                    ? string.Empty
                    : string.Format("「{0}」正在规划中，后续版本会在这里实现。", SelectedFeature.Title);
            }
        }

        public ICommand RefreshCommand { get; }

        public ICommand InsertTestRectangleCommand { get; }

        public void Initialize()
        {
            Connect();
        }

        private void Connect()
        {
            if (_session != null)
            {
                return;
            }

            try
            {
                _session = CorelDrawSession.Attach(AttachTimeout);
                IsConnected = true;
                StatusText = "已连接到 CorelDRAW。";
                RefreshDocument();
            }
            catch (Exception ex)
            {
                IsConnected = false;
                StatusText = ex.Message;
                DocumentInfo = DocumentSummary.None;
                ApplicationInfo = null;
            }
        }

        private void Refresh()
        {
            if (!IsConnected)
            {
                Connect();
                return;
            }

            RefreshDocument();
        }

        private void RefreshDocument()
        {
            if (_session == null)
            {
                return;
            }

            try
            {
                ApplicationInfo = _session.GetApplicationSummary();
                DocumentInfo = _session.GetActiveDocumentSummary();
                StatusText = DocumentInfo.HasDocument
                    ? "文档信息已刷新。"
                    : "CorelDRAW 已连接，但当前没有打开的文档。";
            }
            catch (Exception ex)
            {
                IsConnected = false;
                StatusText = ex.Message;
                DocumentInfo = DocumentSummary.None;
            }
        }

        private void InsertTestRectangle()
        {
            if (_session == null)
            {
                return;
            }

            try
            {
                _session.CreateTestRectangle();
                StatusText = "已在当前文档中插入测试矩形。";
                RefreshDocument();
            }
            catch (Exception ex)
            {
                StatusText = ex.Message;
            }
        }

        public void Dispose()
        {
            BatchExport.Dispose();

            if (_session != null)
            {
                _session.Dispose();
                _session = null;
            }
        }
    }
}
