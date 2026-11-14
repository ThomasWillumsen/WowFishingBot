using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace DeskHelper.Vision;

/// <summary>Watches a small screen area (around the bobber) and reports how much of it changed recently. A bite dips and splashes the bobber.</summary>
internal sealed class BiteWatcher(Rectangle area)
{
    // brightness difference (0-255) for a pixel to count as changed
    private const int ChangedLumaDelta = 40;
    private static readonly TimeSpan ReferenceAge = TimeSpan.FromMilliseconds(150);

    private readonly Queue<(DateTime Time, byte[] Luma)> _frames = new();

    /// <summary>
    /// Percentage (0-100) of pixels that differ noticeably from the frame about 150 ms ago,
    /// or null while there isn't enough history yet.
    /// </summary>
    public double? MeasureChange()
    {
        var now = DateTime.UtcNow;
        var current = CaptureLuma();

        // keep the youngest frame that is at least ReferenceAge old as the reference
        while (_frames.Count > 1 && now - _frames.ElementAt(1).Time >= ReferenceAge)
            _frames.Dequeue();

        double? change = null;
        if (_frames.Count > 0 && now - _frames.Peek().Time >= ReferenceAge / 2)
        {
            var reference = _frames.Peek().Luma;
            var changed = 0;
            for (var i = 0; i < current.Length; i++)
            {
                if (Math.Abs(current[i] - reference[i]) > ChangedLumaDelta)
                    changed++;
            }

            change = 100.0 * changed / current.Length;
        }

        _frames.Enqueue((now, current));
        return change;
    }

    private byte[] CaptureLuma()
    {
        using var bitmap = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
            graphics.CopyFromScreen(area.Location, Point.Empty, area.Size, CopyPixelOperation.SourceCopy);

        var data = bitmap.LockBits(new Rectangle(Point.Empty, bitmap.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var pixels = new byte[data.Stride * data.Height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

            var luma = new byte[data.Width * data.Height];
            for (var y = 0; y < data.Height; y++)
            {
                for (var x = 0; x < data.Width; x++)
                {
                    var offset = y * data.Stride + x * 4; // BGRA
                    luma[y * data.Width + x] = (byte)((pixels[offset + 2] * 299 + pixels[offset + 1] * 587 + pixels[offset] * 114) / 1000);
                }
            }

            return luma;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }
}
