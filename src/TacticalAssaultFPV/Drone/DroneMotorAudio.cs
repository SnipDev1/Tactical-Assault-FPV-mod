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
        // Smooth Betaflight ESC 3-tone chime (E5 -> G5 -> C6) with warm bell decay
        int totalSamples = (int)(SampleRate * 0.40f); // 400ms buffer
        float[] samples = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            float t = (float)i / SampleRate;
            float freq = 0f;
            float env = 0f;

            if (t >= 0.00f && t < 0.09f)
            {
                freq = 659.25f; // E5
                float localT = t / 0.09f;
                env = Mathf.Sin(localT * Mathf.PI) * Mathf.Exp(-localT * 2.0f);
            }
            else if (t >= 0.11f && t < 0.20f)
            {
                freq = 783.99f; // G5
                float localT = (t - 0.11f) / 0.09f;
                env = Mathf.Sin(localT * Mathf.PI) * Mathf.Exp(-localT * 2.0f);
            }
            else if (t >= 0.22f && t < 0.36f)
            {
                freq = 1046.50f; // C6
                float localT = (t - 0.22f) / 0.14f;
                env = Mathf.Sin(localT * Mathf.PI) * Mathf.Exp(-localT * 2.0f);
            }

            if (freq > 0f)
            {
                double phase = 2.0 * Math.PI * freq * t;
                double val = Math.Sin(phase); // Pure warm sine wave
                samples[i] = (float)(val * env * 0.65);
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
            AudioSource.PlayClipAtPoint(PowerOnClip, position, 0.85f);
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
        // 360 Hz (deep 3-blade prop hum), 720 Hz (blade pass chop), 1440 Hz (propwash harmonic)
        // 2640 Hz & 3300 Hz (commutation carrier, smooth subtle overtone)
        const double fHum = 360.0;
        const double fBlade = 720.0;
        const double fSub = 1440.0;
        const double fWhine1 = 2640.0;
        const double fWhine2 = 3300.0;

        const double twoPi = 2.0 * Math.PI;
        double wHum = twoPi * fHum / SampleRate;
        double wBlade = twoPi * fBlade / SampleRate;
        double wSub = twoPi * fSub / SampleRate;
        double wWhine1 = twoPi * fWhine1 / SampleRate;
        double wWhine2 = twoPi * fWhine2 / SampleRate;

        // Warmup noise filter to steady state
        float noiseFilter = 0f;
        for (int i = 0; i < 1000; i++)
        {
            float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
            noiseFilter += 0.18f * (white - noiseFilter);
        }

        for (int i = 0; i < totalGenerated; i++)
        {
            // 1. Warm low-frequency propeller blade hum & chop (dominant body)
            double propChop = Math.Sin(wHum * i) * 0.45 +
                              Math.Sin(wBlade * i) * 0.30 +
                              Math.Sin(wSub * i) * 0.12;

            // 2. Soft, subtle electrical ESC commutation buzz (gentle overtone, not piercing)
            double escWhine = Math.Sin(wWhine1 * i) * 0.04 +
                              Math.Sin(wWhine2 * i) * 0.03;

            // 3. Aerodynamic air wash turbulence noise (warm low-pass filter)
            float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
            noiseFilter += 0.18f * (white - noiseFilter);
            double airNoise = noiseFilter * 0.22;

            // 4. Combined signal with analog tanh soft saturation for warm character
            double acc = Math.Tanh((propChop + escWhine + airNoise) * 0.85);

            raw[i] = (float)acc;
        }

        // Copy first n samples to output buffer
        for (int i = 0; i < n; i++)
        {
            s[i] = raw[i];
        }

        // Crossfade 10ms at the loop boundaries to ensure zero boundary pop:
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
        src.spatialBlend = 1.0f; // 100% 3D spatialized in VR world space (never piercing pilot's ears directly)
        src.rolloffMode = AudioRolloffMode.Logarithmic;
        src.minDistance = 2.0f;
        src.maxDistance = 60.0f;
        src.dopplerLevel = 0.4f;
        src.volume = volume;
        return src;
    }

    public static void PlayPayloadSwitchSound(Vector3 pos, int payloadType)
    {
        float freq = payloadType switch
        {
            1 => 660f,  // E5 (Drop Impact)
            2 => 880f,  // A5 (Drop Frag)
            _ => 440f   // A4 (Kamikaze)
        };
        PlayTone(pos, freq, 0.08f, 0.5f);
    }

    public static void PlayPrimeSound(Vector3 pos)
    {
        PlayTone(pos, 1174.66f, 0.06f, 0.6f); // D6 short alert chirp
    }

    public static void PlayDropSound(Vector3 pos)
    {
        PlayTone(pos, 320f, 0.09f, 0.7f); // Low latch clack
    }

    private static void PlayTone(Vector3 pos, float freq, float duration, float volume)
    {
        try
        {
            int samples = (int)(SampleRate * duration);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Sin((float)i / samples * Mathf.PI);
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env;
            }

            AudioClip clip = AudioClip.Create($"Tone_{freq}", samples, 1, SampleRate, false);
            clip.hideFlags = HideFlags.HideAndDontSave;
            clip.SetData(data, 0);

            GameObject go = new GameObject($"ToneAudio_{freq}");
            go.transform.position = pos;
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.spatialBlend = 1f;
            src.minDistance = 0.5f;
            src.maxDistance = 20f;
            src.volume = volume;
            src.Play();
            UnityEngine.Object.Destroy(go, duration + 0.1f);
        }
        catch { }
    }
}
