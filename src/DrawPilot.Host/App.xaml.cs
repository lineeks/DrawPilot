using System;
using System.Linq;
using System.Windows;

namespace DrawPilot.Host
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string commandId = null;
            var commandArgument = e.Args.FirstOrDefault(
                argument => argument.StartsWith("--command=", StringComparison.OrdinalIgnoreCase));

            if (commandArgument != null)
            {
                commandId = commandArgument.Substring("--command=".Length);
            }

            var window = new MainWindow(commandId);
            window.Show();
        }
    }
}
