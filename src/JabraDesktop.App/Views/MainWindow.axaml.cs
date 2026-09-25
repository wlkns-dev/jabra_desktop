using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Styling;
using JabraDesktop.App.ViewModels;
namespace JabraDesktop.App.Views;
public partial class MainWindow : Window
{
    public MainWindow() { InitializeComponent(); }

    public void HideToTray() => Hide();

    public void ShowAndActivate()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    public void ToggleTheme(object? sender,RoutedEventArgs e)
    {
        if(Application.Current is {} app) app.RequestedThemeVariant=app.ActualThemeVariant==ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
    }
    public async Task<bool> ConfirmUnpair(string name)
    {
        var dialog=new Window { Title="Gerät entkoppeln",Width=430,SizeToContent=SizeToContent.Height,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner };
        var cancel=new Button{Content="Abbrechen"}; var confirm=new Button{Content="Entkoppeln"};
        cancel.Click+=(_,_)=>dialog.Close(false); confirm.Click+=(_,_)=>dialog.Close(true);
        dialog.Content=new StackPanel { Margin=new Thickness(24),Spacing=20,Children=
        {
            new TextBlock{Text=$"„{name}“ wirklich entkoppeln?",TextWrapping=Avalonia.Media.TextWrapping.Wrap,FontSize=18},
            new TextBlock{Text="Die gespeicherte Kopplung wird vom Dongle entfernt. Zum erneuten Verbinden ist Pairing nötig.",TextWrapping=Avalonia.Media.TextWrapping.Wrap},
            new StackPanel{Orientation=Orientation.Horizontal,Spacing=12,HorizontalAlignment=HorizontalAlignment.Right,Children={cancel,confirm}}
        }};
        return await dialog.ShowDialog<bool>(this);
    }
}
