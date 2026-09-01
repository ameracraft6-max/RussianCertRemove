using System;
using System.Windows;

namespace RussianCertRemover
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show(
                    $"Произошла непредвиденная ошибка:\n{args.Exception.Message}",
                    "Ошибка приложения",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };

            base.OnStartup(e);
        }
    }
}
