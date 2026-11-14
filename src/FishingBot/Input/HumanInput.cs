using DeskHelper.Infrastructure;
using WindowsInput;
using WindowsInput.Native;

namespace DeskHelper.Input;

/// <summary>Keyboard and mouse input with randomized hold times so it doesn't look machine-perfect.</summary>
internal sealed class HumanInput
{
    private readonly InputSimulator _simulator = new();

    public void PressKey(VirtualKeyCode key)
    {
        _simulator.Keyboard.KeyDown(key);
        _simulator.Keyboard.Sleep(RandomDelay.Next(50, 90));
        _simulator.Keyboard.KeyUp(key);
    }

    public void RightClick()
    {
        _simulator.Mouse.RightButtonDown();
        _simulator.Mouse.Sleep(RandomDelay.Next(55, 135));
        _simulator.Mouse.RightButtonUp();
    }

    public void AltTab()
    {
        _simulator.Keyboard.KeyDown(VirtualKeyCode.LMENU);
        _simulator.Keyboard.Sleep(RandomDelay.Next(60, 140));
        _simulator.Keyboard.KeyDown(VirtualKeyCode.TAB);
        _simulator.Keyboard.Sleep(RandomDelay.Next(50, 100));
        _simulator.Keyboard.KeyUp(VirtualKeyCode.TAB);
        _simulator.Keyboard.Sleep(RandomDelay.Next(30, 80));
        _simulator.Keyboard.KeyUp(VirtualKeyCode.LMENU);
    }
}
