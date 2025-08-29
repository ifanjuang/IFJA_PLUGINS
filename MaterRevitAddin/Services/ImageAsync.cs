using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Mater2026.Services
{
    /// <summary>
    /// Attached property to load images off the UI thread:
    /// <Image svc:ImageAsync.AsyncSourcePath="{Binding ThumbPath}" />
    /// Cancels the previous load when the path changes.
    /// </summary>
    public static class ImageAsync
    {
        // string path to load
        public static readonly DependencyProperty AsyncSourcePathProperty =
            DependencyProperty.RegisterAttached(
                "AsyncSourcePath",
                typeof(string),
                typeof(ImageAsync),
                new PropertyMetadata(null, OnAsyncSourcePathChanged));

        public static void SetAsyncSourcePath(System.Windows.Controls.Image obj, string? value)
            => obj.SetValue(AsyncSourcePathProperty, value);

        public static string? GetAsyncSourcePath(System.Windows.Controls.Image obj)
            => (string?)obj.GetValue(AsyncSourcePathProperty);

        // per-Image CTS to cancel previous load
        private static readonly DependencyProperty LoadCtsProperty =
            DependencyProperty.RegisterAttached(
                "LoadCts",
                typeof(CancellationTokenSource),
                typeof(ImageAsync),
                new PropertyMetadata(null));

        private static void SetLoadCts(DependencyObject obj, CancellationTokenSource? value)
            => obj.SetValue(LoadCtsProperty, value);

        private static CancellationTokenSource? GetLoadCts(DependencyObject obj)
            => (CancellationTokenSource?)obj.GetValue(LoadCtsProperty);

        private static void OnAsyncSourcePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not System.Windows.Controls.Image img) return;

            // cancel previous
            var oldCts = GetLoadCts(img);
            if (oldCts != null)
            {
                try { oldCts.Cancel(); } catch { }
                oldCts.Dispose();
                SetLoadCts(img, null);
            }

            var path = e.NewValue as string;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                img.Source = null;
                return;
            }

            var cts = new CancellationTokenSource();
            SetLoadCts(img, cts);
            var token = cts.Token;

            // clear current image quickly
            img.Source = null;

            _ = Task.Run(() =>
            {
                try
                {
                    // Load fully into memory (OnLoad) so we can close the file quickly
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri(path, UriKind.Absolute);
                    bi.EndInit();
                    bi.Freeze();

                    if (token.IsCancellationRequested) return;

                    img.Dispatcher.Invoke(() =>
                    {
                        // still the active CTS?
                        if (GetLoadCts(img) == cts)
                            img.Source = bi;
                    });
                }
                catch
                {
                    // swallow – you can also toast here if you want
                }
            }, token);
        }
    }
}
