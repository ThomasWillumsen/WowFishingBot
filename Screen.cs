using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class Screenx
{
    private class User32
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowRect(IntPtr hWnd, ref Rect rect);
    }

    public static Tuple<int, int> FindRedPixel(Process wowProcess)
    {
        var applicationRect = new User32.Rect();
        User32.GetWindowRect(wowProcess.MainWindowHandle, ref applicationRect);

        var applicationWidth = applicationRect.right - applicationRect.left;
        var applicationHeight = applicationRect.bottom - applicationRect.top;

        // // get the center 900x900 of the rect
        // Console.WriteLine("Rect: " + rect.left + ", " + rect.top + ", " + rect.right + ", " + rect.bottom);

        // var x = (rect.left + rect.right) / 2 - 450;
        // var y = (rect.top + rect.bottom) / 2 - 450;
        // var width = 900;
        // var height = 900;

        var screenshotX = 2560 / 2 - 450;
        var screenshotY = 1440 / 2 - 500;

        var width = 900;
        var height = 500;

        var screenshot = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(screenshot))
        {
            graphics.CopyFromScreen(screenshotX, screenshotY, 0, 0, screenshot.Size, CopyPixelOperation.SourceCopy);
        }

        screenshot.Save(@"C:\Users\Thomas\Downloads\wtf\screenshot.png", ImageFormat.Png);

        // Y is inverted, meaning that 0,0 is the top left corner
        for (var y = screenshot.Height - 1; y >= 0; y--)
            for (var x = 0; x < screenshot.Width; x++)
            {
                {
                    var pixel = screenshot.GetPixel(x, y);
                    // 140 +-10, 50 +-10, 25+-5
                    if (pixel.R > 130 && pixel.R < 150 && pixel.G > 40 && pixel.G < 60 && pixel.B > 20 && pixel.B < 35)
                    {
                        var applicationScreenXModifier = ((decimal)applicationWidth / 2560);
                        var applicationScreenYModifier = ((decimal)applicationHeight / 1440);

                        var screenX = (int)((x + 2560 / 2 - 450) * applicationScreenXModifier);
                        var screenY = (int)((y + 1440 / 2 - 500) * applicationScreenYModifier);

                        Console.WriteLine($"Found pixel at ({x},{y}) ({screenX},{screenY}) with rgba: ({pixel.R}, {pixel.G}, {pixel.B})");

                        return new Tuple<int, int>(screenX, screenY);
                    }
                }
            }

        throw new Exception("No red pixel found");
    }
}