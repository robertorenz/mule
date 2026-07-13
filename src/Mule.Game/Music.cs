using System;
using Microsoft.Xna.Framework.Audio;

namespace Mule.Game;

/// <summary>
/// A looping chiptune title theme, synthesized at startup (no audio files). It's an
/// original tune written to capture the FEEL of the classic M.U.L.E. title music: a
/// bright, swung shuffle rhythm over a rolling boogie-woogie bass line, with a light
/// off-beat hi-hat tick. Guarded: no audio device = silence.
/// </summary>
public sealed class Music : IDisposable
{
    private const int Rate = 22050;
    private const double BeatDur = 0.30;  // seconds per quarter note (~200 BPM feel, upbeat)
    private const double Swing = 0.63;    // on-beat eighth takes 63% of the beat

    private enum Osc { Square, Triangle }

    // Lead (square). (MIDI, length in eighths); 0 = rest. 64 eighths = 8 bars.
    // Built around a four-note hook (G-C6-G-E) that recurs so it sticks in the ear.
    private static readonly (int midi, int len)[] Lead =
    {
        (79,1),(84,1),(79,1),(76,1),(72,2),(74,2),                        // bar 1  hook + turn
        (79,1),(84,1),(79,1),(76,1),(76,2),(72,2),                        // bar 2  hook (repeat)
        (81,1),(84,1),(81,1),(77,1),(77,2),(81,2),                        // bar 3  hook up (F)
        (79,1),(84,1),(79,1),(76,1),(79,2),(0,2),                         // bar 4  hook + lift
        (83,1),(86,1),(83,1),(79,1),(79,2),(74,2),                        // bar 5  hook up (G)
        (81,1),(84,1),(81,1),(77,1),(74,2),(77,2),                        // bar 6  (F)
        (79,1),(84,1),(79,1),(76,1),(72,2),(76,2),                        // bar 7  hook (C)
        (74,1),(71,1),(74,1),(79,1),(72,4),                               // bar 8  turnaround to C
    };

    // Boogie-woogie bass (triangle): root-3-5-6-b7-6-5-3 per bar — the shuffle engine.
    private static readonly (int midi, int len)[] Bass = BuildBoogie(new[]
    {
        48, 48, 53, 48, 55, 53, 48, 55, // C C F C G F C G
    });

    private readonly SoundEffectInstance? _instance;
    private readonly bool _ready;

    /// <summary>Which track is playing: the external file path, or "synthesized".</summary>
    public string Source { get; private set; } = "none";

    public Music()
    {
        try
        {
            SoundEffect theme;
            var external = FindExternalPath();
            if (external != null)
            {
                using var fs = System.IO.File.OpenRead(external);
                theme = SoundEffect.FromStream(fs); // WAV (PCM)
                Source = "external: " + external;
            }
            else
            {
                theme = Build();
                Source = "synthesized";
            }

            _instance = theme.CreateInstance();
            _instance.IsLooped = true;
            _instance.Volume = external != null ? 0.6f : 0.34f;
            _ready = true;
        }
        catch (Exception ex)
        {
            // An external file that won't load shouldn't leave us silent — fall back.
            try
            {
                var fx = Build();
                _instance = fx.CreateInstance();
                _instance.IsLooped = true;
                _instance.Volume = 0.34f;
                _ready = true;
                Source = $"synthesized (fallback: {ex.Message})";
            }
            catch { _ready = false; }
        }

        Console.WriteLine($"[Music] {Source}");
    }

    /// <summary>
    /// Finds a user-provided WAV to use as title music: the MULE_MUSIC path, then
    /// "music/title.wav" beside the executable or in the working directory.
    /// </summary>
    private static string? FindExternalPath()
    {
        var candidates = new System.Collections.Generic.List<string>();
        var env = Environment.GetEnvironmentVariable("MULE_MUSIC");
        if (!string.IsNullOrEmpty(env)) candidates.Add(env);
        candidates.Add(System.IO.Path.Combine(AppContext.BaseDirectory, "music", "title.wav"));
        candidates.Add(System.IO.Path.Combine(Environment.CurrentDirectory, "music", "title.wav"));

        foreach (var path in candidates)
            if (System.IO.File.Exists(path))
                return path;
        return null;
    }

    public void Play()
    {
        if (!_ready) return;
        try { if (_instance!.State != SoundState.Playing) _instance.Play(); }
        catch { }
    }

    public void Stop()
    {
        if (!_ready) return;
        try { _instance!.Stop(); }
        catch { }
    }

    public void Dispose()
    {
        try { _instance?.Dispose(); } catch { }
    }

    /// <summary>Turns a list of bar roots into an eight-notes-to-the-bar boogie line.</summary>
    private static (int midi, int len)[] BuildBoogie(int[] roots)
    {
        int[] steps = { 0, 4, 7, 9, 10, 9, 7, 4 }; // major-6 / b7 walk-up-and-down
        var notes = new (int, int)[roots.Length * steps.Length];
        int k = 0;
        foreach (int root in roots)
            foreach (int step in steps)
                notes[k++] = (root + step, 1);
        return notes;
    }

    private static SoundEffect Build()
    {
        var pcm = BuildBuffer();
        return new SoundEffect(pcm, Rate, AudioChannels.Mono);
    }

    /// <summary>Synthesizes the theme to 16-bit mono PCM. No audio device needed.</summary>
    private static byte[] BuildBuffer()
    {
        int totalEighths = 0;
        foreach (var (_, len) in Lead) totalEighths += len;
        int totalSamples = (int)(SwingTime(totalEighths) * Rate) + 1;

        var mix = new int[totalSamples];
        RenderVoice(mix, Lead, Osc.Square, volume: 0.20, gate: 0.92);
        RenderVoice(mix, Bass, Osc.Triangle, volume: 0.30, gate: 0.80);
        RenderHats(mix, totalEighths, volume: 0.06);

        var buffer = new byte[totalSamples * 2];
        for (int i = 0; i < totalSamples; i++)
        {
            int v = Math.Clamp(mix[i], short.MinValue, short.MaxValue);
            buffer[i * 2] = (byte)(v & 0xFF);
            buffer[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
        return buffer;
    }

    /// <summary>Writes the theme to a standard 16-bit mono WAV file (for auditioning).</summary>
    public static void WriteWav(string path)
    {
        var pcm = BuildBuffer();
        using var fs = System.IO.File.Create(path);
        using var w = new System.IO.BinaryWriter(fs);
        int byteRate = Rate * 2;
        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + pcm.Length);
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);            // fmt chunk size
        w.Write((short)1);      // PCM
        w.Write((short)1);      // mono
        w.Write(Rate);
        w.Write(byteRate);
        w.Write((short)2);      // block align
        w.Write((short)16);     // bits per sample
        w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        w.Write(pcm.Length);
        w.Write(pcm);
    }

    /// <summary>Maps an eighth-note index to a time in seconds with a swing feel.</summary>
    private static double SwingTime(int eighth)
    {
        int beat = eighth / 2;
        bool offBeat = (eighth & 1) == 1;
        return beat * BeatDur + (offBeat ? Swing * BeatDur : 0.0);
    }

    private static void RenderVoice(int[] mix, (int midi, int len)[] notes, Osc osc, double volume, double gate)
    {
        int pos = 0;
        foreach (var (midi, len) in notes)
        {
            if (midi > 0)
            {
                double start = SwingTime(pos);
                double dur = (SwingTime(pos + len) - start) * gate; // gate < 1 => bounce
                double freq = 440.0 * Math.Pow(2, (midi - 69) / 12.0);
                int s0 = (int)(start * Rate);
                int s1 = (int)((start + dur) * Rate);
                for (int i = s0; i < s1 && i < mix.Length; i++)
                {
                    double t = i / (double)Rate;
                    double phase = freq * t;
                    double raw = osc == Osc.Square
                        ? (Math.Sin(2 * Math.PI * phase) >= 0 ? 1 : -1)
                        : 2.0 / Math.PI * Math.Asin(Math.Sin(2 * Math.PI * phase)); // triangle
                    double lt = (i - s0) / (double)Rate;
                    mix[i] += (int)(raw * Envelope(lt, dur) * volume * short.MaxValue);
                }
            }
            pos += len;
        }
    }

    /// <summary>A short noise tick on every off-beat for a shuffle groove.</summary>
    private static void RenderHats(int[] mix, int totalEighths, double volume)
    {
        var rng = new Random(7);
        for (int e = 1; e < totalEighths; e += 2) // off-beats only
        {
            double start = SwingTime(e);
            int s0 = (int)(start * Rate);
            int s1 = s0 + (int)(0.03 * Rate);
            for (int i = s0; i < s1 && i < mix.Length; i++)
            {
                double lt = (i - s0) / (double)Rate;
                double env = Math.Exp(-lt * 120);
                mix[i] += (int)((rng.NextDouble() * 2 - 1) * env * volume * short.MaxValue);
            }
        }
    }

    private static double Envelope(double t, double dur)
    {
        const double attack = 0.006, release = 0.03;
        double a = t < attack ? t / attack : 1.0;
        double r = t > dur - release ? Math.Max(0, (dur - t) / release) : 1.0;
        return a * r;
    }
}
