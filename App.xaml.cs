using Microsoft.UI.Xaml;
namespace MyProgress;
public partial class App : Application
{
    Window? w;
    public App() { InitializeComponent(); Store.Init(); }
    protected override void OnLaunched(LaunchActivatedEventArgs a) { w = new MainWindow(); w.Activate(); }
}
