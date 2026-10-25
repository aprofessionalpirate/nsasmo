using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SpideyNet.Client.Services;
using SpideyNet.Client.ViewModels;

namespace SpideyNet.Client;

internal sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Compose platform services at the application boundary, not inside the view model.
            var window = new MainWindow();
            window.DataContext = new MainWindowViewModel(new ClientService(), new ClipboardService(window));
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
