using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FNFBot.Services;
using FNFBot.ViewModels;
using Microsoft.Win32;

namespace FNFBot;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly BotPlayer _bot = new();
    private readonly LowLevelKeyboardHook _hook = new();

    private const double OffsetStepMs = 5;
    private int _hitCount, _missCount;
    private bool _isLoading = true;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        var cfg = ConfigService.Load();
        _vm.Bindings = cfg.Bindings
            .Select((s, i) => ConfigService.ParseKey(s, MainViewModel.DefaultBindings[i]))
            .ToArray();
        _vm.StartBind = ConfigService.ParseKey(cfg.StartBind, Key.F1);
        _vm.OffsetIncreaseBind = ConfigService.ParseKey(cfg.OffsetIncreaseBind, Key.F2);
        _vm.OffsetDecreaseBind = ConfigService.ParseKey(cfg.OffsetDecreaseBind, Key.F3);

        OffsetBox.Text = cfg.OffsetMs.ToString();
        DevMinBox.Text = cfg.DevMinMs.ToString();
        DevMaxBox.Text = cfg.DevMaxMs.ToString();
        MissPctBox.Text = cfg.MissPct.ToString();
        HoldMinBox.Text = cfg.HoldMinMs.ToString();
        HoldMaxBox.Text = cfg.HoldMaxMs.ToString();

        if (cfg.PlayAsLeft) PlayerLeft.IsChecked = true;
        else PlayerRight.IsChecked = true;

        if (cfg.StartFromFirstNote) StartFromFirstNote.IsChecked = true;
        else StartFromSong.IsChecked = true;

        _bot.DevMinMs = cfg.DevMinMs;
        _bot.DevMaxMs = cfg.DevMaxMs;
        _bot.MissPct = cfg.MissPct;
        _bot.HoldMinMs = cfg.HoldMinMs;
        _bot.HoldMaxMs = cfg.HoldMaxMs;
        _bot.PlayAsLeft = cfg.PlayAsLeft;

        _bot.NoteHit += lane => Dispatcher.InvokeAsync(() =>
            _vm.AddLog($"Hit lane {MainViewModel.LaneNames[lane]}"));

        _bot.RatingHit += rating => Dispatcher.InvokeAsync(() =>
        {
            if (rating == "Miss") MissCount.Text = (++_missCount).ToString();
            else HitCount.Text = (++_hitCount).ToString();
        });

        _hook.OnKeyPressed += (_, vk) =>
        {
            int startVk = KeyInterop.VirtualKeyFromKey(_vm.StartBind);
            int increaseVk = KeyInterop.VirtualKeyFromKey(_vm.OffsetIncreaseBind);
            int decreaseVk = KeyInterop.VirtualKeyFromKey(_vm.OffsetDecreaseBind);

            if (vk == startVk)
                Dispatcher.Invoke(() =>
                {
                    if (_bot.IsRunning) Stop_Click(null!, null!);
                    else Play_Click(null!, null!);
                });
            else if (vk == increaseVk)
                Dispatcher.Invoke(() =>
                {
                    if (double.TryParse(OffsetBox.Text, out double val))
                    {
                        val += OffsetStepMs;
                        OffsetBox.Text = val.ToString();
                        _bot.OffsetMs = (float)val;
                        _vm.AddLog($"Offset → {val}ms");
                        SaveConfig();
                    }
                });
            else if (vk == decreaseVk)
                Dispatcher.Invoke(() =>
                {
                    if (double.TryParse(OffsetBox.Text, out double val))
                    {
                        val -= OffsetStepMs;
                        OffsetBox.Text = val.ToString();
                        _bot.OffsetMs = (float)val;
                        _vm.AddLog($"Offset → {val}ms");
                        SaveConfig();
                    }
                });
        };
        _hook.Hook();

        BuildBindingButtons();
        LogBox.ItemsSource = _vm.Log;
        _vm.AddLog("Ready. Load a chart JSON and press Start.");
        _isLoading = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        _hook.Unhook();
        _bot.Stop();
        base.OnClosed(e);
    }

    private void SaveConfig()
    {
        if (_isLoading) return;
        double.TryParse(OffsetBox?.Text, out double offset);
        float.TryParse(DevMinBox?.Text, out float devMin);
        float.TryParse(DevMaxBox?.Text, out float devMax);
        double.TryParse(MissPctBox?.Text, out double miss);
        int.TryParse(HoldMinBox?.Text, out int holdMin);
        int.TryParse(HoldMaxBox?.Text, out int holdMax);

        ConfigService.Save(new BotConfig
        {
            Bindings = _vm.Bindings.Select(k => k.ToString()).ToArray(),
            StartBind = _vm.StartBind.ToString(),
            OffsetIncreaseBind = _vm.OffsetIncreaseBind.ToString(),
            OffsetDecreaseBind = _vm.OffsetDecreaseBind.ToString(),
            OffsetMs = offset,
            DevMinMs = devMin,
            DevMaxMs = devMax,
            MissPct = miss,
            HoldMinMs = holdMin,
            HoldMaxMs = holdMax,
            PlayAsLeft = PlayerLeft.IsChecked == true,
            StartFromFirstNote = StartFromFirstNote.IsChecked == true
        });
    }

    private void Settings_Changed(object sender, TextChangedEventArgs e)
    {
        if (_bot == null || OffsetBox == null) return;
        if (float.TryParse(DevMinBox?.Text, out float dmin)) _bot.DevMinMs = dmin;
        if (float.TryParse(DevMaxBox?.Text, out float dmax)) _bot.DevMaxMs = dmax;
        if (double.TryParse(MissPctBox?.Text, out double m)) _bot.MissPct = m;
        if (int.TryParse(HoldMinBox?.Text, out int mn)) _bot.HoldMinMs = mn;
        if (int.TryParse(HoldMaxBox?.Text, out int mx)) _bot.HoldMaxMs = mx;
        SaveConfig();
    }

    private void BuildBindingButtons()
    {
        BindingsPanel.Children.Clear();

        for (int i = 0; i < _vm.Bindings.Length; i++)
        {
            int lane = i;
            var btn = MakeBindButton(
                $"{MainViewModel.LaneNames[lane]}: {_vm.Bindings[lane]}",
                Color.FromRgb(0x1a, 0x00, 0x30),
                Color.FromRgb(0xff, 0x55, 0xff));
            btn.Click += (_, _) => StartRebindLane(lane, btn);
            BindingsPanel.Children.Add(btn);
        }

        var startBtn = MakeBindButton(
            $"Start/Stop: {_vm.StartBind}",
            Color.FromRgb(0x00, 0x1a, 0x1a),
            Color.FromRgb(0x55, 0xff, 0xff));
        startBtn.Click += (_, _) => StartRebindStartKey(startBtn);
        BindingsPanel.Children.Add(startBtn);

        var incBtn = MakeBindButton(
            $"Offset+: {_vm.OffsetIncreaseBind}",
            Color.FromRgb(0x00, 0x1a, 0x00),
            Color.FromRgb(0x55, 0xff, 0x55));
        incBtn.Click += (_, _) => StartRebindKey(
            incBtn, "Offset+",
            k => { _vm.OffsetIncreaseBind = k; incBtn.Content = $"Offset+: {k}"; },
            Color.FromRgb(0x55, 0xff, 0x55));
        BindingsPanel.Children.Add(incBtn);

        var decBtn = MakeBindButton(
            $"Offset-: {_vm.OffsetDecreaseBind}",
            Color.FromRgb(0x1a, 0x08, 0x00),
            Color.FromRgb(0xff, 0xaa, 0x55));
        decBtn.Click += (_, _) => StartRebindKey(
            decBtn, "Offset-",
            k => { _vm.OffsetDecreaseBind = k; decBtn.Content = $"Offset-: {k}"; },
            Color.FromRgb(0xff, 0xaa, 0x55));
        BindingsPanel.Children.Add(decBtn);
    }

    private static Button MakeBindButton(string label, Color bg, Color fg) => new()
    {
        Content = label,
        Margin = new Thickness(0, 0, 8, 6),
        Padding = new Thickness(10, 5, 10, 5),
        Background = new SolidColorBrush(bg),
        Foreground = new SolidColorBrush(fg),
        BorderBrush = new SolidColorBrush(fg),
        BorderThickness = new Thickness(1.5),
        FontFamily = new FontFamily("Consolas"),
        Cursor = Cursors.Hand
    };

    private void StartRebindLane(int lane, Button btn)
    {
        btn.Content = $"{MainViewModel.LaneNames[lane]}: [press key...]";
        btn.Foreground = new SolidColorBrush(Colors.Yellow);

        void Handler(object s, KeyEventArgs e)
        {
            e.Handled = true;
            _vm.Bindings[lane] = e.Key;
            btn.Content = $"{MainViewModel.LaneNames[lane]}: {e.Key}";
            btn.Foreground = new SolidColorBrush(Color.FromRgb(0xff, 0x55, 0xff));
            this.KeyDown -= Handler;
            _vm.AddLog($"Rebound {MainViewModel.LaneNames[lane]} → {e.Key}");
            SaveConfig();
        }

        this.KeyDown += Handler;
    }

    private void StartRebindStartKey(Button btn)
    {
        btn.Content = "Start/Stop: [press key...]";
        btn.Foreground = new SolidColorBrush(Colors.Yellow);

        void Handler(object s, KeyEventArgs e)
        {
            e.Handled = true;
            _vm.StartBind = e.Key;
            btn.Content = $"Start/Stop: {e.Key}";
            btn.Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0xff, 0xff));
            this.KeyDown -= Handler;
            _vm.AddLog($"Start/Stop bind → {e.Key}");
            SaveConfig();
        }

        this.KeyDown += Handler;
    }

    private void StartRebindKey(Button btn, string label, Action<Key> apply, Color fg)
    {
        btn.Content = $"{label}: [press key...]";
        btn.Foreground = new SolidColorBrush(Colors.Yellow);

        void Handler(object s, KeyEventArgs e)
        {
            e.Handled = true;
            apply(e.Key);
            btn.Foreground = new SolidColorBrush(fg);
            this.KeyDown -= Handler;
            _vm.AddLog($"{label} bind → {e.Key}");
            SaveConfig();
        }

        this.KeyDown += Handler;
    }

    private void ResetBindings_Click(object sender, RoutedEventArgs e)
    {
        _vm.Bindings = (Key[])MainViewModel.DefaultBindings.Clone();
        _vm.StartBind = Key.F1;
        _vm.OffsetIncreaseBind = Key.F2;
        _vm.OffsetDecreaseBind = Key.F3;
        BuildBindingButtons();
        SaveConfig();
        _vm.AddLog("Keybinds reset to defaults.");
    }

    private void LoadChart_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Open FNF Chart JSON",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            _vm.Chart = ChartLoader.Load(dlg.FileName);
            ChartInfoText.Text = _vm.ChartInfo;
            _vm.AddLog($"Loaded: {_vm.Chart.SongName} — {_vm.Chart.Notes.Count(n => n.Player == 1)} player notes");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load chart:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Chart is null)
        {
            MessageBox.Show("Load a chart first.", "No chart", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!double.TryParse(OffsetBox.Text, out double offset))
        {
            MessageBox.Show("Invalid offset.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (float.TryParse(DevMinBox.Text, out float dmin)) _bot.DevMinMs = dmin;
        if (float.TryParse(DevMaxBox.Text, out float dmax)) _bot.DevMaxMs = dmax;
        if (double.TryParse(MissPctBox.Text, out double m)) _bot.MissPct = m;
        if (int.TryParse(HoldMinBox.Text, out int mn)) _bot.HoldMinMs = mn;
        if (int.TryParse(HoldMaxBox.Text, out int mx)) _bot.HoldMaxMs = mx;
        _bot.PlayAsLeft = PlayerLeft.IsChecked == true;
        _bot.StartFromFirstNote = StartFromFirstNote.IsChecked == true;

        _hitCount = _missCount = 0;
        HitCount.Text = MissCount.Text = "0";

        _bot.Start(_vm.Chart, _vm.Bindings, offset);
        _vm.IsPlaying = true;

        PlayBtn.IsEnabled = false;
        StopBtn.IsEnabled = true;
        StatusText.Text = "● PLAYING";

        _vm.AddLog($"Bot started | offset:{offset}ms | dev:{_bot.DevMinMs}~{_bot.DevMaxMs}ms | miss:{_bot.MissPct}%");

        Task.Run(async () =>
        {
            while (_bot.IsRunning) await Task.Delay(200);
            Dispatcher.Invoke(OnBotStopped);
        });
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _bot.Stop();
        OnBotStopped();
        _vm.AddLog("Bot stopped by user.");
    }

    private void OnBotStopped()
    {
        _vm.IsPlaying = false;
        PlayBtn.IsEnabled = true;
        StopBtn.IsEnabled = false;
        StatusText.Text = "";
    }
}