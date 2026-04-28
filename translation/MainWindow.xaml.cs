using System.Windows;
using System.Windows.Media;
using App1.Services;

namespace App1;

public partial class MainWindow : System.Windows.Window
{
    private OverlayManager? _overlayManager;
    private bool _isRunning;

    public MainWindow()
    {
        InitializeComponent();
        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        if (screen != null)
        {
            Left = screen.WorkingArea.Left + 20;
            Top = screen.WorkingArea.Top + 20;
        }
    }

    private async void ToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            await StopAsync();
        }
        else
        {
            await StartAsync();
        }
    }

    private async Task StartAsync()
    {
        try
        {
            _overlayManager = new OverlayManager();
            await _overlayManager.StartAsync();
            _isRunning = true;
            ToggleButton.Content = "关闭";
            if (ToggleButton.Template.FindName("border", ToggleButton) is System.Windows.Controls.Border border)
            {
                border.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xD4, 0x3B, 0x00));
            }
        }
        catch (Exception ex)
        {
            System.Windows.Forms.MessageBox.Show($"启动失败: {ex.Message}", "错误",
                System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
        }
    }

    private async Task StopAsync()
    {
        if (_overlayManager != null)
        {
            await _overlayManager.StopAsync();
            _overlayManager = null;
        }
        _isRunning = false;
        ToggleButton.Content = "打开";
        if (ToggleButton.Template.FindName("border", ToggleButton) is System.Windows.Controls.Border border)
        {
            border.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4));
        }
    }

    protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_isRunning)
        {
            await StopAsync();
        }
        base.OnClosing(e);
    }
}
