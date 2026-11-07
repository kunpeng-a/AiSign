using System;
using System.Diagnostics;
using System.IO;

namespace AiSign.Platforms
{
    internal static class BrowserLauncher
    {
        public static void OpenInPrivate(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                string[] candidates = new string[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\Edge\Application\msedge.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Google\Chrome\Application\chrome.exe")
                };
                for (int i = 0; i < candidates.Length; i++)
                {
                    if (!File.Exists(candidates[i])) continue;
                    string args = i < 2 ? "--inprivate \"" + url + "\"" : "--incognito \"" + url + "\"";
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = candidates[i];
                    psi.Arguments = args;
                    psi.UseShellExecute = false;
                    Process.Start(psi);
                    return;
                }
            }
            catch { }
            try { Process.Start(url); } catch { }
        }
    }
}
