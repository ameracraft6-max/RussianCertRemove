using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RussianCertRemover.Models;

namespace RussianCertRemover.Services
{
    public static class ReportGeneratorService
    {
        public static string GenerateHtmlReport(
            List<CertificateItem> certificates,
            List<StoreScanResult> stores,
            List<string> activeBrowsers,
            List<string> logs,
            int totalScannedCerts)
        {
            var sb = new StringBuilder();

            int foundCount = certificates.Count;
            int deletedCount = certificates.Count(c => c.Status == ItemStatus.Deleted);
            int failedCount = certificates.Count(c => c.Status == ItemStatus.Failed);
            int remainingCount = foundCount - deletedCount;

            string nowStr = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
            string machineName = Environment.MachineName;
            string userName = Environment.UserName;
            bool isAdmin = ElevationHelper.IsAdministrator();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"ru\">");
            sb.AppendLine("<head>");
            sb.AppendLine("<meta charset=\"UTF-8\">");
            sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine("<title>Отчёт: Очистка сертификатов Минцифры</title>");
            sb.AppendLine("<style>");
            sb.AppendLine(@"
                :root {
                    --bg-primary: #0b0f17;
                    --bg-secondary: #131b29;
                    --bg-card: #182234;
                    --border: #24344d;
                    --text-main: #f1f5f9;
                    --text-muted: #94a3b8;
                    --accent: #3b82f6;
                    --success: #10b981;
                    --danger: #ef4444;
                    --warning: #f59e0b;
                }
                * { box-sizing: border-box; margin: 0; padding: 0; }
                body {
                    font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
                    background-color: var(--bg-primary);
                    color: var(--text-main);
                    padding: 30px 20px;
                    line-height: 1.5;
                }
                .container {
                    max-width: 1100px;
                    margin: 0 auto;
                }
                .header {
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    border-bottom: 1px solid var(--border);
                    padding-bottom: 20px;
                    margin-bottom: 25px;
                }
                .logo-title {
                    display: flex;
                    align-items: center;
                    gap: 15px;
                }
                .shield-icon {
                    width: 48px;
                    height: 48px;
                    background: linear-gradient(135deg, #2563eb, #1d4ed8);
                    border-radius: 12px;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    font-size: 26px;
                    box-shadow: 0 4px 12px rgba(37, 99, 235, 0.3);
                }
                .title h1 {
                    font-size: 22px;
                    font-weight: 700;
                    letter-spacing: -0.5px;
                }
                .title p {
                    font-size: 13px;
                    color: var(--text-muted);
                }
                .meta-badge {
                    background: var(--bg-secondary);
                    border: 1px solid var(--border);
                    padding: 8px 16px;
                    border-radius: 8px;
                    font-size: 12px;
                    text-align: right;
                }
                .stats-grid {
                    display: grid;
                    grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
                    gap: 16px;
                    margin-bottom: 30px;
                }
                .stat-card {
                    background: var(--bg-card);
                    border: 1px solid var(--border);
                    border-radius: 12px;
                    padding: 20px;
                    display: flex;
                    flex-direction: column;
                    gap: 6px;
                    position: relative;
                    overflow: hidden;
                }
                .stat-card::before {
                    content: '';
                    position: absolute;
                    top: 0; left: 0; right: 0; height: 3px;
                    background: var(--accent);
                }
                .stat-card.danger::before { background: var(--danger); }
                .stat-card.success::before { background: var(--success); }
                .stat-card.warning::before { background: var(--warning); }
                .stat-card .label {
                    font-size: 13px;
                    color: var(--text-muted);
                    text-transform: uppercase;
                    letter-spacing: 0.5px;
                }
                .stat-card .value {
                    font-size: 28px;
                    font-weight: 700;
                }
                .section {
                    background: var(--bg-secondary);
                    border: 1px solid var(--border);
                    border-radius: 12px;
                    padding: 24px;
                    margin-bottom: 25px;
                }
                .section h2 {
                    font-size: 17px;
                    margin-bottom: 16px;
                    display: flex;
                    align-items: center;
                    gap: 8px;
                }
                table {
                    width: 100%;
                    border-collapse: collapse;
                    font-size: 13px;
                }
                th {
                    text-align: left;
                    padding: 12px;
                    background: var(--bg-card);
                    color: var(--text-muted);
                    font-weight: 600;
                    border-bottom: 1px solid var(--border);
                }
                td {
                    padding: 12px;
                    border-bottom: 1px solid var(--border);
                    vertical-align: middle;
                }
                tr:last-child td { border-bottom: none; }
                .badge {
                    display: inline-flex;
                    align-items: center;
                    padding: 4px 10px;
                    border-radius: 20px;
                    font-size: 11px;
                    font-weight: 600;
                }
                .badge-danger { background: rgba(239, 68, 68, 0.15); color: #f87171; border: 1px solid rgba(239, 68, 68, 0.3); }
                .badge-success { background: rgba(16, 185, 129, 0.15); color: #34d399; border: 1px solid rgba(16, 185, 129, 0.3); }
                .badge-warning { background: rgba(245, 158, 11, 0.15); color: #fbbf24; border: 1px solid rgba(245, 158, 11, 0.3); }
                .thumbprint {
                    font-family: 'Consolas', 'Courier New', monospace;
                    font-size: 11px;
                    color: var(--text-muted);
                    word-break: break-all;
                }
                .log-box {
                    background: #06090e;
                    border: 1px solid var(--border);
                    border-radius: 8px;
                    padding: 15px;
                    font-family: 'Consolas', monospace;
                    font-size: 12px;
                    max-height: 250px;
                    overflow-y: auto;
                    color: #94a3b8;
                    white-space: pre-wrap;
                }
                .recommendations {
                    background: rgba(59, 130, 246, 0.1);
                    border: 1px solid rgba(59, 130, 246, 0.3);
                    border-radius: 8px;
                    padding: 16px;
                    margin-top: 20px;
                }
                .recommendations ul {
                    margin-left: 20px;
                    margin-top: 8px;
                    font-size: 13px;
                }
                @media print {
                    body { background: #fff; color: #000; }
                    .section, .stat-card { background: #fff; border-color: #ccc; }
                    .log-box { background: #f8f9fa; color: #333; }
                }
            ");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<div class=\"container\">");

            // Header
            sb.AppendLine("<div class=\"header\">");
            sb.AppendLine("  <div class=\"logo-title\">");
            sb.AppendLine("    <div class=\"shield-icon\">🛡️</div>");
            sb.AppendLine("    <div class=\"title\">");
            sb.AppendLine("      <h1>Отчёт очистки сертификатов Минцифры</h1>");
            sb.AppendLine("      <p>Russian Trusted Root CA / Russian Trusted Sub CA Cleaner</p>");
            sb.AppendLine("    </div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("  <div class=\"meta-badge\">");
            sb.AppendLine($"    <div><strong>Дата:</strong> {nowStr}</div>");
            sb.AppendLine($"    <div><strong>ПК:</strong> {machineName} | <strong>Пользователь:</strong> {userName}</div>");
            sb.AppendLine($"    <div><strong>Режим:</strong> {(isAdmin ? "<span style='color:var(--success)'>Администратор ✓</span>" : "<span style='color:var(--warning)'>Обычный пользователь</span>")}</div>");
            sb.AppendLine("  </div>");
            sb.AppendLine("</div>");

            // Stats cards
            sb.AppendLine("<div class=\"stats-grid\">");
            sb.AppendLine("  <div class=\"stat-card\">");
            sb.AppendLine("    <span class=\"label\">Проверено объектов</span>");
            sb.AppendLine($"    <span class=\"value\">{totalScannedCerts}</span>");
            sb.AppendLine("  </div>");
            sb.AppendLine($"  <div class=\"stat-card {(foundCount > 0 ? "danger" : "success")}\">");
            sb.AppendLine("    <span class=\"label\">Найдено сертификатов</span>");
            sb.AppendLine($"    <span class=\"value\">{foundCount}</span>");
            sb.AppendLine("  </div>");
            sb.AppendLine($"  <div class=\"stat-card {(deletedCount > 0 ? "success" : "")}\">");
            sb.AppendLine("    <span class=\"label\">Успешно удалено</span>");
            sb.AppendLine($"    <span class=\"value\" style=\"color:var(--success)\">{deletedCount}</span>");
            sb.AppendLine("  </div>");
            sb.AppendLine($"  <div class=\"stat-card {(remainingCount > 0 ? "warning" : "success")}\">");
            sb.AppendLine("    <span class=\"label\">Статус системы</span>");
            sb.AppendLine($"    <span class=\"value\" style=\"font-size:20px;\">{(remainingCount == 0 ? "Безопасно ✓" : $"Осталось: {remainingCount}")}</span>");
            sb.AppendLine("  </div>");
            sb.AppendLine("</div>");

            // Section 1: Found & Deleted Certificates Table
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <h2>📋 Обнаруженные сертификаты Минцифры</h2>");
            if (certificates.Count == 0)
            {
                sb.AppendLine("  <p style=\"color:var(--success); font-weight:500;\">✓ В системе и браузерах не обнаружено сертификатов Russian Trusted Root CA / Минцифры.</p>");
            }
            else
            {
                sb.AppendLine("  <table>");
                sb.AppendLine("    <thead>");
                sb.AppendLine("      <tr>");
                sb.AppendLine("        <th>Статус</th>");
                sb.AppendLine("        <th>Имя сертификата</th>");
                sb.AppendLine("        <th>Тип</th>");
                sb.AppendLine("        <th>Хранилище / Источник</th>");
                sb.AppendLine("        <th>Отпечаток (SHA-1)</th>");
                sb.AppendLine("        <th>Срок действия</th>");
                sb.AppendLine("        <th>Результат</th>");
                sb.AppendLine("      </tr>");
                sb.AppendLine("    </thead>");
                sb.AppendLine("    <tbody>");

                foreach (var c in certificates)
                {
                    string badgeClass = c.Status switch
                    {
                        ItemStatus.Deleted => "badge-success",
                        ItemStatus.Detected => "badge-danger",
                        ItemStatus.Failed => "badge-warning",
                        _ => ""
                    };

                    sb.AppendLine("      <tr>");
                    sb.AppendLine($"        <td><span class=\"badge {badgeClass}\">{c.StatusDisplay}</span></td>");
                    sb.AppendLine($"        <td><strong>{c.CommonName}</strong></td>");
                    sb.AppendLine($"        <td>{c.CategoryDisplay}</td>");
                    sb.AppendLine($"        <td>{c.StoreLocationDisplay}<br><small style=\"color:var(--text-muted)\">{c.StoreName}</small></td>");
                    sb.AppendLine($"        <td class=\"thumbprint\">{c.Thumbprint}</td>");
                    sb.AppendLine($"        <td>{c.ValidityDisplay}</td>");
                    sb.AppendLine($"        <td>{c.StatusMessage}</td>");
                    sb.AppendLine("      </tr>");
                }

                sb.AppendLine("    </tbody>");
                sb.AppendLine("  </table>");
            }
            sb.AppendLine("</div>");

            // Section 2: Scanned stores overview
            sb.AppendLine("<div class=\"section\">");
            sb.AppendLine("  <h2>🌐 Просканированные хранилища и браузеры</h2>");
            sb.AppendLine("  <table>");
            sb.AppendLine("    <thead>");
            sb.AppendLine("      <tr>");
            sb.AppendLine("        <th>Хранилище / Браузер</th>");
            sb.AppendLine("        <th>Тип</th>");
            sb.AppendLine("        <th>Путь / Идентификатор</th>");
            sb.AppendLine("        <th>Сертификатов проверено</th>");
            sb.AppendLine("        <th>Найдено Минцифры</th>");
            sb.AppendLine("        <th>Статус</th>");
            sb.AppendLine("      </tr>");
            sb.AppendLine("    </thead>");
            sb.AppendLine("    <tbody>");

            foreach (var st in stores)
            {
                sb.AppendLine("      <tr>");
                sb.AppendLine($"        <td><strong>{st.StoreName}</strong></td>");
                sb.AppendLine($"        <td>{st.StoreType}</td>");
                sb.AppendLine($"        <td><small style=\"color:var(--text-muted)\">{st.LocationPath}</small></td>");
                sb.AppendLine($"        <td>{st.TotalScanned}</td>");
                sb.AppendLine($"        <td><strong>{st.FoundCount}</strong></td>");
                sb.AppendLine($"        <td><span style=\"color:{st.StatusColor}; font-weight:600;\">{st.StatusText}</span></td>");
                sb.AppendLine("      </tr>");
            }

            sb.AppendLine("    </tbody>");
            sb.AppendLine("  </table>");
            sb.AppendLine("</div>");

            // Recommendations
            sb.AppendLine("<div class=\"recommendations\">");
            sb.AppendLine("  <strong>💡 Рекомендации после очистки:</strong>");
            sb.AppendLine("  <ul>");
            if (activeBrowsers.Count > 0)
            {
                sb.AppendLine($"    <li>Перезапустите активные браузеры ({string.Join(", ", activeBrowsers)}), чтобы они обновили кэш TLS-сессий и сертификатов.</li>");
            }
            sb.AppendLine("    <li>Для ручной проверки вы можете нажать Win+R и ввести <code>certmgr.msc</code> (пользователь) или <code>certlm.msc</code> (компьютер).</li>");
            sb.AppendLine("    <li>Если вы используете Яндекс Браузер, помните, что он содержит собственные встроенные сертификаты Минцифры в дистрибутиве.</li>");
            sb.AppendLine("  </ul>");
            sb.AppendLine("</div>");

            // Section 3: Event Log
            if (logs.Count > 0)
            {
                sb.AppendLine("<div class=\"section\" style=\"margin-top:25px;\">");
                sb.AppendLine("  <h2>📜 Журнал операций</h2>");
                sb.AppendLine("  <div class=\"log-box\">");
                foreach (var log in logs)
                {
                    sb.AppendLine(log);
                }
                sb.AppendLine("  </div>");
                sb.AppendLine("</div>");
            }

            sb.AppendLine("</div>"); // container
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        public static string GenerateTextReport(
            List<CertificateItem> certificates,
            List<StoreScanResult> stores,
            List<string> activeBrowsers,
            List<string> logs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine("      ОТЧЕТ ПОИСКА И УДАЛЕНИЯ СЕРТИФИКАТОВ МИНЦИФРЫ (RUSSIAN CA REMOVER)        ");
            sb.AppendLine("================================================================================");
            sb.AppendLine($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
            sb.AppendLine($"Компьютер:         {Environment.MachineName}");
            sb.AppendLine($"Пользователь:      {Environment.UserName}");
            sb.AppendLine($"Права Admin:       {(ElevationHelper.IsAdministrator() ? "Да" : "Нет")}");
            sb.AppendLine("--------------------------------------------------------------------------------");
            sb.AppendLine($"Обнаружено сертификатов: {certificates.Count}");
            sb.AppendLine($"Успешно удалено:         {certificates.Count(c => c.Status == ItemStatus.Deleted)}");
            sb.AppendLine($"Ошибок / осталось:       {certificates.Count(c => c.Status != ItemStatus.Deleted)}");
            sb.AppendLine("================================================================================");
            sb.AppendLine();

            sb.AppendLine("СПИСОК НАЙДЕННЫХ СЕРТИФИКАТОВ:");
            if (certificates.Count == 0)
            {
                sb.AppendLine("  [✓] Сертификаты Минцифры не обнаружены.");
            }
            else
            {
                int index = 1;
                foreach (var c in certificates)
                {
                    sb.AppendLine($"[{index++}] {c.CommonName}");
                    sb.AppendLine($"    Статус:     {c.StatusDisplay} ({c.StatusMessage})");
                    sb.AppendLine($"    Тип:        {c.CategoryDisplay}");
                    sb.AppendLine($"    Хранилище:  {c.StoreLocationDisplay} / {c.StoreName}");
                    sb.AppendLine($"    Отпечаток:  {c.Thumbprint}");
                    sb.AppendLine($"    Действует:  {c.ValidityDisplay}");
                    sb.AppendLine($"    Субъект:    {c.Subject}");
                    sb.AppendLine($"    Издатель:   {c.Issuer}");
                    sb.AppendLine();
                }
            }

            sb.AppendLine("================================================================================");
            sb.AppendLine("ПРОВЕРЕННЫЕ ХРАНИЛИЩА И БРАУЗЕРЫ:");
            foreach (var st in stores)
            {
                sb.AppendLine($"  - {st.StoreName,-45} | Проверено: {st.TotalScanned,3} | Минцифры: {st.FoundCount,2} | {st.StatusText}");
            }

            if (activeBrowsers.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"АКТИВНЫЕ БРАУЗЕРЫ (ТРЕБУЕТСЯ ПЕРЕЗАПУСК): {string.Join(", ", activeBrowsers)}");
            }

            if (logs.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("ЖУРНАЛ ОПЕРАЦИЙ:");
                foreach (var log in logs)
                {
                    sb.AppendLine($"  {log}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("================================================================================");
            return sb.ToString();
        }
    }
}
