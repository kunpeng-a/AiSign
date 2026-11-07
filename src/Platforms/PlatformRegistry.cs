using System;
using System.Collections.Generic;
using AiSign.Core;

namespace AiSign.Platforms
{
    internal static class PlatformRegistry
    {
        private static readonly ICheckinPlatform[] All =
        {
            new TraePlatform(),
            new WorkBuddyPlatform(),
            new QoderPlatform()
        };

        public static ICheckinPlatform Get(string id)
        {
            for (int i = 0; i < All.Length; i++)
                if (All[i].Id == id) return All[i];
            return null;
        }

        public static ICheckinPlatform[] Items { get { return All; } }

        public static List<AccountDraft> ScanAll()
        {
            List<AccountDraft> result = new List<AccountDraft>();
            for (int i = 0; i < All.Length; i++)
            {
                try
                {
                    List<AccountDraft> found = All[i].ScanLocal();
                    if (found != null) result.AddRange(found);
                }
                catch (Exception ex)
                {
                    Logger.Warn("scan", All[i].DisplayName + " 扫描失败: " + ex.Message);
                }
            }
            return result;
        }
    }
}
