using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using JabraDesktop.App.ViewModels;
using JabraDesktop.Core;
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

    public async Task<bool> ConfirmUnpair(string name)
    {
        var texts = (DataContext as MainViewModel)?.Texts ?? new LocalizationService(UiLanguage.German);
        var dialog=new Window { Title=texts[UiText.UnpairWindowTitle],Width=430,SizeToContent=SizeToContent.Height,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner };
        var cancel=new Button{Content=texts[UiText.Cancel]}; var confirm=new Button{Content=texts[UiText.Unpair]};
        cancel.Click+=(_,_)=>dialog.Close(false); confirm.Click+=(_,_)=>dialog.Close(true);
        dialog.Content=new StackPanel { Margin=new Thickness(24),Spacing=20,Children=
        {
            new TextBlock{Text=texts.Format(UiText.UnpairQuestion, name),TextWrapping=Avalonia.Media.TextWrapping.Wrap,FontSize=18},
            new TextBlock{Text=texts[UiText.UnpairDescription],TextWrapping=Avalonia.Media.TextWrapping.Wrap},
            new StackPanel{Orientation=Orientation.Horizontal,Spacing=12,HorizontalAlignment=HorizontalAlignment.Right,Children={cancel,confirm}}
        }};
        return await dialog.ShowDialog<bool>(this);
    }

    public async Task<string?> PromptBluetoothName(string currentName)
    {
        var texts=(DataContext as MainViewModel)?.Texts ?? new LocalizationService(UiLanguage.German);
        var dialog=new Window { Title=texts[UiText.RenameBluetoothWindowTitle],Width=430,SizeToContent=SizeToContent.Height,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner };
        var input=new TextBox { Text=currentName,Watermark=texts[UiText.BluetoothNameLabel] };
        var cancel=new Button { Content=texts[UiText.Cancel] };
        var save=new Button { Content=texts[UiText.Save],IsEnabled=BluetoothNameRules.Normalize(currentName) is not null };
        input.TextChanged+=(_,_)=>save.IsEnabled=BluetoothNameRules.Normalize(input.Text) is not null;
        cancel.Click+=(_,_)=>dialog.Close(null);
        save.Click+=(_,_)=>dialog.Close(BluetoothNameRules.Normalize(input.Text));
        dialog.Content=new StackPanel { Margin=new Thickness(24),Spacing=16,Children=
        {
            new TextBlock { Text=texts[UiText.RenameBluetoothDescription],TextWrapping=Avalonia.Media.TextWrapping.Wrap },
            new TextBlock { Text=texts[UiText.BluetoothNameLabel] },
            input,
            new StackPanel { Orientation=Orientation.Horizontal,Spacing=12,HorizontalAlignment=HorizontalAlignment.Right,Children={cancel,save} }
        }};
        return await dialog.ShowDialog<string?>(this);
    }
}
