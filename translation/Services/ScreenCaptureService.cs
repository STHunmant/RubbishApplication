namespace App1.Services;

public class ScreenCaptureInfo
{
    public System.Drawing.Bitmap FullBitmap { get; set; } = null!;
    public System.Drawing.Bitmap OcrBitmap { get; set; } = null!;
    public int ScreenLeft { get; set; }
    public int ScreenTop { get; set; }
    public double DpiScaleX { get; set; }
    public double DpiScaleY { get; set; }
    public double ScaleFactor { get; set; }
}

public class ScreenCaptureService
{
    public ScreenCaptureInfo CaptureScreen(double ocrScale = 0.5)
    {
        var virtualScreen = System.Windows.Forms.SystemInformation.VirtualScreen;
        int fullW = virtualScreen.Width;
        int fullH = virtualScreen.Height;

        var fullBitmap = new System.Drawing.Bitmap(fullW, fullH);
        using var graphics = System.Drawing.Graphics.FromImage(fullBitmap);
        graphics.CopyFromScreen(virtualScreen.Left, virtualScreen.Top, 0, 0,
            new System.Drawing.Size(fullW, fullH));

        double dpiScaleX = graphics.DpiX / 96.0;
        double dpiScaleY = graphics.DpiY / 96.0;

        int ocrW = (int)(fullW * ocrScale);
        int ocrH = (int)(fullH * ocrScale);

        var ocrBitmap = new System.Drawing.Bitmap(ocrW, ocrH);
        using var ocrGraphics = System.Drawing.Graphics.FromImage(ocrBitmap);
        ocrGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
        ocrGraphics.DrawImage(fullBitmap, 0, 0, ocrW, ocrH);

        return new ScreenCaptureInfo
        {
            FullBitmap = fullBitmap,
            OcrBitmap = ocrBitmap,
            ScreenLeft = virtualScreen.Left,
            ScreenTop = virtualScreen.Top,
            DpiScaleX = dpiScaleX,
            DpiScaleY = dpiScaleY,
            ScaleFactor = 1.0 / ocrScale
        };
    }
}
