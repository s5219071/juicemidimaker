using System.ComponentModel;
using System.Windows;
using JuiceMidiMaker.ViewModels;

namespace JuiceMidiMaker.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        _viewModel.NotificationRequested += OnNotificationRequested;
        DataContext = _viewModel;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _viewModel.NotificationRequested -= OnNotificationRequested;
        _viewModel.Dispose();
        base.OnClosing(e);
    }

    private void OnNotificationRequested(string message, bool isError)
    {
        System.Windows.MessageBox.Show(
            this,
            message,
            isError ? "JuiceMidiMaker - Error" : "JuiceMidiMaker",
            MessageBoxButton.OK,
            isError ? MessageBoxImage.Error : MessageBoxImage.Information);
    }
}
