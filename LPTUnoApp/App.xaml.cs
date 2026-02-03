using System;
using System.IO;
using System.Threading.Tasks;
using System.Security.Principal;
using System.Windows;

namespace LPTUnoApp
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            string startupLogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LPT-UNO", "startup_debug.log");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(startupLogPath)!);
                bool isElevated = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
                File.AppendAllText(startupLogPath, $"{DateTime.Now:O} App.OnStartup. User={Environment.UserName} Elevated={isElevated} Args={string.Join(' ', e.Args)}\n");

                // Show messagebox to make startup visible for debugging
                System.Windows.MessageBox.Show($"LPT-UNO App starting\nUser: {Environment.UserName}\nElevated: {isElevated}\n\nIf this window does not appear when you run the EXE, the process likely exits before startup.\nLog: {startupLogPath}", "LPT-UNO Debug Startup", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(startupLogPath, $"{DateTime.Now:O} OnStartup exception: {ex}\n"); } catch { }
            }

            AppDomain.CurrentDomain.UnhandledException += (s, ex) => { try { File.AppendAllText(startupLogPath, $"{DateTime.Now:O} UnhandledException: {ex}\n"); } catch { } };
            this.DispatcherUnhandledException += (s, ex) => { try { File.AppendAllText(startupLogPath, $"{DateTime.Now:O} DispatcherUnhandledException: {ex.Exception}\n"); } catch { } };
            TaskScheduler.UnobservedTaskException += (s, ex) => { try { File.AppendAllText(startupLogPath, $"{DateTime.Now:O} TaskSchedulerException: {ex.Exception}\n"); } catch { } };
        }
    }
}