using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Mater2026.Services
{
    public static class ToastService
    {
        public static void Show(string message, int ms = 1800)
        {
            try
            {
                var w = new Window
                {
                    Width = 360,
                    Height = 48,
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = System.Windows.Media.Brushes.Transparent,
                    Topmost = true,
                    ShowInTaskbar = false
                };

                var border = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 30, 30, 30)),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 90, 90, 90)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(12)
                };
                border.Child = new TextBlock
                {
                    Text = message,
                    Foreground = System.Windows.Media.Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center
                };
                w.Content = border;

                var wa = SystemParameters.WorkArea;
                w.Left = wa.Right - w.Width - 16;
                w.Top = wa.Top + 16;

                w.Show();

                var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
                t.Tick += (_, __) => { t.Stop(); w.Close(); };
                t.Start();
            }
            catch { /* ignore */ }
        }
    }
}