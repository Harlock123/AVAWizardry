using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Wizardry.Desktop.ViewModels;

namespace Wizardry.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Tunnel so game keys are seen before focused buttons or text boxes act on them.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.HandleKey(e.Key)) e.Handled = true;
    }
}
