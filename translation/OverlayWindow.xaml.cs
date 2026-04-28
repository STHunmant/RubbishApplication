using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace App1;

public partial class OverlayWindow : Window
{
    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    public OverlayWindow()
    {
        InitializeComponent();
    }

    public void ShowOverlay()
    {
        var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;

        using var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
        double dpiScaleX = g.DpiX / 96.0;
        double dpiScaleY = g.DpiY / 96.0;

        Left = virtualScreen.Left / dpiScaleX;
        Top = virtualScreen.Top / dpiScaleY;
        Width = virtualScreen.Width / dpiScaleX;
        Height = virtualScreen.Height / dpiScaleY;

        Show();
        MakeClickThrough();
    }

    public void HideOverlay()
    {
        OverlayCanvas.Children.Clear();
        Hide();
    }

    private void MakeClickThrough()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
        {
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE,
                extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        }
    }

    public void UpdateOverlays(List<OverlayItem> items)
    {
        Dispatcher.Invoke(() =>
        {
            OverlayCanvas.Children.Clear();

            foreach (var item in items)
            {
                var border = new Border
                {
                    Background = new SolidColorBrush(item.MaskColor),
                    CornerRadius = new CornerRadius(3),
                    Width = item.Width,
                    MinHeight = item.Height,
                };

                var textBlock = new TextBlock
                {
                    Text = item.ChineseText,
                    Foreground = new SolidColorBrush(item.TextColor),
                    FontSize = item.FontSize,
                    FontWeight = FontWeights.Bold,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = item.Width - 4,
                    TextTrimming = TextTrimming.None,
                    Margin = new Thickness(4, 1, 4, 1),
                };

                border.Child = textBlock;

                Canvas.SetLeft(border, item.X);
                Canvas.SetTop(border, item.Y);

                OverlayCanvas.Children.Add(border);
            }
        });
    }
}

public class OverlayItem
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public required string ChineseText { get; set; }
    public System.Windows.Media.Color MaskColor { get; set; }
    public System.Windows.Media.Color TextColor { get; set; }
    public double FontSize { get; set; }
}
