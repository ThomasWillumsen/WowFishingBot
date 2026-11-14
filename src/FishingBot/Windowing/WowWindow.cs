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

    /// <summary>Brings WoW to the foreground. Disposing alt-tabs back if WoW wasn't already focused, but only when <see cref="Settings.AltTabBack"/> is enabled.</summary>
    public IDisposable FocusTemporarily()
    {
        var wasForeground = NativeMethods.GetForegroundWindow() == Handle;
        NativeMethods.SetForegroundWindow(Handle);
        return new FocusScope(wasForeground, input);
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
