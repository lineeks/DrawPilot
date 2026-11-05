using System;
using System.Windows;
using DrawPilot.Host.ViewModels;

namespace DrawPilot.Host
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
            : this(null)
        {
        }

        public MainWindow(string commandId)
        {
            InitializeComponent();

            _viewModel = new MainViewModel(commandId);
            DataContext = _viewModel;

            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _viewModel.Initialize();
        }

        private void OnClosed(object sender, EventArgs e)
        {
            _viewModel.Dispose();
        }
    }
}
