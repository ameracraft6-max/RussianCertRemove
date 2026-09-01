using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using RussianCertRemover.Models;

namespace RussianCertRemover.Services
{
    public class RemovalResult
    {
        public int TotalRequested { get; set; }
        public int SuccessfullyDeleted { get; set; }
        public int FailedCount { get; set; }
        public List<string> LogEntries { get; set; } = new();
    }

    public class CertificateRemoverService
    {
        public async Task<RemovalResult> RemoveCertificatesAsync(
            IEnumerable<CertificateItem> itemsToRemove,
            IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                var result = new RemovalResult();
                bool isAdmin = ElevationHelper.IsAdministrator();

                foreach (var item in itemsToRemove)
                {
                    result.TotalRequested++;
                    string logPrefix = $"[{DateTime.Now:HH:mm:ss}] {item.CommonName} ({item.StoreLocationDisplay}): ";

                    try
                    {
                        if (item.SourceType == CertSourceType.WindowsCurrentUser ||
                            item.SourceType == CertSourceType.WindowsLocalMachine)
                        {
                            progress?.Report($"Удаление из Windows: {item.CommonName}...");
                            RemoveFromWindowsStore(item, isAdmin, result, logPrefix);
                        }
                        else if (item.SourceType == CertSourceType.FirefoxProfile)
                        {
                            progress?.Report($"Удаление из профиля Firefox: {item.CommonName}...");
                            RemoveFromFirefoxProfile(item, result, logPrefix);
                        }
                    }
                    catch (Exception ex)
                    {
                        item.Status = ItemStatus.Failed;
                        item.StatusMessage = $"Ошибка удаления: {ex.Message}";
                        result.FailedCount++;
                        result.LogEntries.Add(logPrefix + $"ОШИБКА: {ex.Message}");
                    }
                }

                progress?.Report($"Операция завершена. Удалено: {result.SuccessfullyDeleted}, ошибок: {result.FailedCount}.");
                return result;
            });
        }

        private void RemoveFromWindowsStore(CertificateItem item, bool isAdmin, RemovalResult result, string logPrefix)
        {
            if (item.WinStoreLocation == StoreLocation.LocalMachine && !isAdmin)
            {
                item.Status = ItemStatus.Failed;
                item.StatusMessage = "Требуются права Администратора (LocalMachine)";
                result.FailedCount++;
                result.LogEntries.Add(logPrefix + "ОШИБКА: Доступ запрещен. Необходимы права Администратора Windows для очистки LocalMachine.");
                return;
            }

            using var store = new X509Store(item.WinStoreName, item.WinStoreLocation);
            try
            {
                store.Open(OpenFlags.ReadWrite);
            }
            catch (UnauthorizedAccessException)
            {
                item.Status = ItemStatus.Failed;
                item.StatusMessage = "Отказано в доступе к хранилищу";
                result.FailedCount++;
                result.LogEntries.Add(logPrefix + "ОШИБКА: Отказано в доступе при открытии хранилища на запись.");
                return;
            }

            var matchingCerts = store.Certificates.Find(
                X509FindType.FindByThumbprint,
                item.Thumbprint,
                validOnly: false
            );

            if (matchingCerts.Count == 0)
            {
                // Try finding by serial number
                matchingCerts = store.Certificates.Find(
                    X509FindType.FindBySerialNumber,
                    item.SerialNumber,
                    validOnly: false
                );
            }

            if (matchingCerts.Count == 0)
            {
                item.Status = ItemStatus.Deleted;
                item.StatusMessage = "Сертификат уже отсутствует в хранилище";
                result.SuccessfullyDeleted++;
                result.LogEntries.Add(logPrefix + "Сертификат уже не найден в хранилище (возможно, удалён ранее).");
                return;
            }

            foreach (var cert in matchingCerts)
            {
                store.Remove(cert);
            }

            // Verify removal
            var checkCerts = store.Certificates.Find(
                X509FindType.FindByThumbprint,
                item.Thumbprint,
                validOnly: false
            );

            if (checkCerts.Count == 0)
            {
                item.Status = ItemStatus.Deleted;
                item.StatusMessage = "Успешно удалён";
                result.SuccessfullyDeleted++;
                result.LogEntries.Add(logPrefix + "УСПЕШНО УДАЛЁН из Windows хранилища.");
            }
            else
            {
                item.Status = ItemStatus.Failed;
                item.StatusMessage = "Сертификат остался после попытки удаления";
                result.FailedCount++;
                result.LogEntries.Add(logPrefix + "ВНИМАНИЕ: Сертификат остался в хранилище после вызова Remove.");
            }
        }

        private void RemoveFromFirefoxProfile(CertificateItem item, RemovalResult result, string logPrefix)
        {
            if (item.NssId == null || item.NssId.Length == 0)
            {
                item.Status = ItemStatus.Failed;
                item.StatusMessage = "Отсутствует идентификатор записи NSS";
                result.FailedCount++;
                result.LogEntries.Add(logPrefix + "ОШИБКА: Нет ID сертификата в cert9.db.");
                return;
            }

            // Check if Firefox is running
            bool isFirefoxRunning = Process.GetProcessesByName("firefox").Length > 0 ||
                                    Process.GetProcessesByName("waterfox").Length > 0;

            if (isFirefoxRunning)
            {
                result.LogEntries.Add(logPrefix + "ПРЕДУПРЕЖДЕНИЕ: Браузер Firefox запущен. Файл cert9.db может быть заблокирован.");
            }

            try
            {
                // Create backup first
                WinSqlite.CreateBackup(item.SourcePath);
                result.LogEntries.Add(logPrefix + $"Создана резервная копия: {item.SourcePath}.bak");

                using var sqlite = new WinSqlite(item.SourcePath);
                bool deleted = sqlite.DeleteNssCertificate(item.NssId);

                if (deleted)
                {
                    item.Status = ItemStatus.Deleted;
                    item.StatusMessage = "Успешно удалён из cert9.db";
                    result.SuccessfullyDeleted++;
                    result.LogEntries.Add(logPrefix + "УСПЕШНО УДАЛЁН из профиля Mozilla (cert9.db).");
                }
                else
                {
                    item.Status = ItemStatus.Failed;
                    string errMsg = sqlite.GetLastError();
                    item.StatusMessage = $"Ошибка удаления из БД: {errMsg}";
                    result.FailedCount++;
                    result.LogEntries.Add(logPrefix + $"ОШИБКА удаления из SQLite: {errMsg}");
                }
            }
            catch (Exception ex)
            {
                item.Status = ItemStatus.Failed;
                item.StatusMessage = $"Исключение при работе с cert9.db: {ex.Message}";
                result.FailedCount++;
                result.LogEntries.Add(logPrefix + $"ОШИБКА: {ex.Message}");
            }
        }
    }
}
