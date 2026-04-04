namespace FNFBot.Models;

public class Chart
{
    public string SongName { get; set; } = "";
    public double Bpm { get; set; } = 120;
    public double Speed { get; set; } = 1.0;

    public List<Note> Notes { get; set; } = new();
}