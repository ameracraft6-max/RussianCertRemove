using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RussianCertRemover.Models
{
    public class StoreScanResult : INotifyPropertyChanged
    {
        private int _foundCount;
        private string _statusText = "Готов";
        private string _statusColor = "#94A3B8";

        public string StoreName { get; set; } = string.Empty;
        public string StoreType { get; set; } = string.Empty;
        public string LocationPath { get; set; } = string.Empty;
        public int TotalScanned { get; set; }

        public int FoundCount
        {
            get => _foundCount;
            set
            {
                _foundCount = value;
                OnPropertyChanged();
                UpdateStatus();
            }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public string StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(); }
        }

        public void UpdateStatus(string? customStatus = null, string? customColor = null)
        {
            if (customStatus != null)
            {
                StatusText = customStatus;
                StatusColor = customColor ?? "#94A3B8";
                return;
            }

            if (FoundCount > 0)
            {
                StatusText = $"Найдено: {FoundCount}";
                StatusColor = "#EF4444";
            }
            else
            {
                StatusText = "Чисто ✓";
                StatusColor = "#10B981";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
