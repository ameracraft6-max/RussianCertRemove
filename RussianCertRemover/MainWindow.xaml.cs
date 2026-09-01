using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using RussianCertRemover.Models;
using RussianCertRemover.Services;

namespace RussianCertRemover
{
    public partial class MainWindow : Window
    {
        private readonly CertificateScannerService _scannerService = new();
        private readonly CertificateRemoverService _removerService = new();

        private readonly ObservableCollection<CertificateItem> _allCertificates = new();
        private readonly ObservableCollection<CertificateItem> _filteredCertificates = new();
        private readonly ObservableCollection<StoreScanResult> _stores = new();

        private List<string> _activeBrowsers = new();
        private readonly List<string> _logEntries = new();
        private int _totalScannedCerts = 0;
        private bool _isScanning = false;

        public MainWindow()
        {
            InitializeComponent();

            GridCertificates.ItemsSource = _filteredCertificates;
            GridStores.ItemsSource = _stores;

            InitializeAdminStatus();
            Log("Russian CA Remover инициализирован.");

            Loaded += async (s, e) =>
            {
                await RunScanAsync();
            };
        }

        private void InitializeAdminStatus()
        {
            bool isAdmin = ElevationHelper.IsAdministrator();
            if (isAdmin)
            {
                AdminStatusText.Text = "🛡️ Администратор";
                AdminStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4ADE80"));
                AdminStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#14532D"));
                AdminStatusBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#166534"));
                BtnRestartAdmin.Visibility = Visibility.Collapsed;
            }
            else
            {
                AdminStatusText.Text = "⚠️ Пользователь (Ограничено)";
                AdminStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBBF24"));
                AdminStatusBadge.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#451A03"));
                AdminStatusBadge.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#78350F"));
                BtnRestartAdmin.Visibility = Visibility.Visible;
            }
        }

        private void BtnRestartAdmin_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show(
                "Перезапустить программу с правами Администратора?\n\nЭто необходимо для удаления сертификатов из системного хранилища LocalMachine.",
                "Повышение привилегий",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (res == MessageBoxResult.Yes)
            {
                ElevationHelper.RestartAsAdministrator();
            }
        }

        private async void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            await RunScanAsync();
        }

        private async Task RunScanAsync()
        {
            if (_isScanning) return;
            _isScanning = true;

            BtnScan.IsEnabled = false;
            BtnDeleteSelected.IsEnabled = false;
            PrgProgress.Visibility = Visibility.Visible;
            PrgProgress.IsIndeterminate = true;

            Log("=== Запуск сканирования системы и браузеров ===");

            var progress = new Progress<string>(msg =>
            {
                TxtStatus.Text = msg;
            });

            try
            {
                var report = await _scannerService.ScanAllAsync(progress);

                _allCertificates.Clear();
                foreach (var cert in report.FoundCertificates)
                {
                    _allCertificates.Add(cert);
                }

                _stores.Clear();
                foreach (var st in report.ScannedStores)
                {
                    _stores.Add(st);
                }

                _activeBrowsers = report.ActiveBrowsers;
                _totalScannedCerts = report.TotalCertsChecked;

                ApplyFilter();
                UpdateMetrics();

                // Update active browsers label
                if (_activeBrowsers.Count > 0)
                {
                    TxtActiveBrowsersList.Text = string.Join(", ", _activeBrowsers);
                    TxtActiveBrowsersList.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FBBF24"));
                }
                else
                {
                    TxtActiveBrowsersList.Text = "не обнаружено";
                    TxtActiveBrowsersList.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#94A3B8"));
                }

                // Update empty state vs datagrid
                if (_allCertificates.Count == 0)
                {
                    EmptyStatePanel.Visibility = Visibility.Visible;
                    GridCertificates.Visibility = Visibility.Collapsed;

                    EmptyStateIcon.Text = "🎉";
                    EmptyStateTitle.Text = "Сертификаты Минцифры не найдены!";
                    EmptyStateDesc.Text = "В вашей операционной системе Windows и в проверенных браузерах сертификаты Russian Trusted Root CA и Минцифры отсутствуют. Ваша система чиста.";
                }
                else
                {
                    EmptyStatePanel.Visibility = Visibility.Collapsed;
                    GridCertificates.Visibility = Visibility.Visible;
                }

                TabCerts.Header = $"📋 Найденные сертификаты ({_allCertificates.Count})";
                TabStores.Header = $"🌐 Хранилища и браузеры ({_stores.Count})";

                Log($"Сканирование завершено. Проверено объектов: {_totalScannedCerts}. Найдено сертификатов Минцифры: {_allCertificates.Count}.");
                TxtStatus.Text = $"Сканирование завершено. Найдено сертификатов: {_allCertificates.Count}.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка во время сканирования: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Log($"ОШИБКА сканирования: {ex.Message}");
                TxtStatus.Text = "Ошибка сканирования.";
            }
            finally
            {
                _isScanning = false;
                BtnScan.IsEnabled = true;
                BtnDeleteSelected.IsEnabled = _allCertificates.Any(c => c.Status == ItemStatus.Detected);
                PrgProgress.Visibility = Visibility.Collapsed;
                PrgProgress.IsIndeterminate = false;
            }
        }

        private async void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = _filteredCertificates
                .Where(c => c.IsSelected && c.Status != ItemStatus.Deleted)
                .ToList();

            if (selectedItems.Count == 0)
            {
                MessageBox.Show(
                    "Отметьте галочками сертификаты, которые хотите удалить (или все они уже удалены).",
                    "Нет выбранных сертификатов",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var confirmResult = MessageBox.Show(
                $"Вы действительно хотите полностью удалить {selectedItems.Count} сертификат(ов) Минцифры?\n\n" +
                string.Join("\n", selectedItems.Take(5).Select(s => $"• {s.CommonName} ({s.StoreLocationDisplay})")) +
                (selectedItems.Count > 5 ? $"\n... и ещё {selectedItems.Count - 5} серт." : "") +
                "\n\nЭто действие отзовёт доверие к российским сертификатам в Windows и браузерах.",
                "Подтверждение удаления",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmResult != MessageBoxResult.Yes) return;

            await DeleteItemsAsync(selectedItems);
        }

        private async void BtnDeleteSingle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CertificateItem item)
            {
                if (item.Status == ItemStatus.Deleted)
                {
                    MessageBox.Show("Этот сертификат уже удалён.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var res = MessageBox.Show(
                    $"Удалить сертификат '{item.CommonName}' из '{item.StoreLocationDisplay}'?",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (res == MessageBoxResult.Yes)
                {
                    await DeleteItemsAsync(new[] { item });
                }
            }
        }

        private async Task DeleteItemsAsync(IEnumerable<CertificateItem> items)
        {
            BtnScan.IsEnabled = false;
            BtnDeleteSelected.IsEnabled = false;
            PrgProgress.Visibility = Visibility.Visible;
            PrgProgress.IsIndeterminate = true;

            var progress = new Progress<string>(msg =>
            {
                TxtStatus.Text = msg;
            });

            Log("=== Запуск процедуры удаления сертификатов ===");

            try
            {
                var result = await _removerService.RemoveCertificatesAsync(items, progress);

                foreach (var log in result.LogEntries)
                {
                    Log(log);
                }

                UpdateMetrics();

                string completionMsg = $"Удаление завершено.\n\nУспешно удалено: {result.SuccessfullyDeleted}\nОшибок: {result.FailedCount}";
                if (_activeBrowsers.Count > 0)
                {
                    completionMsg += $"\n\nВнимание: Рекомендуется перезапустить браузеры ({string.Join(", ", _activeBrowsers)}) для применения изменений.";
                }

                var askReport = MessageBox.Show(
                    completionMsg + "\n\nОткрыть подробный HTML-отчёт?",
                    "Результат операции",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (askReport == MessageBoxResult.Yes)
                {
                    OpenHtmlReport();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Log($"ОШИБКА: {ex.Message}");
            }
            finally
            {
                BtnScan.IsEnabled = true;
                BtnDeleteSelected.IsEnabled = _allCertificates.Any(c => c.Status == ItemStatus.Detected);
                PrgProgress.Visibility = Visibility.Collapsed;
                PrgProgress.IsIndeterminate = false;
                TxtStatus.Text = "Операция удаления завершена.";
            }
        }

        private void UpdateMetrics()
        {
            LblTotalStores.Text = _stores.Count.ToString();
            LblTotalCertsScanned.Text = $" ({_totalScannedCerts} серт.)";
            LblFoundCerts.Text = _allCertificates.Count.ToString();

            int deletedCount = _allCertificates.Count(c => c.Status == ItemStatus.Deleted);
            LblDeletedCerts.Text = deletedCount.ToString();

            int remaining = _allCertificates.Count - deletedCount;
            if (_allCertificates.Count == 0)
            {
                LblSystemStatus.Text = "Безопасно ✓ (Чисто)";
                LblSystemStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            }
            else if (remaining == 0)
            {
                LblSystemStatus.Text = "Очищено ✓ (Все удалены)";
                LblSystemStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            }
            else
            {
                LblSystemStatus.Text = $"Внимание: {remaining} серт.!";
                LblSystemStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF4444"));
            }
        }

        private void ApplyFilter()
        {
            string query = TxtFilter.Text.Trim();
            TxtFilterPlaceholder.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;

            _filteredCertificates.Clear();
            foreach (var cert in _allCertificates)
            {
                if (string.IsNullOrEmpty(query) ||
                    cert.CommonName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    cert.Thumbprint.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    cert.StoreLocationDisplay.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    cert.Subject.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    _filteredCertificates.Add(cert);
                }
            }
        }

        private void TxtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ChkSelectAll_Checked(object sender, RoutedEventArgs e)
        {
            foreach (var cert in _filteredCertificates)
            {
                cert.IsSelected = true;
            }
        }

        private void ChkSelectAll_Unchecked(object sender, RoutedEventArgs e)
        {
            foreach (var cert in _filteredCertificates)
            {
                cert.IsSelected = false;
            }
        }

        private void BtnCertDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is CertificateItem item)
            {
                DlgCertCategory.Text = item.CategoryDisplay;
                DlgCommonName.Text = item.CommonName;
                DlgThumbprint.Text = item.Thumbprint;
                DlgSerialNumber.Text = item.SerialNumber;
                DlgValidity.Text = item.ValidityDisplay;
                DlgStorePath.Text = $"{item.StoreLocationDisplay} ({item.StoreName})";
                DlgSubject.Text = item.Subject;
                DlgIssuer.Text = item.Issuer;
                DlgReason.Text = item.StatusMessage;

                ModalDetailsOverlay.Visibility = Visibility.Visible;
            }
        }

        private void BtnCloseModal_Click(object sender, RoutedEventArgs e)
        {
            ModalDetailsOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnExportReport_Click(object sender, RoutedEventArgs e)
        {
            OpenHtmlReport();
        }

        private void BtnOpenHtmlReport_Click(object sender, RoutedEventArgs e)
        {
            OpenHtmlReport();
        }

        private void OpenHtmlReport()
        {
            try
            {
                string html = ReportGeneratorService.GenerateHtmlReport(
                    _allCertificates.ToList(),
                    _stores.ToList(),
                    _activeBrowsers,
                    _logEntries,
                    _totalScannedCerts
                );

                string tempPath = Path.Combine(Path.GetTempPath(), $"RussianCA_Report_{DateTime.Now:yyyyMMdd_HHmmss}.html");
                File.WriteAllText(tempPath, html, System.Text.Encoding.UTF8);

                Process.Start(new ProcessStartInfo(tempPath) { UseShellExecute = true });
                Log($"HTML отчёт сформирован и открыт: {tempPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть отчёт: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnSaveReportFile_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new SaveFileDialog
            {
                Title = "Сохранить отчёт",
                Filter = "HTML Отчёт (*.html)|*.html|Текстовый файл (*.txt)|*.txt",
                FileName = $"RussianCA_Report_{DateTime.Now:yyyyMMdd_HHmmss}.html"
            };

            if (sfd.ShowDialog() == true)
            {
                try
                {
                    string ext = Path.GetExtension(sfd.FileName).ToLowerInvariant();
                    string content;

                    if (ext == ".txt")
                    {
                        content = ReportGeneratorService.GenerateTextReport(
                            _allCertificates.ToList(),
                            _stores.ToList(),
                            _activeBrowsers,
                            _logEntries
                        );
                    }
                    else
                    {
                        content = ReportGeneratorService.GenerateHtmlReport(
                            _allCertificates.ToList(),
                            _stores.ToList(),
                            _activeBrowsers,
                            _logEntries,
                            _totalScannedCerts
                        );
                    }

                    File.WriteAllText(sfd.FileName, content, System.Text.Encoding.UTF8);
                    MessageBox.Show($"Отчёт успешно сохранён:\n{sfd.FileName}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    Log($"Отчёт сохранён в файл: {sfd.FileName}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnCopyTextReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string text = ReportGeneratorService.GenerateTextReport(
                    _allCertificates.ToList(),
                    _stores.ToList(),
                    _activeBrowsers,
                    _logEntries
                );

                Clipboard.SetText(text);
                MessageBox.Show("Текстовый отчёт скопирован в буфер обмена!", "Скопировано", MessageBoxButton.OK, MessageBoxImage.Information);
                Log("Текстовый отчёт скопирован в буфер обмена.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось скопировать отчёт: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Log(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            _logEntries.Add(line);
            TxtLogOutput.AppendText(line + "\n");
            TxtLogOutput.ScrollToEnd();
        }
    }
}