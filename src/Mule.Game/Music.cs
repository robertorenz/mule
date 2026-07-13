using System;
using Microsoft.Xna.Framework.Audio;

namespace Mule.Game;

/// <summary>
/// A looping chiptune title theme, synthesized at startup (no audio files). It's an
/// original, upbeat tune written in the spirit of the classic M.U.L.E. title music —
/// a square-wave lead over a rounded bass line. Guarded: no audio device = silence.
/// </summary>
public sealed class Music : IDisposable
{
    private const int Rate = 22050;
    private const double Eighth = 0.2; // seconds per eighth note

    // (MIDI note, length in eighths); note 0 = rest.
    private static readonly (int midi, int len)[] Lead =
    {
        (67, 2), (72, 1), (76, 1), (79, 2), (76, 1), (72, 1),   // bar 1
        (74, 2), (77, 1), (81, 1), (79, 4),                     // bar 2
        (76, 2), (72, 1), (76, 1), (74, 2), (71, 1), (74, 1),   // bar 3
        (72, 4), (0, 4),                                        // bar 4
    };

    private static readonly (int midi, int len)[] Bass =
    {
        (48, 2), (52, 2), (55, 2), (52, 2),                     // C
        (50, 2), (53, 2), (57, 2), (53, 2),                     // Dm
        (48, 2), (52, 2), (55, 2), (59, 2),                     // C -> B
        (48, 2), (55, 2), (48, 2), (0, 2),                      // C
    };

    private readonly SoundEffectInstance? _instance;
    private readonly bool _ready;

    public Music()
    {
        try
        {
            var theme = Build();
            _instance = theme.CreateInstance();
            _instance.IsLooped = true;
            _instance.Volume = 0.32f;
            _ready = true;
        }
        catch
        {
            _ready = false;
        }
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

    private static SoundEffect Build()
    {
        int totalEighths = 0;
        foreach (var (_, len) in Lead) totalEighths += len;
        int totalSamples = (int)(totalEighths * Eighth * Rate);

        var mix = new int[totalSamples];
        RenderVoice(mix, Lead, square: true, volume: 0.22);
        RenderVoice(mix, Bass, square: false, volume: 0.28);

        var buffer = new byte[totalSamples * 2];
        for (int i = 0; i < totalSamples; i++)
        {
            int v = Math.Clamp(mix[i], short.MinValue, short.MaxValue);
            buffer[i * 2] = (byte)(v & 0xFF);
            buffer[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
        return new SoundEffect(buffer, Rate, AudioChannels.Mono);
    }

    private static void RenderVoice(int[] mix, (int midi, int len)[] notes, bool square, double volume)
    {
        double time = 0;
        foreach (var (midi, len) in notes)
        {
            double dur = len * Eighth;
            if (midi > 0)
            {
                double freq = 440.0 * Math.Pow(2, (midi - 69) / 12.0);
                int start = (int)(time * Rate);
                int end = (int)((time + dur) * Rate);
                for (int i = start; i < end && i < mix.Length; i++)
                {
                    double lt = (i - start) / (double)Rate;
                    double raw = Math.Sin(2 * Math.PI * freq * (i / (double)Rate));
                    if (square) raw = raw >= 0 ? 1 : -1;
                    double sample = raw * Envelope(lt, dur) * volume;
                    mix[i] += (int)(sample * short.MaxValue);
                }
            }
            time += dur;
        }
    }

    private static double Envelope(double t, double dur)
    {
        const double attack = 0.008, release = 0.04;
        double a = t < attack ? t / attack : 1.0;
        double r = t > dur - release ? Math.Max(0, (dur - t) / release) : 1.0;
        return a * r;
    }
}
