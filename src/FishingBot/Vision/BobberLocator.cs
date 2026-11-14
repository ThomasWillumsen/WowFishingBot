using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using DeskHelper.Infrastructure;
using DeskHelper.Windowing;

namespace DeskHelper.Vision;

/// <summary>Where the bobber is on screen (center, in screen coordinates) and roughly how big it is.</summary>
internal readonly record struct BobberInfo(Point Center, double Radius);

/// <summary>Finds the fishing bobber by looking for reddish pixels in the area just above the screen center.</summary>
internal sealed class BobberLocator(WowWindow window)
{
    // the capture area is a fraction of the client height, so it adapts to any resolution/aspect ratio
    private const double CaptureWidthToClientHeight = 0.625;
    private const double CaptureHeightToClientHeight = 0.35;

    private const int MaxBlobPixels = 5000;

    private const string LogDirectory = "logs";

    private readonly record struct Blob(Point Center, double Radius);

    /// <summary>
    /// Looks for the bobber and returns its center (in screen coordinates) and approximate radius, or null if none was found.
    /// A failed search can optionally save the capture to the logs folder for inspection.
    /// </summary>
    public BobberInfo? Find(bool saveFailedCapture = false)
    {
        var client = window.GetClientAreaOnScreen();
        var captureSize = new Size(
            (int)(client.Height * CaptureWidthToClientHeight),
            (int)(client.Height * CaptureHeightToClientHeight));

        var center = new Point(client.X + client.Width / 2, client.Y + client.Height / 2);
        // horizontally centered, vertically above the center: the bottom edge ends at the center of the game window
        var captureOrigin = new Point(center.X - captureSize.Width / 2, center.Y - captureSize.Height);

        Log.Debug($"Capturing {captureSize.Width}x{captureSize.Height} at ({captureOrigin.X}, {captureOrigin.Y}) around window center ({center.X}, {center.Y})");

        using var screenshot = Capture(captureOrigin, captureSize);

        if (TryFindBobber(screenshot, out var blob, out var hue, out var saturation, out var brightness))
        {
            var screenCenter = new Point(captureOrigin.X + blob.Center.X, captureOrigin.Y + blob.Center.Y);
            Log.Info($"Possible fish bobber located at ({screenCenter.X}, {screenCenter.Y}), radius ~{blob.Radius:F0}px, seed HSL: ({hue}, {saturation}, {brightness})");

            if (Log.IsDebugEnabled)
                SaveSuccessScreenshot(screenshot, blob.Center);

            return new BobberInfo(screenCenter, blob.Radius);
        }

        if (saveFailedCapture)
        {
            Directory.CreateDirectory(LogDirectory);
            screenshot.Save(Path.Combine(LogDirectory, $"failed_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png"), ImageFormat.Png);
        }

        return null;
    }

    /// <summary>A click position on the bobber: its center plus a small random offset.</summary>
    public static Point PickClickPoint(BobberInfo bobber)
    {
        var limit = bobber.Radius * 0.6;
        var x = Math.Clamp(RandomDelay.Gaussian(0, bobber.Radius * 0.25), -limit, limit);
        var y = Math.Clamp(RandomDelay.Gaussian(0, bobber.Radius * 0.25), -limit, limit);
        return new Point(bobber.Center.X + (int)Math.Round(x), bobber.Center.Y + (int)Math.Round(y));
    }

    private static Bitmap Capture(Point origin, Size size)
    {
        var screenshot = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(screenshot);
        graphics.CopyFromScreen(origin, Point.Empty, size, CopyPixelOperation.SourceCopy);
        return screenshot;
    }

    // scans bottom-up (the bobber is closest to the center), left to right, then measures the connected red area around the first hit
    private static bool TryFindBobber(Bitmap bitmap, out Blob blob, out float hue, out float saturation, out float brightness)
    {
        var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var pixels = new byte[data.Stride * data.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

            for (var y = data.Height - 1; y >= 0; y--)
            {
                for (var x = 0; x < data.Width; x++)
                {
                    if (IsReddish(pixels, y * data.Stride + x * 4, out hue, out saturation, out brightness))
                    {
                        blob = MeasureBlob(pixels, data.Stride, data.Width, data.Height, new Point(x, y));
                        return true;
                    }
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        blob = default;
        hue = saturation = brightness = 0;
        return false;
    }

    private static bool IsReddish(byte[] pixels, int offset, out float hue, out float saturation, out float brightness)
    {
        // BGRA
        var color = Color.FromArgb(pixels[offset + 2], pixels[offset + 1], pixels[offset]);

        hue = color.GetHue();
        saturation = color.GetSaturation();
        brightness = color.GetBrightness();

        return (hue > 345 || hue < 15) && saturation > 0.5;
    }

    /// <summary>Flood-fills the connected reddish pixels around <paramref name="seed"/> and returns their centroid and approximate radius.</summary>
    private static Blob MeasureBlob(byte[] pixels, int stride, int width, int height, Point seed)
    {
        var visited = new bool[width * height];
        var queue = new Queue<Point>();
        queue.Enqueue(seed);
        visited[seed.Y * width + seed.X] = true;

        long sumX = 0, sumY = 0;
        var count = 0;

        while (count < MaxBlobPixels && queue.TryDequeue(out var p))
        {
            count++;
            sumX += p.X;
            sumY += p.Y;

            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var nx = p.X + dx;
                    var ny = p.Y + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height || visited[ny * width + nx])
                        continue;

                    visited[ny * width + nx] = true;
                    if (IsReddish(pixels, ny * stride + nx * 4, out _, out _, out _))
                        queue.Enqueue(new Point(nx, ny));
                }
            }
        }

        var center = new Point((int)(sumX / count), (int)(sumY / count));
        return new Blob(center, Math.Max(2, Math.Sqrt(count / Math.PI)));
    }

    private static void SaveSuccessScreenshot(Bitmap screenshot, Point bobber)
    {
        // draw on a copy so the original isn't modified
        using var marked = new Bitmap(screenshot);
        using (var graphics = Graphics.FromImage(marked))
        using (var pen = new Pen(Color.Lime, 2))
        {
            const int radius = 20;
            graphics.DrawEllipse(pen, bobber.X - radius, bobber.Y - radius, radius * 2, radius * 2);
            graphics.DrawLine(pen, bobber.X - radius - 10, bobber.Y, bobber.X + radius + 10, bobber.Y);
            graphics.DrawLine(pen, bobber.X, bobber.Y - radius - 10, bobber.X, bobber.Y + radius + 10);
        }

        Directory.CreateDirectory(LogDirectory);
        var path = Path.Combine(LogDirectory, $"success_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
        marked.Save(path, ImageFormat.Png);
        Log.Info($"Saved success screenshot to {path}");
    }
}
