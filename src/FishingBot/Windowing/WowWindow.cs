using System.Diagnostics;
using System.Drawing;
using DeskHelper.Infrastructure;
using DeskHelper.Input;

namespace DeskHelper.Windowing;

internal sealed class WowWindow(Process process, HumanInput input)
{
    private IntPtr Handle => process.MainWindowHandle;

    /// <summary>The game's client area (excluding borders and title bar) in screen coordinates.</summary>
    public Rectangle GetClientAreaOnScreen()
    {
        NativeMethods.GetClientRect(Handle, out var clientRect);
        var nativeOrigin = new NativeMethods.NativePoint();
        NativeMethods.ClientToScreen(Handle, ref nativeOrigin);
        Point origin = nativeOrigin;

        var area = new Rectangle(origin.X, origin.Y, clientRect.Width, clientRect.Height);
        Log.Debug($"Client area on screen: {area}");
        return area;
    }

    /// <summary>
    /// Brings WoW to the foreground if it isn't there already and throws if that doesn't work, so input is never sent to another window.
    /// Disposing alt-tabs back if WoW wasn't already focused, but only when <see cref="Settings.AltTabBack"/> is enabled.
    /// </summary>
    public IDisposable FocusTemporarily()
    {
        var wasForeground = IsForeground();

        if (!wasForeground)
        {
            Log.Debug("WoW is not in the foreground, focusing it");
            BringToForeground();
        }

        return new FocusScope(wasForeground, input);
    }

    private bool IsForeground() => NativeMethods.GetForegroundWindow() == Handle;

    private void BringToForeground()
    {
        const int attempts = 3;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            if (NativeMethods.IsIconic(Handle))
                NativeMethods.ShowWindow(Handle, NativeMethods.ShowWindowRestore);

            // Windows usually refuses a plain SetForegroundWindow from a background process; sharing input state with the current foreground thread allows it
            var foregroundThread = NativeMethods.GetWindowThreadProcessId(NativeMethods.GetForegroundWindow(), IntPtr.Zero);
            var currentThread = NativeMethods.GetCurrentThreadId();
            var attached = foregroundThread != 0 && foregroundThread != currentThread
                && NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);

            try
            {
                NativeMethods.BringWindowToTop(Handle);
                NativeMethods.SetForegroundWindow(Handle);
            }
            finally
            {
                if (attached)
                    NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
            }

            // give Windows a moment to switch before checking
            RandomDelay.Sleep(100, 250);
            if (IsForeground())
                return;

            Log.Debug($"Focusing WoW failed (attempt {attempt} of {attempts})");
        }

        throw new InvalidOperationException("Could not bring World of Warcraft to the foreground");
    }

    private sealed class FocusScope(bool wasForeground, HumanInput input) : IDisposable
    {
        public void Dispose()
        {
            if (!wasForeground && Settings.AltTabBack)
                input.AltTab();
        }
    }
}
