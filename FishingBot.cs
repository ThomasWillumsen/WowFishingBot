using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using CSCore.CoreAudioAPI;
using WindowsInput.Native;

[DllImport("user32.dll")]
static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = false)]
static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

[DllImport("user32.dll")]
static extern bool SetCursorPos(int X, int Y);

[SupportedOSPlatform("windows")]
public class FishingBot
{
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
        Utility.ThreadSleepRandom(100, 150);

        Console.WriteLine("Cast fishing rod");

        _windowManager.SetToForegroundWindow();
        SetToForegroundWindow(wowProcess);
        Thread.Sleep(100);
        PressFishingKey(wowProcess);

        while (true)
        {
            using (var audioMeterInformation = wowAudioSession.QueryInterface<AudioMeterInformation>())
            {
                var peakVolume = audioMeterInformation.GetPeakValue() * 100;
                Console.WriteLine(peakVolume);

                if (peakVolume < 3)
                    continue;

                Console.WriteLine("Fish detected");
                Console.WriteLine("Waiting for 500-1000ms (random)");

                var random = new Random();
                var randomTime = random.Next(500, 1000);
                System.Threading.Thread.Sleep(randomTime);

                Console.WriteLine("Reeling in fish");
                // SetToForegroundWindow(wowProcess);
                Thread.Sleep(100);

                try
                {
                    PressFishingKey(wowProcess, true);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }

                Console.WriteLine("Fishing rod reeled in");
                Thread.Sleep(1000);

                PressFishingKey(wowProcess);
                Console.WriteLine("Recast fishing rod");
            }

            System.Threading.Thread.Sleep(100);
        }
    }
    // Console.ReadKey();

    static void SetToForegroundWindow(Process wowProcess)
    {
        var wowWindowHandle = wowProcess.MainWindowHandle;
        SetForegroundWindow(wowWindowHandle);
    }

    static void PressFishingKey(Process wowProcess, bool reel = false)
    {
        if (reel == false)
        {
            var simulator = new WindowsInput.InputSimulator();
            simulator.Keyboard.KeyDown(VirtualKeyCode.F4);
            // keybd_event(0x73, 0, 0x0000, (UIntPtr)0);
            simulator.Keyboard.Sleep(120);
            // Thread.Sleep(150);
            simulator.Keyboard.KeyUp(VirtualKeyCode.F4);
            // keybd_event(0x73, 0, 0x0002, (UIntPtr)0);
        }
        else
        {
            var redPixel = WindowManager.FindRedPixel(wowProcess);

            // move mouse to red pixel
            var x = redPixel.Item1;
            var y = redPixel.Item2;

            SetCursorPos(x, y);

            Thread.Sleep(150);

            var simulator = new WindowsInput.InputSimulator();
            // simulator.Keyboard.KeyDown(VirtualKeyCode.OEM_PLUS);
            simulator.Mouse.RightButtonDown();
            simulator.Mouse.Sleep(120);
            simulator.Mouse.RightButtonUp();
            // simulator.Keyboard.Sleep(120);
            // keybd_event(0xBB, 0, 0x0000, (UIntPtr)0);
            // Thread.Sleep(120);
            // simulator.Keyboard.KeyUp(VirtualKeyCode.OEM_PLUS);
            // keybd_event(0xBB, 0, 0x0002, (UIntPtr)0);
        }
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