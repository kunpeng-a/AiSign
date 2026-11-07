using System;
using System.Threading;
using System.Windows;
using AiSign.Core;

namespace AiSign
{
    internal static class Program
    {
        [STAThread]
        internal static int Main(string[] args)
        {
            bool startMinimized = false;
            bool selfTest = false;
            bool uiCheck = false;
            bool scan = false;
            bool dryRun = false;
            bool topmost = false;
            string dataDir = null;
            string page = null;

            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i] == null ? string.Empty : args[i].ToLowerInvariant();
                if (a == "--minimized" || a == "-m") startMinimized = true;
                else if (a == "--selftest" || a == "-t") selfTest = true;
                else if (a == "--uicheck") uiCheck = true;
                else if (a == "--scan") scan = true;
                else if (a == "--dryrun") dryRun = true;
                else if (a == "--topmost") topmost = true;
                else if (a == "--data-dir" && i + 1 < args.Length) { dataDir = args[i + 1]; i++; }
                else if (a == "--page" && i + 1 < args.Length) { page = args[i + 1]; i++; }
            }

            if (!string.IsNullOrEmpty(dataDir)) AppPaths.SetDataDir(dataDir);

            if (selfTest) return Diagnostics.SelfTest.RunAll();
            if (scan) return Diagnostics.ScanReport.Run();
            if (dryRun) return Diagnostics.DryRunReport.Run();

            AppPaths.Ensure();
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                Exception ex = e.ExceptionObject as Exception;
                Logger.Error("crash", "未处理异常", ex);
            };

            SingleInstance instance = SingleInstance.Acquire();
            if (!instance.IsFirst)
            {
                instance.SignalExisting();
                instance.Dispose();
                return 0;
            }

            try
            {
                Application app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                app.DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
                {
                    Logger.Error("crash", "UI 未处理异常", e.Exception);
                    e.Handled = true;
                };

                AppHost host = new AppHost(app, startMinimized, uiCheck);
                host.AttachSingleInstance(instance);
                host.Start();
                if (!string.IsNullOrEmpty(page)) host.NavigateTo(page);
                if (topmost && host.Window != null) host.Window.Topmost = true;

                if (uiCheck)
                {
                    int rc = Diagnostics.UiCheck.Run(host);
                    host.Exit();
                    return rc;
                }
                return app.Run();
            }
            catch (Exception ex)
            {
                Logger.Error("startup", "启动失败", ex);
                try
                {
                    System.IO.File.WriteAllText(System.IO.Path.Combine(AppPaths.DataDir, "startup_error.txt"), ex.ToString());
                }
                catch { }
                return 1;
            }
            finally
            {
                instance.Dispose();
            }
        }
    }
}
