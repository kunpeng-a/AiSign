using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AiSign.Core;
using AiSign.UI;

namespace AiSign.Diagnostics
{
    internal static class UiCheck
    {
        public static int Run(AppHost host)
        {
            StringBuilder report = new StringBuilder();
            try
            {
                if (host.Window == null) throw new Exception("window is null");
                RenderWindow(host, "accounts");
                host.Window.NavigateTo("checkin");
                RenderWindow(host, "checkin");
                host.Window.NavigateTo("settings");
                RenderWindow(host, "settings");

                AddAccountDialog dlg = new AddAccountDialog(null, host.Controller, "scan");
                dlg.Measure(new Size(760, 620));
                dlg.Arrange(new Rect(0, 0, 760, 620));
                RenderElement(dlg.Content as FrameworkElement, "add", 760, 620);

                AddAccountDialog qoderOauth = new AddAccountDialog(null, host.Controller, "oauth", PlatformIds.Qoder);
                qoderOauth.Measure(new Size(760, 620));
                qoderOauth.Arrange(new Rect(0, 0, 760, 620));
                RenderElement(qoderOauth.Content as FrameworkElement, "qoder_oauth", 760, 620);

                report.AppendLine("[PASS] shell + pages + add dialog + qoder oauth + render");
                Write(report);
                return 0;
            }
            catch (Exception ex)
            {
                report.AppendLine("[FAIL] " + ex.GetType().Name + ": " + ex.Message);
                report.AppendLine(ex.StackTrace);
                Write(report);
                return 1;
            }
        }

        private static void RenderWindow(AppHost host, string name)
        {
            host.Window.Measure(new Size(1080, 720));
            host.Window.Arrange(new Rect(0, 0, 1080, 720));
            RenderElement(host.Window.Content as FrameworkElement, name, 1080, 720);
        }

        private static void RenderElement(FrameworkElement root, string name, int w, int h)
        {
            if (root == null) throw new Exception("content is not a FrameworkElement");
            root.Measure(new Size(w, h));
            root.Arrange(new Rect(0, 0, w, h));
            root.UpdateLayout();
            RenderTargetBitmap bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(root);
            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            string path = Path.Combine(Path.GetTempPath(), "aisign_ui_" + name + ".png");
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                enc.Save(fs);
            }
        }

        private static void Write(StringBuilder report)
        {
            try
            {
                string path = Path.Combine(Path.GetTempPath(), "aisign_uicheck.log");
                File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
