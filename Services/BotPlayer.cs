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

    public bool IsRunning => _running;
    public event Action<int>? NoteHit;
    public event Action<string>? RatingHit;

    public float DevMinMs { get; set; } = 0;
    public float DevMaxMs { get; set; } = 0;
    public double MissPct { get; set; } = 0;
    public int HoldMinMs { get; set; } = 5;
    public int HoldMaxMs { get; set; } = 30;

    public void Start(Chart chart, Key[] bindings, double offsetMs = 0)
    {
        if (_running) Stop();
        _chart = chart;
        _bindings = bindings;
        _running = true;

        _thread = new Thread(() => RunLoop((float)offsetMs))
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

    private void RunLoop(float offsetMs)
    {
        if (_chart is null) return;

        var notes = _chart.Notes
            .Where(n => n.Player == 1 && n.Lane < _bindings.Length)
            .OrderBy(n => n.TimeMs)
            .ToList();

        if (notes.Count == 0) { _running = false; return; }

        var rng = new Random();
        var deviations = new float[notes.Count];
        var isMiss = new bool[notes.Count];
        var decided = new bool[notes.Count];

        var sw = Stopwatch.StartNew();
        var holdTimes = new float[_bindings.Length];
        int i = 0;

        while (_running)
        {
            float nowMs = (float)sw.Elapsed.TotalMilliseconds + offsetMs;

            for (int lane = 0; lane < holdTimes.Length; lane++)
            {
                if (holdTimes[lane] != 0 && (float)sw.Elapsed.TotalMilliseconds > holdTimes[lane])
                {
                    holdTimes[lane] = 0;
                    SendKeyUp(lane);
                }
            }

            while (i < notes.Count)
            {
                if (!decided[i])
                {
                    decided[i] = true;
                    if (MissPct > 0 && rng.NextDouble() * 100.0 < MissPct)
                    {
                        isMiss[i] = true;
                        deviations[i] = 0;
                    }
                    else
                    {
                        isMiss[i] = false;
                        float lo = DevMinMs, hi = DevMaxMs;
                        if (lo > hi) { float t = lo; lo = hi; hi = t; }
                        deviations[i] = (lo == hi) ? lo : lo + (float)(rng.NextDouble() * (hi - lo));
                    }
                }

                if (isMiss[i])
                {
                    if (nowMs >= (float)notes[i].TimeMs + 166f)
                    {
                        RatingHit?.Invoke("Miss");
                        i++;
                        continue;
                    }
                    break;
                }

                if (nowMs < (float)notes[i].TimeMs + deviations[i]) break;

                var note = notes[i];
                i++;
                int lane = note.Lane;

                if (holdTimes[lane] != 0)
                {
                    holdTimes[lane] = 0;
                    SendKeyUp(lane);
                }

                if (note.Duration > 0)
                {
                    float holdEnd = (float)sw.Elapsed.TotalMilliseconds + (float)note.Duration + 10f;

                    for (int j = i; j < notes.Count; j++)
                    {
                        if (notes[j].Lane == lane)
                        {
                            float nextDev = decided[j] ? deviations[j] : 0f;
                            float nextFire = (float)notes[j].TimeMs + nextDev - offsetMs;
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
                    KeyPress(lane, holdTimes, sw, notes, i, offsetMs);
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

    private void KeyPress(int lane, float[] holdTimes, Stopwatch sw, List<Note> notes, int nextIndex, float offsetMs)
    {
        byte vk = (byte)KeyInterop.VirtualKeyFromKey(_bindings[lane]);
        byte scan = (byte)MapVirtualKey(vk, 0);
        var rng = new Random();
        int holdMs = (HoldMaxMs > 0 && HoldMinMs < HoldMaxMs)
            ? rng.Next(HoldMinMs, HoldMaxMs + 1)
            : HoldMaxMs;

        keybd_event(vk, scan, (int)KEYEVENTF_EXTENDEDKEY, 0);

        float deadline = (float)sw.Elapsed.TotalMilliseconds + holdMs;
        while ((float)sw.Elapsed.TotalMilliseconds < deadline)
        {
            for (int j = nextIndex; j < notes.Count; j++)
            {
                if (notes[j].Lane == lane &&
                    (float)notes[j].TimeMs - offsetMs <= (float)sw.Elapsed.TotalMilliseconds + 15f)
                    goto done;
            }
        }
    done:
        keybd_event(vk, scan, (int)(KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP), 0);
    }
}