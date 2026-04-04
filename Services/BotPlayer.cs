using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using FNFBot.Models;

namespace FNFBot.Services;

public class BotPlayer
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct INPUT
    {
        [FieldOffset(0)] public int type;
        [FieldOffset(8)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private Chart? _chart;
    private Key[] _bindings = Array.Empty<Key>();
    private Thread? _thread;
    private volatile bool _running;
    private volatile float _offsetMs;

    public bool IsRunning => _running;
    public event Action<int>? NoteHit;
    public event Action<string>? RatingHit;

    public float DevMinMs { get; set; } = 0;
    public float DevMaxMs { get; set; } = 0;
    public double MissPct { get; set; } = 0;
    public int HoldMinMs { get; set; } = 0;
    public int HoldMaxMs { get; set; } = 0;

    public PlayerSide PlayerSide { get; set; } = PlayerSide.Right;

    public bool StartFromFirstNote { get; set; } = false;

    public float OffsetMs
    {
        get => _offsetMs;
        set => _offsetMs = value;
    }

    public void Start(Chart chart, Key[] bindings, double offsetMs = 0)
    {
        if (_running) Stop();
        _chart = chart;
        _bindings = bindings;
        _offsetMs = (float)offsetMs;
        _running = true;

        _thread = new Thread(RunLoop)
        {
            IsBackground = true,
            Priority = ThreadPriority.Highest,
            Name = "BotPlayerThread"
        };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        _thread?.Join(500);
        _thread = null;
    }

    private void RunLoop()
    {
        if (_chart is null) return;

        var notes = _chart.Notes
            .Where(n => (PlayerSide == PlayerSide.Both || n.Player == (PlayerSide == PlayerSide.Left ? 0 : 1))
                        && n.Lane < _bindings.Length)
            .OrderBy(n => n.TimeMs)
            .ToList();

        if (notes.Count == 0) { _running = false; return; }

        double timeShift = StartFromFirstNote ? notes[0].TimeMs : 0.0;

        var rng = new Random();
        var sw = Stopwatch.StartNew();
        var holdTimes = new float[_bindings.Length];
        int i = 0;

        while (_running)
        {
            float offset = _offsetMs;

            float nowMs = (float)sw.Elapsed.TotalMilliseconds;

            for (int lane = 0; lane < holdTimes.Length; lane++)
            {
                if (holdTimes[lane] != 0 && nowMs > holdTimes[lane])
                {
                    holdTimes[lane] = 0;
                    SendKeyUp(lane);
                }
            }

            while (i < notes.Count)
            {
                var note = notes[i];

                float fireTime = (float)(note.TimeMs - timeShift) - offset;

                bool isMiss = MissPct > 0 && rng.NextDouble() * 100.0 < MissPct;

                if (isMiss)
                {
                    if (nowMs >= fireTime + 166f)
                    {
                        RatingHit?.Invoke("Miss");
                        i++;
                        continue;
                    }
                    break;
                }

                float lo = DevMinMs, hi = DevMaxMs;
                if (lo > hi) { float t = lo; lo = hi; hi = t; }
                float deviation = (lo == hi) ? lo : lo + (float)(rng.NextDouble() * (hi - lo));

                if (nowMs < fireTime + deviation) break;

                i++;
                int lane = note.Lane;

                if (holdTimes[lane] != 0)
                {
                    holdTimes[lane] = 0;
                    SendKeyUp(lane);
                }

                if (note.Duration > 0)
                {
                    float holdEnd = nowMs + (float)note.Duration + 10f;

                    for (int j = i; j < notes.Count; j++)
                    {
                        if (notes[j].Lane == lane)
                        {
                            float nextFire = (float)(notes[j].TimeMs - timeShift) - offset;
                            if (holdEnd >= nextFire - 10f)
                                holdEnd = nextFire - 15f;
                            break;
                        }
                    }

                    holdTimes[lane] = holdEnd;
                    SendKeyDown(lane);
                }
                else
                {
                    int holdMs = (HoldMinMs < HoldMaxMs)
                        ? rng.Next(HoldMinMs, HoldMaxMs + 1)
                        : HoldMaxMs;

                    holdTimes[lane] = nowMs + holdMs;
                    SendKeyDown(lane);
                }

                NoteHit?.Invoke(lane);
                RatingHit?.Invoke("Hit");
            }

            if (i >= notes.Count && holdTimes.All(t => t == 0))
                break;
        }

        for (int lane = 0; lane < holdTimes.Length; lane++)
            if (holdTimes[lane] != 0)
                SendKeyUp(lane);

        _running = false;
    }

    private void SendKeyDown(int lane)
    {
        ushort vk = (ushort)KeyInterop.VirtualKeyFromKey(_bindings[lane]);
        ushort scan = (ushort)MapVirtualKey(vk, 0);
        DoSendInput(vk, scan, KEYEVENTF_EXTENDEDKEY);
    }

    private void SendKeyUp(int lane)
    {
        ushort vk = (ushort)KeyInterop.VirtualKeyFromKey(_bindings[lane]);
        ushort scan = (ushort)MapVirtualKey(vk, 0);
        DoSendInput(vk, scan, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP);
    }

    private void DoSendInput(ushort vk, ushort scan, uint flags)
    {
        var input = new INPUT { type = INPUT_KEYBOARD };
        input.ki.wVk = vk;
        input.ki.wScan = scan;
        input.ki.dwFlags = flags;
        input.ki.time = 0;
        input.ki.dwExtraInfo = IntPtr.Zero;
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }
}