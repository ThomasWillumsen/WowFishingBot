using System.Diagnostics;
using System.Runtime.InteropServices;
using CSCore.CoreAudioAPI;
using WindowsInput.Native;


public class FishingBot
{
    // [DllImport("user32.dll")]
    // private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    // [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
    // private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    private Process _wowProcess;
    private WindowManager _windowManager;
    private AudioSessionControl _wowAudioSession;

    public FishingBot()
    {
        _wowProcess = FindWowProcess();
        _wowAudioSession = GetWowAudioSession();
        _windowManager = new WindowManager(_wowProcess);
    }

    public void Start()
    {
        Console.WriteLine("Fishing bot started");
        Console.WriteLine("====================================");
        Console.WriteLine("Cast fishing rod");

        using (_windowManager.WowWindowFocused())
        {
            Utility.ThreadSleepRandom(250, 750);
            CastFishingRod();
            Utility.ThreadSleepRandom(200, 400);
        }

        while (true)
        {
            using (var audioMeterInformation = _wowAudioSession.QueryInterface<AudioMeterInformation>())
            {
                var peakVolume = audioMeterInformation.GetPeakValue() * 100;
                if (peakVolume < 2.7)
                    continue;

                Console.WriteLine("Possible fish detected");
                Utility.ThreadSleepRandom(250, 750);

                using (_windowManager.WowWindowFocused())
                {
                    Utility.ThreadSleepRandom(250, 750);
                    try
                    {
                        ReelInFish();
                        Utility.ThreadSleepRandom(500, 1500);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }

                    Console.WriteLine("====================================");
                    Console.WriteLine("Recast fishing rod");
                    CastFishingRod();
                    Utility.ThreadSleepRandom(200, 400);
                }
            }

            // wait 100ms before checking volume again
            System.Threading.Thread.Sleep(100);
        }
    }

    private void CastFishingRod()
    {
        var simulator = new WindowsInput.InputSimulator();
        simulator.Keyboard.KeyDown(VirtualKeyCode.F4);
        simulator.Keyboard.SleepRandom(70, 150);
        simulator.Keyboard.KeyUp(VirtualKeyCode.F4);

        // keybd_event(0x73, 0, 0x0000, (UIntPtr)0);
        // Thread.Sleep(150);
        // keybd_event(0x73, 0, 0x0002, (UIntPtr)0);
    }

    private void ReelInFish()
    {
        Console.WriteLine("Locating fishing bobber");
        var bobberCoordinates = _windowManager.LocateFishingBobber();

        // move mouse to red pixel
        var x = bobberCoordinates.Item1;
        var y = bobberCoordinates.Item2;

        Console.WriteLine("Reeling in fish");
        SetCursorPos(x, y);

        Utility.ThreadSleepRandom(120, 170);

        var simulator = new WindowsInput.InputSimulator();
        simulator.Mouse.RightButtonDown();
        simulator.Mouse.Sleep(120);
        simulator.Mouse.RightButtonUp();

        Console.WriteLine("Fish reeled in");

        // simulator.Keyboard.KeyDown(VirtualKeyCode.OEM_PLUS);
        // simulator.Keyboard.Sleep(120);
        // keybd_event(0xBB, 0, 0x0000, (UIntPtr)0);
        // Thread.Sleep(120);
        // simulator.Keyboard.KeyUp(VirtualKeyCode.OEM_PLUS);
        // keybd_event(0xBB, 0, 0x0002, (UIntPtr)0);
    }

    private AudioSessionControl GetWowAudioSession()
    {
        using (var sessionManager = GetDefaultAudioSessionManager2(DataFlow.Render))
        using (var sessionEnumerator = sessionManager.GetSessionEnumerator())
        {
            foreach (var session in sessionEnumerator)
            {
                using (var session2 = session.QueryInterface<AudioSessionControl2>())
                {
                    if (session2.Process.ProcessName != "Wow")
                        continue;
                }

                Console.WriteLine("World of Warcraft audio session was found");
                return session;
            }

            throw new Exception("No World of Warcraft audio session was found");
        }
    }

    private AudioSessionManager2 GetDefaultAudioSessionManager2(DataFlow dataFlow)
    {
        using (var enumerator = new MMDeviceEnumerator())
        {
            using (var device = enumerator.GetDefaultAudioEndpoint(dataFlow, Role.Multimedia))
            {
                Console.WriteLine("Default Audio device found: " + device.FriendlyName);
                var sessionManager = AudioSessionManager2.FromMMDevice(device);
                return sessionManager;
            }
        }
    }

    private Process FindWowProcess()
    {
        Process[] wowProcesses = Process.GetProcessesByName("Wow");
        if (wowProcesses.Length < 0)
            throw new Exception("No World of Warcraft process was found on the machine");

        if (wowProcesses.Length > 1)
            Console.WriteLine("Multiple World of Warcraft processes are running. Choosing the first one");

        var wowProcess = wowProcesses[0];
        Console.WriteLine($"A World of Warcraft process ({wowProcess.ProcessName}) was found");
        return wowProcess;
    }
}