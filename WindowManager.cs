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

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

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


        // Y is inverted, meaning that 0,0 is the top left corner
        for (var y = screenshot.Height - 1; y >= 0; y--)
            for (var x = 0; x < screenshot.Width; x++)
            {
                {
                    var pixel = screenshot.GetPixel(x, y);
                    var hue = pixel.GetHue();
                    var saturation = pixel.GetSaturation();
                    var brightness = pixel.GetBrightness();

                    // check if hue is reddish
                    if ((hue > 345 || hue < 15) && saturation > 0.5)
                    {
                        var applicationScreenXModifier = ((decimal)applicationWidth / 2560);
                        var applicationScreenYModifier = ((decimal)applicationHeight / 1440);

                        var screenX = (int)((x + 2560 / 2 - 450) * applicationScreenXModifier);
                        var screenY = (int)((y + 1440 / 2 - 500) * applicationScreenYModifier);

                        Console.WriteLine($"Possible fish bobber located at ({screenX}, {screenY}) with HSL: ({hue}, {saturation}, {brightness})");

                        return new Tuple<int, int>(screenX, screenY);
                    }
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
            if (!_isWowAlreadyInForeground)
            {
                var simulator = new WindowsInput.InputSimulator();
                // press alt tab to go to previous window
                simulator.Keyboard.ModifiedKeyStroke(WindowsInput.Native.VirtualKeyCode.LMENU, WindowsInput.Native.VirtualKeyCode.TAB);
            }
        }
    }
}