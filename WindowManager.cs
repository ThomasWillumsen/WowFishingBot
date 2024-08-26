using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public class WindowManager
{
    private readonly Process _wowProcess;

    public WindowManager(Process wowProcess)
    {
        _wowProcess = wowProcess;
    }


    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

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

    public FocusWindowTemporarily WowWindowFocused()
    {
        return new FocusWindowTemporarily(_wowProcess);
    }

    public Tuple<int, int> LocateFishingBobber()
    {
        var applicationRect = new User32.Rect();
        User32.GetWindowRect(_wowProcess.MainWindowHandle, ref applicationRect);

        var screenWidth = 2560;
        var screenHeight = 1440;
        var applicationWidth = applicationRect.right - applicationRect.left;
        var applicationHeight = applicationRect.bottom - applicationRect.top;
        var applicationScreenModifierX = (decimal)applicationWidth / screenWidth;
        var applicationScreenModifierY = (decimal)applicationHeight / screenHeight;

        var screenshotWidth = 900;
        var screenshotHeight = 500;
        var screenshotStartX = screenWidth / 2 - (screenshotWidth / 2); // horizontally center the screenshot
        var screenshotStartY = screenHeight / 2 - screenshotHeight; // vertically above the center. The bottom ends at the center of the screen

        var screenshot = new Bitmap(screenshotWidth, screenshotHeight, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(screenshot))
        {
            graphics.CopyFromScreen(screenshotStartX, screenshotStartY, 0, 0, screenshot.Size, CopyPixelOperation.SourceCopy);
        }


        // Y is inverted, meaning that 0,0 is the top left corner
        for (var y = screenshot.Height - 1; y >= 0; y--)
            for (var x = 0; x < screenshot.Width; x++)
            {
                var pixel = screenshot.GetPixel(x, y);
                var hue = pixel.GetHue();
                var saturation = pixel.GetSaturation();
                var brightness = pixel.GetBrightness();

                // check if hue is reddish
                if ((hue > 345 || hue < 15) && saturation > 0.5)
                {
                    var screenX = (int)((x + screenshotStartX) * applicationScreenModifierX);
                    var screenY = (int)((y + screenshotStartY) * applicationScreenModifierY);

                    Console.WriteLine($"Possible fish bobber located at ({screenX}, {screenY}) with HSL: ({hue}, {saturation}, {brightness})");

                    return new Tuple<int, int>(screenX, screenY);
                }
            }

        // create logs dir if it doesn't exist
        if (!Directory.Exists("logs"))
            Directory.CreateDirectory("logs");
        screenshot.Save($"logs/failed_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png", ImageFormat.Png);
        throw new Exception("No red pixel found");
    }


    public class FocusWindowTemporarily : IDisposable
    {
        private readonly bool _isWowAlreadyInForeground;

        public FocusWindowTemporarily(Process wowProcess)
        {
            var foregroundWindowHandle = GetForegroundWindow();
            _isWowAlreadyInForeground = foregroundWindowHandle == wowProcess.MainWindowHandle;
            SetToForegroundWindow(wowProcess);
        }

        private void SetToForegroundWindow(Process wowProcess)
        {
            var wowWindowHandle = wowProcess.MainWindowHandle;
            SetForegroundWindow(wowWindowHandle);
        }

        public void Dispose()
        {
            // if wow was not in foreground, alt tab back to previous window
            if (!_isWowAlreadyInForeground)
            {
                var simulator = new WindowsInput.InputSimulator();
                simulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.TAB);
            }
        }
    }
}