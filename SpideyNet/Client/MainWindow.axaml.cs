using Avalonia.Controls;
using Avalonia.Threading;
using SpideyNet.Client.ViewModels;

namespace SpideyNet.Client;

internal sealed partial class MainWindow : Window
{
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private bool closing;
    private bool closed;

    internal MainWindow()
    {
        InitializeComponent();
        if (Screens.Primary is { } screen)
        {
            // Work in logical pixels so a high-DPI display never opens an oversized window.
            var width = screen.WorkingArea.Width / screen.Scaling - 32;
            var height = screen.WorkingArea.Height / screen.Scaling - 48;
            MinWidth = Math.Min(MinWidth, width);
            MinHeight = Math.Min(MinHeight, height);
            Width = Math.Min(Width, width);
            Height = Math.Min(Height, height);
        }

        Opened += OnOpenedAsync;
        Closing += OnClosingAsync;
        refresh.Tick += OnRefreshAsync;
    }

    private async void OnOpenedAsync(object? sender, EventArgs args)
    {
        if (DataContext is MainWindowViewModel model)
        {
            await model.RefreshGamesCommand.ExecuteAsync(null);
            if (!closing)
            {
                refresh.Start();
            }
        }
    }

    private async void OnRefreshAsync(object? sender, EventArgs args)
    {
        // The view owns UI cadence; the view model owns all session state and commands.
        refresh.Stop();
        if (DataContext is MainWindowViewModel model)
        {
            await model.RefreshAsync();
        }

        if (!closing)
        {
            refresh.Start();
        }
    }

    private async void OnClosingAsync(object? sender, WindowClosingEventArgs args)
    {
        if (closed)
        {
            return;
        }

        args.Cancel = true;
        if (closing)
        {
            return;
        }

        // Await the same cleanup as Disconnect before allowing the native window to close.
        closing = true;
        refresh.Stop();
        if (DataContext is MainWindowViewModel model)
        {
            await model.ShutdownAsync();
            if (model.IsAttached)
            {
                closing = false;
                refresh.Start();
                return;
            }
        }

        closed = true;
        refresh.Tick -= OnRefreshAsync;
        Dispatcher.UIThread.Post(Close);
    }
}
