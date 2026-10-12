using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Wizardry.Desktop.Services;
using Wizardry.Desktop.ViewModels;
using Wizardry.Desktop.Views;

namespace Wizardry.Desktop;

public partial class App : Application
{
    /// <summary>Tests may set this before the app starts to isolate saves and settings.</summary>
    public static AppServices? ServicesOverride { get; set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var services = ServicesOverride ?? AppServices.CreateDefault(desktop.Args ?? []);
            desktop.Exit += (_, _) => services.Dispose();
            var main = new MainViewModel(services) { UiScale = services.Settings.UiScale };
            var window = new MainWindow { DataContext = main };
            main.ExitRequested += () => window.Close();
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
