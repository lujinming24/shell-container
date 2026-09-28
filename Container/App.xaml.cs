using System.Windows;
namespace Container
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            new ShellWindow().Show();
        }
    }
}
