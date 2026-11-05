using System;
using UnityEngine;

namespace TacticalAssaultFPV.Drone;

/// <summary>
/// Procedural FPV quad acoustic synthesis.
/// Synthesizes authentic brushless motor ESC PWM carrier whine + 3-blade propeller chop + air wash.
/// Integer cycles per 1.0s buffer guarantee a 100% click-free seamless loop.
/// </summary>
public static class DroneMotorAudio
{
    private const int SampleRate = 44100;
    private static AudioClip _motorClip;
    private static AudioClip _powerOnClip;

    public static AudioClip MotorClip
    {
        get
        {
            if (_motorClip == null)
            {
                _motorClip = BuildMotorClip();
            }
            return _motorClip;
        }
    }

    public static AudioClip PowerOnClip
    {
        get
        {
            if (_powerOnClip == null)
            {
                _powerOnClip = BuildPowerOnClip();
            }
            return _powerOnClip;
        }
    }

    private static AudioClip BuildPowerOnClip()
    {
        // Authentic Betaflight / BLHeli 3-tone ESC startup chime:
        // Tone 1: 659 Hz (E5) 0ms - 90ms
        // Tone 2: 784 Hz (G5) 110ms - 200ms
        // Tone 3: 1046 Hz (C6) 220ms - 360ms
        int totalSamples = (int)(SampleRate * 0.40f); // 400ms buffer
        float[] samples = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float freq = 0f;
            float env = 0f;

            if (t >= 0.00f && t < 0.09f)
            {
                freq = 659.25f;
                float localT = t / 0.09f;
                env = Mathf.Sin(localT * Mathf.PI);
            }
            else if (t >= 0.11f && t < 0.20f)
            {
                freq = 783.99f;
                float localT = (t - 0.11f) / 0.09f;
                env = Mathf.Sin(localT * Mathf.PI);
            }
            else if (t >= 0.22f && t < 0.36f)
            {
                freq = 1046.50f;
                float localT = (t - 0.22f) / 0.14f;
                env = Mathf.Sin(localT * Mathf.PI);
            }

            if (freq > 0f)
            {
                double phase = 2.0 * Math.PI * freq * t;
                double val = Math.Sin(phase) + 0.35 * Math.Sin(3.0 * phase);
                samples[i] = (float)(val * env * 0.85);
            }
        }

        AudioClip clip = AudioClip.Create("FPV_PowerOn_Chime", totalSamples, 1, SampleRate, false);
        clip.hideFlags = HideFlags.HideAndDontSave;
        clip.SetData(samples, 0);
        return clip;
    }

    public static void PlayPowerOnSound(Vector3 position)
    {
        try
        {
            AudioSource.PlayClipAtPoint(PowerOnClip, position, 1.0f);
        }
        catch { }
    }

    private static AudioClip BuildMotorClip()
    {
        int n = SampleRate; // 1.0 s @ 44.1 kHz
        int crossfadeSamples = SampleRate / 100; // 10ms boundary crossfade
        int totalGenerated = n + crossfadeSamples;
        float[] raw = new float[totalGenerated];
        float[] s = new float[n];
        var rnd = new System.Random(1337);

        // Frequencies chosen with exact integer cycles in 1.0 second:
        // 720 Hz (3-blade propeller pass chop), 1440 Hz (2nd prop harmonic),
        // 2640 Hz & 3300 Hz (14-pole brushless motor ESC commutation carrier singing whine)
        const double fBlade = 720.0;
        const double fSub = 1440.0;
        const double fWhine1 = 2640.0;
        const double fWhine2 = 3300.0;

        const double twoPi = 2.0 * Math.PI;
        double wBlade = twoPi * fBlade / SampleRate;
        double wSub = twoPi * fSub / SampleRate;
        double wWhine1 = twoPi * fWhine1 / SampleRate;
        double wWhine2 = twoPi * fWhine2 / SampleRate;

        // Warmup noise filter to steady state
        float noiseFilter = 0f;
        for (int i = 0; i < 1000; i++)
        {
            float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
            noiseFilter += 0.25f * (white - noiseFilter);
        }

        for (int i = 0; i < totalGenerated; i++)
        {
            // 1. High-frequency ESC PWM whine
            double whine = Math.Sin(wWhine1 * i) * 0.35 +
                           Math.Sin(wWhine2 * i) * 0.20 +
                           Math.Sin(wSub * i) * 0.25;

            // 2. Propeller blade chop
            double blade = Math.Sin(wBlade * i) * 0.40;

            // 3. Air slice turbulence noise (gentle low-pass smoothed)
            float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
            noiseFilter += 0.25f * (white - noiseFilter);
            double airNoise = noiseFilter * 0.15;

            // 4. Combined signal with analog tanh soft saturation
            double acc = Math.Tanh((whine + blade + airNoise) * 0.85);

            raw[i] = (float)acc;
        }

        // Copy first n samples to output buffer
        for (int i = 0; i < n; i++)
        {
            s[i] = raw[i];
        }

        // Crossfade 10ms at the loop boundaries to ensure zero boundary pop:
        // raw[n + i] is the direct phase-continuous continuation of raw[n - 1] (= s[n - 1]).
        // At i = 0 (loop wrap), s[0] equals raw[n], which smoothly follows s[n - 1] without any phase jump.
        float peak = 0f;
        for (int i = 0; i < crossfadeSamples; i++)
        {
            float t = (float)i / crossfadeSamples;
            float blend = t * t * (3f - 2f * t); // smoothstep
            s[i] = s[i] * blend + raw[n + i] * (1f - blend);
        }

        for (int i = 0; i < n; i++)
        {
            peak = Mathf.Max(peak, Mathf.Abs(s[i]));
        }

        // Normalize to solid 0.85f peak
        if (peak > 0.001f)
        {
            float norm = 0.85f / peak;
            for (int i = 0; i < n; i++) s[i] *= norm;
        }

        AudioClip clip = AudioClip.Create("FPV_Motor", n, 1, SampleRate, false);
        clip.hideFlags = HideFlags.HideAndDontSave;
        clip.SetData(s, 0);
        return clip;
    }

    public static AudioSource CreateSource(GameObject host, AudioClip clip, float volume)
    {
        AudioSource src = host.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 0.70f; // Authentic FPV pilot telemetry audio: 70% 3D spatial world, 30% direct headset/speaker
        src.rolloffMode = AudioRolloffMode.Logarithmic;
        src.minDistance = 3.0f;
        src.maxDistance = 200.0f;
        src.dopplerLevel = 0.6f;
        src.volume = volume;
        return src;
    }
}
