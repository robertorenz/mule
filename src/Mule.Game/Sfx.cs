using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;

namespace Mule.Game;

/// <summary>
/// Tiny procedural sound-effects bank. Tones are synthesized at startup into short
/// PCM buffers, so the game needs no audio asset files. Everything is guarded: if
/// there's no audio device (e.g. a headless run), it silently does nothing rather
/// than crash.
/// </summary>
public sealed class Sfx
{
    private const int SampleRate = 22050;

    private readonly Dictionary<string, SoundEffect> _sounds = new();
    private readonly bool _ready;

    public Sfx()
    {
        try
        {
            _sounds["move"] = Tone((330, 0.045f), square: true, volume: 0.25f);
            _sounds["confirm"] = Tone((523, 0.06f), (784, 0.09f));       // rising two-note
            _sounds["pop"] = Tone((659, 0.05f), volume: 0.4f);
            _sounds["trade"] = Tone((1046, 0.05f), volume: 0.35f);       // bright coin ping
            _sounds["event"] = Tone((262, 0.14f), volume: 0.4f);        // low sting
            _sounds["gameover"] = Tone((523, 0.12f), (392, 0.12f), (330, 0.2f));
            _ready = true;
        }
        catch
        {
            _ready = false; // no audio device — run silently
        }
    }

    public void Play(string name, float volume = 0.5f)
    {
        if (!_ready) return;
        try
        {
            if (_sounds.TryGetValue(name, out var s))
                s.Play(volume, 0f, 0f);
        }
        catch { /* audio hiccup — ignore */ }
    }

    /// <summary>Synthesizes a sequence of decaying notes into one SoundEffect.</summary>
    private static SoundEffect Tone(params (double freq, float seconds)[] notes)
        => Tone(notes, square: false, volume: 0.5f);

    private static SoundEffect Tone((double freq, float seconds) note, bool square = false, float volume = 0.5f)
        => Tone(new[] { note }, square, volume);

    private static SoundEffect Tone((double freq, float seconds)[] notes, bool square, float volume)
    {
        int total = 0;
        foreach (var n in notes) total += (int)(SampleRate * n.seconds);
        var buffer = new byte[total * 2];

        int idx = 0;
        foreach (var (freq, seconds) in notes)
        {
            int samples = (int)(SampleRate * seconds);
            for (int i = 0; i < samples; i++)
            {
                double t = i / (double)SampleRate;
                double env = Math.Exp(-t * 11.0); // quick percussive decay
                double raw = Math.Sin(2 * Math.PI * freq * t);
                if (square) raw = raw >= 0 ? 1 : -1;
                short s = (short)(raw * env * volume * short.MaxValue);
                buffer[idx++] = (byte)(s & 0xFF);
                buffer[idx++] = (byte)((s >> 8) & 0xFF);
            }
        }

        return new SoundEffect(buffer, SampleRate, AudioChannels.Mono);
    }
}
