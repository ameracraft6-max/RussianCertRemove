using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RussianCertRemover
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += (s, args) =>
            {
                MessageBox.Show(
                    $"Произошла непредвиденная ошибка:\n{args.Exception.Message}",
                    "Ошибка приложения",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };

            if (e.Args.Length > 0 && e.Args[0] == "--screenshot")
            {
                try
                {
                    var win = new MainWindow(isDemo: true);
                    win.Show();
                    win.UpdateLayout();

                    int width = 1140;
                    int height = 740;

                    var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(win);

                    var enc = new PngBitmapEncoder();
                    enc.Frames.Add(BitmapFrame.Create(rtb));
                    string outPath = e.Args.Length > 1 ? e.Args[1] : "screenshot.png";
                    using (var fs = File.Create(outPath))
                    {
                        enc.Save(fs);
                    }
                }
                finally
                {
                    Shutdown(0);
                }
                return;
            }

            var mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}
