using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AiSign.Core;
using AiSign.Platforms;

namespace AiSign.Diagnostics
{
    internal static class ScanReport
    {
        public static int Run()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("AiSign scan " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            try
            {
                List<AccountDraft> list = PlatformRegistry.ScanAll();
                report.AppendLine("found=" + list.Count);
                for (int i = 0; i < list.Count; i++)
                {
                    AccountDraft d = list[i];
                    report.AppendLine("----");
                    report.AppendLine("platform=" + d.Platform);
                    report.AppendLine("name=" + (d.Name == null ? string.Empty : d.Name));
                    report.AppendLine("uid=" + Hash.Mask(d.Uid, 6, 4));
                    report.AppendLine("source=" + d.Source);
                    report.AppendLine("hasSecret=" + (d.Secret != null));
                    if (!string.IsNullOrEmpty(d.Warning)) report.AppendLine("warning=" + d.Warning);
                }
            }
            catch (Exception ex)
            {
                report.AppendLine("ERROR " + ex);
            }
            string path = Path.Combine(Path.GetTempPath(), "aisign_scan.log");
            try { File.WriteAllText(path, report.ToString(), new UTF8Encoding(false)); } catch { }
            return 0;
        }
    }
}
