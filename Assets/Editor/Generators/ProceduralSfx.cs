using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Synthesises the sound effects the project was missing, as 16-bit mono WAVs.
///
/// Written in code for the same reason HudSprites draws the HUD art in code: it is
/// re-runnable, it lives in the repo as the thing that produced it rather than as an
/// opaque binary, and the character stays consistent because every sound is built from
/// the same handful of parts.
///
/// The house style is "punchy analog synth arcade", which in practice means: hard
/// attacks, short exponential decays, detuned oscillator pairs for the analog wobble,
/// and a lowpass that opens or closes across the sound rather than sitting still. No
/// reverb tails - the game is top-down and busy, and long tails just turn to mud.
///
/// Idempotent: a file is only rewritten when its bytes actually change, so re-running
/// this does not churn the repo.
/// </summary>
public static class ProceduralSfx
{
    public const string Dir = "Assets/Audio/Generated/";

    const int SampleRate = 44100;

    struct Spec
    {
        public string file;
        public Func<float[]> render;
    }

    static Spec[] All => new[]
    {
        new Spec { file = "sfx_tree_impact.wav",  render = TreeImpact },
        new Spec { file = "sfx_reload_ready.wav", render = ReloadReady },
        new Spec { file = "sfx_ui_move.wav",      render = UiMove },
        new Spec { file = "sfx_low_hull.wav",     render = LowHull },
    };

    [MenuItem("Tools/Tankeo/14 - Generate SFX")]
    public static void Generate()
    {
        Directory.CreateDirectory(Dir);

        int written = 0;
        foreach (var spec in All)
            if (Write(spec.file, spec.render())) written++;

        if (written > 0) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Import();
        Debug.Log("[SFX] " + written + " of " + All.Length + " regenerated.");
    }

    public static void Import()
    {
        foreach (var spec in All)
        {
            string path = Dir + spec.file;
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) continue;

            var settings = importer.defaultSampleSettings;
            // These are all well under a second. Decompressing on load costs nothing and
            // avoids the per-play decode hitch that matters in WebGL.
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;

            bool dirty = importer.defaultSampleSettings.loadType != settings.loadType ||
                         importer.defaultSampleSettings.compressionFormat != settings.compressionFormat ||
                         importer.forceToMono != true ||
                         importer.preloadAudioData != true;

            if (!dirty) continue;

            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.preloadAudioData = true;
            importer.SaveAndReimport();
        }
    }

    // =========================================================================
    // The sounds
    // =========================================================================

    /// <summary>
    /// Ramming a tree: a low body thud with a bright splinter crack over the top.
    /// The thud carries the weight of a 500kg tank; the crack is what makes it read as
    /// wood rather than as another explosion.
    /// </summary>
    static float[] TreeImpact()
    {
        int length = Seconds(0.42f);
        var buffer = new float[length];
        var rng = new System.Random(20260806);

        double bodyPhase = 0, splinterCut = 0;

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / SampleRate;

            // Body: a fast downward sweep, which is what a heavy impact sounds like.
            float bodyHz = Mathf.Lerp(150f, 55f, Ease(t / 0.16f));
            bodyPhase += bodyHz / SampleRate;
            float body = (float)Math.Sin(bodyPhase * 2.0 * Math.PI) * Env(t, 0.004f, 0.17f);

            // Crack: noise pushed through a closing lowpass so it starts bright and
            // collapses into a dull rustle.
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            float cutoff = Mathf.Lerp(0.85f, 0.06f, Ease(t / 0.28f));
            splinterCut += (noise - splinterCut) * cutoff;
            float crack = (float)splinterCut * Env(t, 0.001f, 0.09f) * 0.9f;

            // A couple of late transients: the tree giving way rather than one clean hit.
            float splinter = 0f;
            if (t > 0.10f && t < 0.30f && rng.NextDouble() < 0.0016)
                splinter = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.5f;

            buffer[i] = body * 0.85f + crack * 0.55f + splinter;
        }

        return Normalise(buffer, 0.92f);
    }

    /// <summary>
    /// The gun coming back online: two short square blips a fifth apart. Musical rather
    /// than mechanical, so it can fire every half second without grating.
    /// </summary>
    static float[] ReloadReady()
    {
        int length = Seconds(0.14f);
        var buffer = new float[length];

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / SampleRate;

            float a = Blip(t, 0f, 784f, 0.045f);          // G5
            float b = Blip(t, 0.055f, 1175f, 0.055f);     // D6

            buffer[i] = (a + b) * 0.5f;
        }

        return Normalise(buffer, 0.7f);
    }

    /// <summary>
    /// Menu focus moving. Extremely short and quiet - it fires on every hover, so
    /// anything with a tail becomes noise immediately.
    /// </summary>
    static float[] UiMove()
    {
        int length = Seconds(0.05f);
        var buffer = new float[length];

        double phase = 0;

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / SampleRate;

            // Slight downward slide stops it sounding like an error beep.
            float hz = Mathf.Lerp(1650f, 1320f, Ease(t / 0.04f));
            phase += hz / SampleRate;

            buffer[i] = Square(phase) * Env(t, 0.001f, 0.022f);
        }

        return Normalise(buffer, 0.45f);
    }

    /// <summary>
    /// Hull critical. A detuned saw pair pulsed twice - the detune is the analog part,
    /// and it sits in the same register as the HUD's alarm state without being a siren.
    /// </summary>
    static float[] LowHull()
    {
        int length = Seconds(0.62f);
        var buffer = new float[length];

        double phaseA = 0, phaseB = 0, cut = 0;

        for (int i = 0; i < length; i++)
        {
            float t = (float)i / SampleRate;

            // Two pulses, the second a tone higher: reads as a warning rather than a note.
            float pulse = Env(t, 0.006f, 0.12f) + Env(t - 0.28f, 0.006f, 0.16f);
            float hz = t < 0.28f ? 233f : 262f;

            phaseA += hz / SampleRate;
            phaseB += hz * 1.006f / SampleRate;           // ~10 cents apart

            float raw = (Saw(phaseA) + Saw(phaseB)) * 0.5f;

            // Fixed lowpass: saws are harsh at this level without one.
            cut += (raw - cut) * 0.16f;

            buffer[i] = (float)cut * Mathf.Clamp01(pulse);
        }

        return Normalise(buffer, 0.78f);
    }

    // =========================================================================
    // Synthesis parts
    // =========================================================================

    static int Seconds(float s) => Mathf.Max(1, Mathf.RoundToInt(SampleRate * s));

    /// <summary>Percussive envelope: near-instant attack, exponential decay.</summary>
    static float Env(float t, float attack, float decay)
    {
        if (t < 0f) return 0f;
        if (t < attack) return attack <= 0f ? 1f : t / attack;
        return Mathf.Exp(-(t - attack) / Mathf.Max(0.0001f, decay));
    }

    /// <summary>Ease-out, for pitch sweeps that fall fast then settle.</summary>
    static float Ease(float k)
    {
        k = Mathf.Clamp01(k);
        return 1f - (1f - k) * (1f - k);
    }

    static float Square(double phase) => (phase % 1.0) < 0.5 ? 1f : -1f;

    static float Saw(double phase) => (float)((phase % 1.0) * 2.0 - 1.0);

    /// <summary>One square-wave blip starting at <paramref name="start"/>.</summary>
    static float Blip(float t, float start, float hz, float decay)
    {
        float local = t - start;
        if (local < 0f) return 0f;
        return Square(local * hz) * Env(local, 0.001f, decay);
    }

    /// <summary>Scales to a target peak, so every generated sound lands at a known level.</summary>
    static float[] Normalise(float[] buffer, float peak)
    {
        float max = 0f;
        for (int i = 0; i < buffer.Length; i++) max = Mathf.Max(max, Mathf.Abs(buffer[i]));
        if (max < 0.0001f) return buffer;

        float scale = peak / max;
        for (int i = 0; i < buffer.Length; i++) buffer[i] = Mathf.Clamp(buffer[i] * scale, -1f, 1f);

        // Short fade at the tail so nothing ends on a discontinuity and clicks.
        int fade = Mathf.Min(256, buffer.Length);
        for (int i = 0; i < fade; i++)
        {
            int index = buffer.Length - 1 - i;
            buffer[index] *= (float)i / fade;
        }

        return buffer;
    }

    // =========================================================================
    // WAV
    // =========================================================================

    /// <summary>Writes a 16-bit mono PCM WAV, skipping the write if nothing changed.</summary>
    static bool Write(string file, float[] samples)
    {
        byte[] bytes = Encode(samples);
        string path = Dir + file;

        if (File.Exists(path) && Same(File.ReadAllBytes(path), bytes)) return false;

        File.WriteAllBytes(path, bytes);
        return true;
    }

    static byte[] Encode(float[] samples)
    {
        int dataBytes = samples.Length * 2;

        using (var stream = new MemoryStream(44 + dataBytes))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E' });

            writer.Write(new[] { 'f', 'm', 't', ' ' });
            writer.Write(16);                       // PCM header size
            writer.Write((short)1);                 // format: PCM
            writer.Write((short)1);                 // channels: mono
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);           // byte rate
            writer.Write((short)2);                 // block align
            writer.Write((short)16);                // bits per sample

            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);

            for (int i = 0; i < samples.Length; i++)
                writer.Write((short)Mathf.Clamp(samples[i] * short.MaxValue,
                                                short.MinValue, short.MaxValue));

            writer.Flush();
            return stream.ToArray();
        }
    }

    static bool Same(byte[] a, byte[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
}
