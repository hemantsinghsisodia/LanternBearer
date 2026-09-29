using UnityEngine;

namespace LanternKeeper
{
public static class ProceduralAudio
{
    const int Rate = 22050;

    static AudioClip firefly;
    static AudioClip beacon;
    static AudioClip footstep;
    static AudioClip moth;
    static AudioClip ambience;
    static AudioClip heartbeat;
    static AudioClip fizzle;
    static AudioClip splash;
    static AudioClip whisper;
    static AudioClip dying;

    public static AudioClip FireflyChime()
    {
        if (firefly != null)
        {
            return firefly;
        }

        int length = Rate;
        float[] data = new float[length];
        for (int i = 0; i < length; i++)
        {
            float time = i / (float)Rate;
            float envelope = Mathf.Exp(-time * 4.2f);
            float tone = 0.62f * Mathf.Sin(2f * Mathf.PI * 880f * time);
            tone += 0.32f * Mathf.Sin(2f * Mathf.PI * 1320f * time);
            data[i] = tone * envelope * 0.7f;
        }

        firefly = Clip("FireflyChime", data);
        return firefly;
    }

    public static AudioClip BeaconWhoosh()
    {
        if (beacon != null)
        {
            return beacon;
        }

        int length = (int)(Rate * 1.1f);
        float[] data = new float[length];
        System.Random random = new System.Random(19);
        float low = 0f;
        for (int i = 0; i < length; i++)
        {
            float time = i / (float)Rate;
            float rise = Mathf.Sin(Mathf.Clamp01(time / 0.85f) * Mathf.PI);
            float noise = (float)random.NextDouble() * 2f - 1f;
            low = low * 0.86f + noise * 0.14f;
            float thump = Mathf.Exp(-time * 10f) * Mathf.Sin(2f * Mathf.PI * 70f * time);
            data[i] = (low * rise * 0.75f + thump * 0.85f) * 0.8f;
        }

        beacon = Clip("BeaconWhoosh", data);
        return beacon;
    }

    public static AudioClip Footstep()
    {
        if (footstep != null)
        {
            return footstep;
        }

        int length = (int)(Rate * 0.12f);
        float[] data = new float[length];
        System.Random random = new System.Random(4);
        for (int i = 0; i < length; i++)
        {
            float time = i / (float)Rate;
            float envelope = Mathf.Exp(-time * 38f);
            float noise = (float)random.NextDouble() * 2f - 1f;
            data[i] = noise * envelope * 0.55f;
        }

        footstep = Clip("Footstep", data);
        return footstep;
    }

    public static AudioClip MothFlutter()
    {
        if (moth != null)
        {
            return moth;
        }

        int length = Rate;
        float[] data = new float[length];
        System.Random random = new System.Random(11);
        float noise = 0f;
        for (int i = 0; i < length; i++)
        {
            float time = i / (float)Rate;
            float sample = (float)random.NextDouble() * 2f - 1f;
            noise = noise * 0.65f + sample * 0.35f;
            float flutter = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 28f * time));
            data[i] = noise * flutter * 0.45f;
        }

        moth = Clip("MothFlutter", data);
        return moth;
    }

    public static AudioClip Ambience()
    {
        if (ambience != null)
        {
            return ambience;
        }

        int length = Rate * 4;
        float[] data = new float[length];
        System.Random random = new System.Random(23);
        float wind = 0f;
        float gust = 0f;
        for (int i = 0; i < length; i++)
        {
            float time = i / (float)Rate;
            float sample = (float)random.NextDouble() * 2f - 1f;
            wind = wind * 0.985f + sample * 0.015f;
            gust = gust * 0.997f + wind;
            float chirp = 0f;
            float cycle = time % 0.42f;
            if (cycle < 0.045f)
            {
                float env = Mathf.Sin((cycle / 0.045f) * Mathf.PI);
                chirp = env * Mathf.Sin(2f * Mathf.PI * 4100f * time) * 0.22f;
                chirp += env * Mathf.Sin(2f * Mathf.PI * 4700f * time) * 0.08f;
            }

            float second = (time + 0.18f) % 0.73f;
            if (second < 0.03f)
            {
                float env = Mathf.Sin((second / 0.03f) * Mathf.PI);
                chirp += env * Mathf.Sin(2f * Mathf.PI * 3600f * time) * 0.12f;
            }

            data[i] = gust * 0.35f + chirp;
        }

        ambience = Clip("Ambience", data);
        return ambience;
    }

    public static AudioClip Fizzle()
    {
        if (fizzle != null)
        {
            return fizzle;
        }

        int length = (int)(Rate * 0.28f);
        float[] data = new float[length];
        System.Random random = new System.Random(31);
        for (int i = 0; i < length; i++)
        {
            float time = i / (float)Rate;
            float envelope = Mathf.Exp(-time * 11f);
            float noise = (float)random.NextDouble() * 2f - 1f;
            float sputter = Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 23f * time)) > 0.45f ? 1f : 0.05f;
            float spark = Mathf.Sin(2f * Mathf.PI * 180f * time) * Mathf.Exp(-time * 16f);
            data[i] = (noise * 0.65f * sputter + spark * 0.25f) * envelope * 0.8f;
        }

        fizzle = Clip("Fizzle", data);
        return fizzle;
    }

    public static AudioClip Splash()
    {
        if (splash != null)
        {
            return splash;
        }

        int length = (int)(Rate * 0.42f);
        float[] data = new float[length];
        System.Random random = new System.Random(47);
        for (int i = 0; i < length; i++)
        {
            float time = i / (float)Rate;
            float envelope = Mathf.Exp(-time * 7.5f);
            float noise = (float)random.NextDouble() * 2f - 1f;
            float bubble = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(420f, 180f, time) * time) * Mathf.Exp(-time * 9f);
            data[i] = (noise * 0.55f + bubble * 0.4f) * envelope;
        }

        splash = Clip("Splash", data);
        return splash;
    }

    public static AudioClip MothWhisper()
    {
        if (whisper != null)
        {
            return whisper;
        }

        int length = Rate * 2;
        float[] data = new float[length];
        System.Random random = new System.Random(53);
        float noise = 0f;
        for (int i = 0; i < length; i++)
        {
            float time = i / (float)Rate;
            float sample = (float)random.NextDouble() * 2f - 1f;
            noise = noise * 0.92f + sample * 0.08f;
            float flutter = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 9f * time));
            float tone = Mathf.Sin(2f * Mathf.PI * 180f * time) * 0.08f;
            data[i] = (noise * flutter + tone) * 0.55f;
        }

        whisper = Clip("MothWhisper", data);
        return whisper;
    }

    public static AudioClip LanternDying()
    {
        if (dying != null)
        {
            return dying;
        }

        int length = (int)(Rate * 1.3f);
        float[] data = new float[length];
        for (int i = 0; i < length; i++)
        {
            float time = i / (float)Rate;
            float envelope = Mathf.Sin(Mathf.Clamp01(time / 1.15f) * Mathf.PI) * Mathf.Exp(-time * 1.1f);
            float freq = Mathf.Lerp(320f, 90f, time / 1.3f);
            float tone = Mathf.Sin(2f * Mathf.PI * freq * time);
            float crackle = Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 17f * time)) > 0.82f ? 0.25f : 0f;
            data[i] = (tone * 0.55f + crackle) * envelope * 0.7f;
        }

        dying = Clip("LanternDying", data);
        return dying;
    }

    public static AudioClip Heartbeat()
    {
        if (heartbeat != null)
        {
            return heartbeat;
        }

        int length = Rate;
        float[] data = new float[length];
        for (int i = 0; i < length; i++)
        {
            float time = i / (float)Rate;
            float first = Pulse(time, 0.05f);
            float second = Pulse(time, 0.28f);
            data[i] = (first + second * 0.75f) * 0.8f;
        }

        heartbeat = Clip("Heartbeat", data);
        return heartbeat;
    }

    static float Pulse(float time, float at)
    {
        float delta = time - at;
        if (delta < 0f || delta > 0.12f)
        {
            return 0f;
        }

        return Mathf.Exp(-delta * 28f) * Mathf.Sin(2f * Mathf.PI * 58f * delta);
    }

    static AudioClip Clip(string clipName, float[] data)
    {
        AudioClip clip = AudioClip.Create(clipName, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        clip.name = clipName;
        return clip;
    }
}
}
