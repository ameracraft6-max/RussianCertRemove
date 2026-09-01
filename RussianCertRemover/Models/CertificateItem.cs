using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;

namespace RussianCertRemover.Models
{
    public enum CertSourceType
    {
        WindowsCurrentUser,
        WindowsLocalMachine,
        FirefoxProfile,
        ChromiumBrowser
    }

    public enum CertCategory
    {
        RootCA,
        SubCA,
        Personal,
        Other
    }

    public enum ItemStatus
    {
        Detected,
        Deleted,
        Failed,
        Skipped
    }

    public class CertificateItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        private ItemStatus _status = ItemStatus.Detected;
        private string _statusMessage = "Обнаружен";

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public ItemStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusDisplay)); OnPropertyChanged(nameof(StatusBadgeColor)); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public string StatusDisplay => Status switch
        {
            ItemStatus.Detected => "🔴 Обнаружен",
            ItemStatus.Deleted => "🟢 Удалён",
            ItemStatus.Failed => "⚠️ Ошибка",
            ItemStatus.Skipped => "⚪ Пропущен",
            _ => "Неизвестно"
        };

        public string StatusBadgeColor => Status switch
        {
            ItemStatus.Detected => "#EF4444",
            ItemStatus.Deleted => "#10B981",
            ItemStatus.Failed => "#F59E0B",
            ItemStatus.Skipped => "#6B7280",
            _ => "#9CA3AF"
        };

        public string CommonName { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string IssuerCommonName { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;
        public string Thumbprint { get; set; } = string.Empty;
        public DateTime NotBefore { get; set; }
        public DateTime NotAfter { get; set; }
        public bool IsExpired => DateTime.Now > NotAfter;

        public CertSourceType SourceType { get; set; }
        public string StoreLocationDisplay { get; set; } = string.Empty;
        public string StoreName { get; set; } = string.Empty;
        public string SourcePath { get; set; } = string.Empty;
        public CertCategory Category { get; set; }

        public string CategoryDisplay => Category switch
        {
            CertCategory.RootCA => "Корневой CA (Root)",
            CertCategory.SubCA => "Подчиненный CA (Sub)",
            CertCategory.Personal => "Персональный",
            _ => "Сертификат Минцифры"
        };

        // For Windows store
        public StoreLocation WinStoreLocation { get; set; }
        public StoreName WinStoreName { get; set; }
        public X509Certificate2? RawCertificate { get; set; }

        // For Firefox NSS cert9.db
        public byte[]? NssId { get; set; }
        public string? NssNickname { get; set; }

        public string ValidityDisplay => $"{NotBefore:dd.MM.yyyy} — {NotAfter:dd.MM.yyyy}" + (IsExpired ? " (Истёк)" : "");

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
