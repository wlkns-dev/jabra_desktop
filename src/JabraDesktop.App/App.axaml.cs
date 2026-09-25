using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using JabraDesktop.App.ViewModels;
using JabraDesktop.App.Views;
using JabraDesktop.Core;
using JabraDesktop.Jabra;
namespace JabraDesktop.App;
public class App : Application
{
    public override void Initialize()=>AvaloniaXamlLoader.Load(this);
    public override void OnFrameworkInitializationCompleted()
    {
        if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var session=new DeviceSession(new JabraBackend());
            var vm=new MainViewModel(session,a=>Dispatcher.UIThread.Post(a));
            var window=new MainWindow{DataContext=vm};
            vm.ConfirmUnpair=window.ConfirmUnpair;
            desktop.MainWindow=window;
            var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(4)};
            timer.Tick+=async (_,_)=> { if(vm.CanScan) await vm.RefreshAsync(); };
            window.Opened+=async (_,_)=> { await vm.StartAsync(); timer.Start(); };
            window.Closed+=async (_,_)=> { timer.Stop(); vm.Dispose(); await session.DisposeAsync(); };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
