using System;
using System.Diagnostics;
using System.Security.Principal;

namespace RussianCertRemover.Services
{
    public static class ElevationHelper
    {
        public static bool IsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static bool RestartAsAdministrator()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "",
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(processInfo);
                Environment.Exit(0);
                return true;
            }
            catch (Exception)
            {
                // User might have clicked "No" on UAC prompt
                return false;
            }
        }
    }
}
