using System.IO;
using FNFBot.Models;
using Newtonsoft.Json.Linq;

namespace FNFBot.Services;

public static class ChartLoader
{
    public static Chart Load(string path)
    {
        var text = File.ReadAllText(path);
        var root = JObject.Parse(text);

        if (root["format"]?.ToString() == "psych_v1")
            return ParsePsychV1Format(root);

        if (root["song"] is JObject songObj)
            return ParseFnfFormat(songObj);

        return ParseSimpleFormat(root);
    }

    /// <summary>
    /// Returns true only for real notes. Event entries (e.g. "Cam Boom Speed")
    /// embed a string in slot [2] instead of a numeric duration — skip those.
    /// </summary>
    private static bool IsRealNote(JToken n)
    {
        if (n.Count() < 3) return false;
        var durToken = n[2];
        return durToken?.Type != JTokenType.String;
    }

    private static Chart ParsePsychV1Format(JObject root)
    {
        var chart = new Chart
        {
            SongName = root["song"]?.ToString() ?? "Unknown",
            Bpm = root["bpm"]?.Value<double>() ?? 120,
            Speed = root["speed"]?.Value<double>() ?? 1.0
        };

        double bpm = chart.Bpm;
        double stepCrochet = StepCrochet(bpm);
        double currentMs = 0;

        var sections = root["notes"] as JArray ?? new JArray();

        foreach (var section in sections)
        {
            bool changeBpm = section["changeBPM"]?.Value<bool>() ?? false;
            double sectionBpm = section["bpm"]?.Value<double>() ?? 0;
            if (changeBpm && sectionBpm > 0)
            {
                bpm = sectionBpm;
                stepCrochet = StepCrochet(bpm);
            }

            bool mustHitSection = section["mustHitSection"]?.Value<bool>() ?? true;
            var sectionNotes = section["sectionNotes"] as JArray ?? new JArray();
            int sectionBeats = section["sectionBeats"]?.Value<int>() ?? 4;

            foreach (var n in sectionNotes)
            {
                if (!IsRealNote(n)) continue; // skip camera events and other non-note entries

                double rawTime = n[0]?.Value<double>() ?? 0;
                int rawLane = n[1]?.Value<int>() ?? 0;
                double dur = n[2]?.Value<double>() ?? 0;

                double noteTimeMs = rawTime > 0 ? rawTime : currentMs;

                int player = rawLane < 4 ? 1 : 0;
                int lane = rawLane < 4 ? rawLane : rawLane - 4;

                chart.Notes.Add(new Note
                {
                    TimeMs = noteTimeMs,
                    Lane = lane,
                    Duration = dur,
                    Player = player
                });
            }

            currentMs += StepCrochet(bpm) * 4 * sectionBeats;
        }

        chart.Notes.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
        return chart;
    }

    private static Chart ParseFnfFormat(JObject song)
    {
        var chart = new Chart
        {
            SongName = song["song"]?.ToString() ?? "Unknown",
            Bpm = song["bpm"]?.Value<double>() ?? 120,
            Speed = song["speed"]?.Value<double>() ?? 1.0
        };

        double bpm = chart.Bpm;
        double stepCrochet = StepCrochet(bpm);
        double currentMs = 0;

        var sections = song["notes"] as JArray ?? new JArray();

        foreach (var section in sections)
        {
            bool changeBpm = section["changeBPM"]?.Value<bool>() ?? false;
            double sectionBpm = section["bpm"]?.Value<double>() ?? 0;
            if (changeBpm && sectionBpm > 0)
            {
                bpm = sectionBpm;
                stepCrochet = StepCrochet(bpm);
            }

            bool mustHitSection = section["mustHitSection"]?.Value<bool>() ?? true;
            var sectionNotes = section["sectionNotes"] as JArray ?? new JArray();

            foreach (var n in sectionNotes)
            {
                if (!IsRealNote(n)) continue; // skip camera events and other non-note entries

                double rawTime = n[0]?.Value<double>() ?? 0;
                int rawLane = n[1]?.Value<int>() ?? 0;
                double dur = n[2]?.Value<double>() ?? 0;

                double noteTimeMs = rawTime > 0 ? rawTime : currentMs;

                int player, lane;
                if (rawLane < 4)
                {
                    player = mustHitSection ? 1 : 0;
                    lane = rawLane;
                }
                else
                {
                    player = mustHitSection ? 0 : 1;
                    lane = rawLane - 4;
                }

                chart.Notes.Add(new Note
                {
                    TimeMs = noteTimeMs,
                    Lane = lane,
                    Duration = dur,
                    Player = player
                });
            }

            currentMs += stepCrochet * 16;
        }

        chart.Notes.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
        return chart;
    }

    private static double StepCrochet(double bpm) => (60_000.0 / bpm) / 4.0;

    private static Chart ParseSimpleFormat(JObject root)
    {
        var chart = new Chart
        {
            SongName = root["songName"]?.ToString() ?? "Unknown",
            Bpm = root["bpm"]?.Value<double>() ?? 120,
            Speed = root["speed"]?.Value<double>() ?? 1.0
        };

        var notes = root["notes"] as JArray ?? new JArray();
        foreach (var n in notes)
        {
            chart.Notes.Add(new Note
            {
                TimeMs = n["time"]?.Value<double>() ?? 0,
                Lane = n["lane"]?.Value<int>() ?? 0,
                Duration = n["duration"]?.Value<double>() ?? 0,
                Player = n["player"]?.Value<int>() ?? 1
            });
        }

        chart.Notes.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
        return chart;
    }
}