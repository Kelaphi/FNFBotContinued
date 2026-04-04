using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FNFBot.Models;

namespace FNFBot.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    public static readonly Key[] DefaultBindings =
    {
        Key.Left, Key.Down, Key.Up, Key.Right
    };

    public static readonly string[] LaneNames =
    {
        "Left", "Down", "Up", "Right"
    };

    private Key[] _bindings = (Key[])DefaultBindings.Clone();
    public Key[] Bindings
    {
        get => _bindings;
        set { _bindings = value; OnPropertyChanged(); }
    }

    private Key _startBind = Key.F1;
    public Key StartBind
    {
        get => _startBind;
        set { _startBind = value; OnPropertyChanged(); }
    }

    private Key _offsetIncreaseBind = Key.F2;
    public Key OffsetIncreaseBind
    {
        get => _offsetIncreaseBind;
        set { _offsetIncreaseBind = value; OnPropertyChanged(); }
    }

    private Key _offsetDecreaseBind = Key.F3;
    public Key OffsetDecreaseBind
    {
        get => _offsetDecreaseBind;
        set { _offsetDecreaseBind = value; OnPropertyChanged(); }
    }

    private Chart? _chart;
    public Chart? Chart
    {
        get => _chart;
        set { _chart = value; OnPropertyChanged(); OnPropertyChanged(nameof(ChartInfo)); }
    }

    public string ChartInfo => _chart is null
        ? "No chart loaded"
        : $"{_chart.SongName}  |  {_chart.Bpm} BPM  |  {_chart.Notes.Count(n => n.Player == 1)} player notes";

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        set { _isPlaying = value; OnPropertyChanged(); }
    }

    private double _offsetMs;
    public double OffsetMs
    {
        get => _offsetMs;
        set { _offsetMs = value; OnPropertyChanged(); }
    }

    private readonly ObservableCollection<string> _log = new();
    public ObservableCollection<string> Log => _log;

    public void AddLog(string msg) =>
        _log.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {msg}");

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}