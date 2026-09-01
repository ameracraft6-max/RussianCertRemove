using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using RussianCertRemover.Models;

namespace RussianCertRemover.Services
{
    public class ScanReport
    {
        public List<CertificateItem> FoundCertificates { get; set; } = new();
        public List<StoreScanResult> ScannedStores { get; set; } = new();
        public List<string> ActiveBrowsers { get; set; } = new();
        public int TotalCertsChecked { get; set; }
        public DateTime ScanTime { get; set; } = DateTime.Now;
    }

    public class CertificateScannerService
    {
        private static readonly (StoreLocation Location, StoreName Name, string DisplayName)[] WindowsStoresToScan =
        {
            (StoreLocation.CurrentUser, StoreName.Root, "Windows CurrentUser → Доверенные корневые центры (Root)"),
            (StoreLocation.CurrentUser, StoreName.CertificateAuthority, "Windows CurrentUser → Промежуточные центры (CA)"),
            (StoreLocation.CurrentUser, StoreName.My, "Windows CurrentUser → Личные сертификаты (My)"),
            (StoreLocation.CurrentUser, StoreName.AuthRoot, "Windows CurrentUser → Сторонние корневые центры (AuthRoot)"),
            (StoreLocation.CurrentUser, StoreName.AddressBook, "Windows CurrentUser → Другие пользователи (AddressBook)"),

            (StoreLocation.LocalMachine, StoreName.Root, "Windows LocalMachine → Доверенные корневые центры (Root)"),
            (StoreLocation.LocalMachine, StoreName.CertificateAuthority, "Windows LocalMachine → Промежуточные центры (CA)"),
            (StoreLocation.LocalMachine, StoreName.My, "Windows LocalMachine → Личные сертификаты (My)"),
            (StoreLocation.LocalMachine, StoreName.AuthRoot, "Windows LocalMachine → Сторонние корневые центры (AuthRoot)"),
            (StoreLocation.LocalMachine, StoreName.AddressBook, "Windows LocalMachine → Другие пользователи (AddressBook)"),
        };

        public async Task<ScanReport> ScanAllAsync(IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                var report = new ScanReport();

                // 1. Detect active browser processes
                progress?.Report("Проверка активных браузеров...");
                report.ActiveBrowsers = DetectActiveBrowsers();

                // 2. Scan Windows Certificate Stores
                foreach (var (loc, name, disp) in WindowsStoresToScan)
                {
                    progress?.Report($"Сканирование: {disp}...");
                    var storeResult = new StoreScanResult
                    {
                        StoreName = disp,
                        StoreType = loc == StoreLocation.CurrentUser ? "Windows CurrentUser" : "Windows LocalMachine",
                        LocationPath = $"Cert:\\{loc}\\{name}"
                    };

                    try
                    {
                        using var store = new X509Store(name, loc);
                        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

                        storeResult.TotalScanned = store.Certificates.Count;
                        report.TotalCertsChecked += store.Certificates.Count;

                        foreach (var cert in store.Certificates)
                        {
                            if (CertificateRules.IsRussianMintsifryCert(cert, out string reason, out CertCategory category))
                            {
                                var item = CreateFromX509(cert, loc, name, disp, reason, category);
                                report.FoundCertificates.Add(item);
                                storeResult.FoundCount++;
                            }
                        }

                        storeResult.UpdateStatus();
                    }
                    catch (Exception ex)
                    {
                        storeResult.UpdateStatus($"Ошибка доступа: {ex.Message}", "#F59E0B");
                    }

                    report.ScannedStores.Add(storeResult);
                }

                // 3. Scan Mozilla / Firefox profiles
                progress?.Report("Поиск профилей Firefox и Mozilla...");
                ScanMozillaProfiles(report, progress);

                // 4. Summarize Chromium browser integration
                AddChromiumBrowserEntries(report);

                progress?.Report("Сканирование завершено.");
                return report;
            });
        }

        private CertificateItem CreateFromX509(
            X509Certificate2 cert,
            StoreLocation loc,
            StoreName name,
            string storeDisplay,
            string detectionReason,
            CertCategory category)
        {
            string cn = CertificateRules.ExtractCommonName(cert.Subject);
            string issuerCn = CertificateRules.ExtractCommonName(cert.Issuer);

            return new CertificateItem
            {
                CommonName = cn,
                Subject = cert.Subject,
                Issuer = cert.Issuer,
                IssuerCommonName = issuerCn,
                SerialNumber = cert.SerialNumber,
                Thumbprint = cert.Thumbprint?.Replace(" ", "") ?? "",
                NotBefore = cert.NotBefore,
                NotAfter = cert.NotAfter,
                SourceType = loc == StoreLocation.CurrentUser ? CertSourceType.WindowsCurrentUser : CertSourceType.WindowsLocalMachine,
                StoreLocationDisplay = loc == StoreLocation.CurrentUser ? "Windows: Текущий пользователь" : "Windows: Локальный компьютер",
                StoreName = $"{name} ({storeDisplay})",
                SourcePath = $"Cert:\\{loc}\\{name}",
                Category = category,
                WinStoreLocation = loc,
                WinStoreName = name,
                RawCertificate = cert,
                StatusMessage = detectionReason
            };
        }

        private void ScanMozillaProfiles(ScanReport report, IProgress<string>? progress)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var mozillaBaseDirs = new[]
            {
                Path.Combine(appData, "Mozilla", "Firefox", "Profiles"),
                Path.Combine(appData, "Waterfox", "Profiles"),
                Path.Combine(appData, "LibreWolf", "Profiles"),
                Path.Combine(appData, "floorp", "Profiles"),
                Path.Combine(appData, "Thunderbird", "Profiles")
            };

            foreach (var baseDir in mozillaBaseDirs)
            {
                if (!Directory.Exists(baseDir)) continue;

                string clientName = new DirectoryInfo(Path.GetDirectoryName(baseDir) ?? baseDir).Name;

                try
                {
                    var profileDirs = Directory.GetDirectories(baseDir);
                    foreach (var pDir in profileDirs)
                    {
                        string profileName = Path.GetFileName(pDir);
                        string certDbPath = Path.Combine(pDir, "cert9.db");

                        if (!File.Exists(certDbPath)) continue;

                        progress?.Report($"Проверка профиля {clientName}: {profileName}...");

                        var storeResult = new StoreScanResult
                        {
                            StoreName = $"{clientName} ({profileName})",
                            StoreType = "Mozilla cert9.db",
                            LocationPath = certDbPath
                        };

                        try
                        {
                            using var sqlite = new WinSqlite(certDbPath);
                            var rows = sqlite.ReadNssCertificates();
                            storeResult.TotalScanned = rows.Count;
                            report.TotalCertsChecked += rows.Count;

                            foreach (var row in rows)
                            {
                                try
                                {
                                    using var x509 = new X509Certificate2(row.CertDer);
                                    if (CertificateRules.IsRussianMintsifryCert(x509, out string reason, out CertCategory category))
                                    {
                                        string cn = CertificateRules.ExtractCommonName(x509.Subject);
                                        string issuerCn = CertificateRules.ExtractCommonName(x509.Issuer);

                                        var item = new CertificateItem
                                        {
                                            CommonName = !string.IsNullOrWhiteSpace(row.Nickname) ? row.Nickname : cn,
                                            Subject = x509.Subject,
                                            Issuer = x509.Issuer,
                                            IssuerCommonName = issuerCn,
                                            SerialNumber = x509.SerialNumber,
                                            Thumbprint = x509.Thumbprint?.Replace(" ", "") ?? "",
                                            NotBefore = x509.NotBefore,
                                            NotAfter = x509.NotAfter,
                                            SourceType = CertSourceType.FirefoxProfile,
                                            StoreLocationDisplay = $"{clientName} Профиль: {profileName}",
                                            StoreName = "cert9.db (nssCertificates)",
                                            SourcePath = certDbPath,
                                            Category = category,
                                            NssId = row.Id,
                                            NssNickname = row.Nickname,
                                            StatusMessage = reason
                                        };

                                        report.FoundCertificates.Add(item);
                                        storeResult.FoundCount++;
                                    }
                                }
                                catch
                                {
                                    // Skip unparseable cert blobs
                                }
                            }

                            storeResult.UpdateStatus();
                        }
                        catch (Exception ex)
                        {
                            storeResult.UpdateStatus($"Ошибка чтения: {ex.Message}", "#F59E0B");
                        }

                        report.ScannedStores.Add(storeResult);
                    }
                }
                catch { }
            }
        }

        private void AddChromiumBrowserEntries(ScanReport report)
        {
            var chromiumBrowsers = new (string Name, string ExeName)[]
            {
                ("Google Chrome", "chrome"),
                ("Microsoft Edge", "msedge"),
                ("Яндекс Браузер", "browser"),
                ("Brave", "brave"),
                ("Opera", "opera"),
                ("Vivaldi", "vivaldi")
            };

            foreach (var (name, exe) in chromiumBrowsers)
            {
                bool isRunning = report.ActiveBrowsers.Contains(name);
                var entry = new StoreScanResult
                {
                    StoreName = $"{name} {(isRunning ? "(Запущен)" : "(Использует системное хранилище Windows)")}",
                    StoreType = "Chromium CAPI",
                    LocationPath = "Синхронизируется с хранилищем Windows",
                    TotalScanned = report.ScannedStores.Where(s => s.StoreType.StartsWith("Windows")).Sum(s => s.TotalScanned),
                    FoundCount = report.ScannedStores.Where(s => s.StoreType.StartsWith("Windows")).Sum(s => s.FoundCount)
                };

                entry.UpdateStatus(
                    isRunning ? "Активен (требуется перезапуск после очистки)" : "Защищён системным хранилищем",
                    isRunning ? "#3B82F6" : "#10B981"
                );

                report.ScannedStores.Add(entry);
            }
        }

        public List<string> DetectActiveBrowsers()
        {
            var list = new List<string>();
            var targets = new (string ProcessName, string DisplayName)[]
            {
                ("chrome", "Google Chrome"),
                ("msedge", "Microsoft Edge"),
                ("browser", "Яндекс Браузер"),
                ("firefox", "Mozilla Firefox"),
                ("waterfox", "Waterfox"),
                ("brave", "Brave"),
                ("opera", "Opera"),
                ("vivaldi", "Vivaldi")
            };

            foreach (var (proc, disp) in targets)
            {
                try
                {
                    if (Process.GetProcessesByName(proc).Length > 0)
                    {
                        list.Add(disp);
                    }
                }
                catch { }
            }

            return list;
        }
    }
}
