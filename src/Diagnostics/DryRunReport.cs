using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using AiSign.Core;
using AiSign.Platforms;

namespace AiSign.Diagnostics
{
    internal static class DryRunReport
    {
        public static int Run()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("AiSign dry-run " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            int failed = 0;
            try
            {
                List<AccountDraft> list = PlatformRegistry.ScanAll();
                for (int i = 0; i < list.Count; i++)
                {
                    AccountDraft d = list[i];
                    if (d.Secret == null)
                    {
                        report.AppendLine("SKIP " + d.Platform + " " + Hash.Mask(d.Uid, 6, 4) + " (no secret)");
                        continue;
                    }
                    Account a = new Account();
                    a.Id = Hash.AccountId(d.Platform, string.IsNullOrEmpty(d.Uid) ? d.Name : d.Uid);
                    a.Platform = d.Platform;
                    a.Uid = d.Uid;
                    a.Name = d.Name;
                    a.Enabled = true;
                    ICheckinPlatform p = PlatformRegistry.Get(d.Platform);
                    CheckinOutcome o = null;
                    try { o = p.Run(a, d.Secret, null, true); }
                    catch (Exception ex) { o = CheckinOutcome.Make(OutcomeKind.Transient, -3, "异常：" + ex.Message); }
                    report.AppendLine(d.Platform + " " + Hash.Mask(d.Uid, 6, 4) + " -> " +
                        (o == null ? "null" : o.Kind + " code=" + o.Code + " credits=" + o.Credits + " " + o.Message));
                    if (o == null || o.Kind == OutcomeKind.AuthRequired || o.Kind == OutcomeKind.Transient)
                        failed++;
                }
            }
            catch (Exception ex)
            {
                report.AppendLine("ERROR " + ex);
                failed++;
            }
            report.AppendLine("RESULT failed=" + failed);
            string path = Path.Combine(Path.GetTempPath(), "aisign_dryrun.log");
            try { File.WriteAllText(path, report.ToString(), new UTF8Encoding(false)); } catch { }
            return failed == 0 ? 0 : 1;
        }
    }
}
