using System.Windows;

namespace ClipboardServer
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private Server server;
        protected override void OnStartup(StartupEventArgs e)
        {
            server = new Server();
            base.OnStartup(e);
        }
    }
}
