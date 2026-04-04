using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace FNFBot.Services;

public class BotConfig
{
    public string[] Bindings { get; set; } = { "Left", "Down", "Up", "Right" };
    public string StartBind { get; set; } = "F1";
    public string OffsetIncreaseBind { get; set; } = "F2";
    public string OffsetDecreaseBind { get; set; } = "F3";

    public double OffsetMs { get; set; } = 0;
    public float DevMinMs { get; set; } = 0;
    public float DevMaxMs { get; set; } = 0;
    public double MissPct { get; set; } = 0;
    public int HoldMinMs { get; set; } = 0;
    public int HoldMaxMs { get; set; } = 0;
    public bool PlayAsLeft { get; set; } = false;
    public bool StartFromFirstNote { get; set; } = false;
}

public static class ConfigService
{
    private static readonly string ConfigPath =
        Path.Combine(AppContext.BaseDirectory, "config.json");

    public static BotConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<BotConfig>(json) ?? new BotConfig();
            }
        }
        catch { }
        return new BotConfig();
    }

    public static void Save(BotConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }

    public static Key ParseKey(string name, Key fallback) =>
        Enum.TryParse<Key>(name, out var key) ? key : fallback;
}