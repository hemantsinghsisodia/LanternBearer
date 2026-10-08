using UnityEngine;

namespace LanternKeeper
{
public static class ProceduralAudio
{
    const int Rate = 22050;

    static AudioClip fireflyArrive;
    static AudioClip firefly;
    static AudioClip beacon;
    static AudioClip whoomp;
    static AudioClip footstep;
    static AudioClip moth;
    static AudioClip ambience;
    static AudioClip heartbeat;
    static AudioClip fizzle;
    static AudioClip splash;
    static AudioClip whisper;
    static AudioClip dying;
    static AudioClip surf;
    static AudioClip windBed;
    static AudioClip windHowl;
    static AudioClip thunderRumble;
    static AudioClip thunderCrack;
    static AudioClip shadeDrone;
    static AudioClip rain;

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

    // Soft high bell, about 0.4 s: the motes arriving in the lantern.
    public static AudioClip FireflyArrive()
    {
        if (fireflyArrive != null)
        {
            return fireflyArrive;
        }

        int length = (int)(Rate * 0.4f);
        float[] data = new float[length];
        for (int i = 0; i < length; i++)
        {
            float time = i / (float)Rate;
            float attack = Mathf.Clamp01(time / 0.004f);
            float envelope = attack * Mathf.Exp(-time * 9f);
            float tone = 0.6f * Mathf.Sin(2f * Mathf.PI * 1760f * time);
            tone += 0.25f * Mathf.Sin(2f * Mathf.PI * 2637f * time);
            tone += 0.1f * Mathf.Sin(2f * Mathf.PI * 3520f * time);
            data[i] = tone * envelope * 0.6f;
        }

        fireflyArrive = Clip("FireflyArrive", data);
        return fireflyArrive;
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

    // The deep "whoomp" of a beacon catching: a pitch-dropping sine thump with a soft low-passed rush behind it. About 1.3 s.
    public static AudioClip Whoomp()
    {
        if (whoomp != null)
        {
            return whoomp;
        }

        int length = (int)(Rate * 1.3f);
        float[] data = new float[length];
        System.Random random = new System.Random(23);
        float low = 0f;
        float phase = 0f;
        for (int i = 0; i < length; i++)
        {
            float time = i / (float)Rate;
            float attack = Mathf.Clamp01(time / 0.012f);
            float body = attack * Mathf.Exp(-time * 3.2f);
            float freq = Mathf.Lerp(120f, 38f, 1f - Mathf.Exp(-time * 9f));
            phase += 2f * Mathf.PI * freq / Rate;
            float thump = Mathf.Sin(phase) * body;
            float noise = (float)random.NextDouble() * 2f - 1f;
            low = low * 0.93f + noise * 0.07f;
            float rush = low * Mathf.Sin(Mathf.Clamp01(time / 0.7f) * Mathf.PI) * 0.6f;
            data[i] = Mathf.Clamp((thump * 0.95f + rush * 0.45f) * 0.85f, -1f, 1f);
        }

        whoomp = Clip("BeaconWhoomp", data);
        return whoomp;
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

    public static AudioClip SurfSwell()
    {
        if (surf != null)
        {
            return surf;
        }

        int length = Rate * 6;
        float[] data = new float[length];
        System.Random random = new System.Random(131);
        float low = 0f;
        float mid = 0f;
        for (int i = 0; i < length; i++)
        {
            float time = i / (float)Rate;
            float sample = (float)random.NextDouble() * 2f - 1f;
            low = low * 0.996f + sample * 0.004f;
            mid = mid * 0.95f + sample * 0.05f;
            float swell = 0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * time / 6f - 1.2f);
            swell *= swell;
            data[i] = (low * 5f + mid * 0.35f * swell) * (0.35f + 0.65f * swell);
        }

        FadeEdges(data, (int)(Rate * 0.05f));
        surf = Clip("SurfSwell", data);
        return surf;
    }

    // Loops: every modulation runs a whole number of cycles per clip so the seam stays quiet.
    public static AudioClip WindBed()
    {
        if (windBed != null)
        {
            return windBed;
        }

        int length = Rate * 8;
        float[] data = new float[length];
        System.Random random = new System.Random(401);
        float low = 0f;
        float mid = 0f;
        for (int i = 0; i < length; i++)
        {
            float time = i / (float)Rate;
            float sample = (float)random.NextDouble() * 2f - 1f;
            low = low * 0.992f + sample * 0.008f;
            mid = mid * 0.9f + sample * 0.1f;
            float lull = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * time / 8f) * Mathf.Sin(2f * Mathf.PI * time * 3f / 8f + 0.7f);
            data[i] = (low * 3.4f + mid * 0.25f * lull) * lull;
        }

        FadeEdges(data, (int)(Rate * 0.05f));
        windBed = Clip("WindBed", data);
        return windBed;
    }

    public static AudioClip WindHowl()
    {
        if (windHowl != null)
        {
            return windHowl;
        }

        int length = Rate * 4;
        float[] data = new float[length];
        System.Random random = new System.Random(402);
        float band = 0f;
        float lowpass = 0f;
        float phase = 0f;
        for (int i = 0; i < length; i++)
        {
            float time = i / (float)Rate;
            float sample = (float)random.NextDouble() * 2f - 1f;
            band = band * 0.97f + sample * 0.03f;
            lowpass = lowpass * 0.9f + band * 0.1f;
            float pitch = 340f + 70f * Mathf.Sin(2f * Mathf.PI * time / 4f) + 25f * Mathf.Sin(2f * Mathf.PI * time * 3f / 4f);
            phase += 2f * Mathf.PI * pitch / Rate;
            float tone = Mathf.Sin(phase) * 0.55f + Mathf.Sin(phase * 1.5f) * 0.18f;
            float swell = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * time * 2f / 4f - 1.2f);
            data[i] = (tone * 0.5f + lowpass * 5f) * swell * 0.55f;
        }

        FadeEdges(data, (int)(Rate * 0.08f));
        windHowl = Clip("WindHowl", data);
        return windHowl;
    }

    public static AudioClip ThunderRumble()
    {
        if (thunderRumble != null)
        {
            return thunderRumble;
        }

        int length = (int)(Rate * 3.2f);
        float[] data = new float[length];
        System.Random random = new System.Random(403);
        float low = 0f;
        float lower = 0f;
        for (int i = 0; i < length; i++)
        {
            float time = i / (float)Rate;
            float noise = (float)random.NextDouble() * 2f - 1f;
            low = low * 0.985f + noise * 0.015f;
            lower = lower * 0.997f + noise * 0.003f;
            float attack = Mathf.Clamp01(time / 0.7f);
            float envelope = attack * Mathf.Exp(-Mathf.Max(0f, time - 0.7f) * 1.1f);
            float roll = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 7f * time);
            data[i] = (low * 6f + lower * 14f) * envelope * roll * 0.9f;
        }

        FadeEdges(data, (int)(Rate * 0.05f));
        thunderRumble = Clip("ThunderRumble", data);
        return thunderRumble;
    }

    public static AudioClip ThunderCrack()
    {
        if (thunderCrack != null)
        {
            return thunderCrack;
        }

        int length = (int)(Rate * 1.3f);
        float[] data = new float[length];
        System.Random random = new System.Random(404);
        float low = 0f;
        for (int i = 0; i < length; i++)
        {
            float time = i / (float)Rate;
            float noise = (float)random.NextDouble() * 2f - 1f;
            low = low * 0.93f + noise * 0.07f;
            float snap = noise * Mathf.Exp(-time * 38f);
            float body = low * 1.6f * Mathf.Exp(-time * 6f);
            float thump = Mathf.Sin(2f * Mathf.PI * 52f * time) * Mathf.Exp(-time * 7f);
            data[i] = Mathf.Clamp(snap * 0.9f + body + thump * 0.55f, -1f, 1f) * 0.9f;
        }

        FadeEdges(data, (int)(Rate * 0.01f));
        thunderCrack = Clip("ThunderCrack", data);
        return thunderCrack;
    }

    public static AudioClip ShadeDrone()
    {
        if (shadeDrone != null)
        {
            return shadeDrone;
        }

        int length = Rate * 4;
        float[] data = new float[length];
        System.Random random = new System.Random(405);
        float low = 0f;
        for (int i = 0; i < length; i++)
        {
            float time = i / (float)Rate;
            float noise = (float)random.NextDouble() * 2f - 1f;
            low = low * 0.99f + noise * 0.01f;
            float beat = Mathf.Sin(2f * Mathf.PI * 55f * time) + 0.7f * Mathf.Sin(2f * Mathf.PI * 55.5f * time);
            float fifth = 0.35f * Mathf.Sin(2f * Mathf.PI * 82.5f * time + 0.6f * Mathf.Sin(2f * Mathf.PI * time * 2f / 4f));
            float tremolo = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * time * 3f / 4f);
            data[i] = (beat * 0.3f + fifth + low * 3f) * tremolo * 0.55f;
        }

        FadeEdges(data, (int)(Rate * 0.05f));
        shadeDrone = Clip("ShadeDrone", data);
        return shadeDrone;
    }

    public static AudioClip Rain()
    {
        if (rain != null)
        {
            return rain;
        }

        int length = Rate * 4;
        float[] data = new float[length];
        System.Random random = new System.Random(406);
        float trend = 0f;
        float smooth = 0f;
        for (int i = 0; i < length; i++)
        {
            float time = i / (float)Rate;
            float noise = (float)random.NextDouble() * 2f - 1f;
            smooth = smooth * 0.55f + noise * 0.45f;
            trend = trend * 0.9f + smooth * 0.1f;
            float high = smooth - trend;
            float patter = 0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * time * 5f / 4f) * Mathf.Sin(2f * Mathf.PI * time * 2f / 4f);
            data[i] = high * patter * 0.5f;
        }

        FadeEdges(data, (int)(Rate * 0.05f));
        rain = Clip("Rain", data);
        return rain;
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

    const int VariantCount = 4;

    static AudioClip[] grassSteps;
    static AudioClip[] dirtSteps;
    static AudioClip[] rockSteps;
    static AudioClip[] sandSteps;
    static AudioClip[] chimes;
    static AudioClip[] beacons;
    static AudioClip[] splashes;
    static AudioClip[] fizzles;
    static AudioClip crackle;

    public static int SurfaceVariantCount => VariantCount;

    public static AudioClip FootstepVariant(int surface, int variant)
    {
        AudioClip[] bank = Bank(surface);
        int index = variant < 0 ? 0 : variant;
        if (index >= bank.Length)
        {
            index = bank.Length - 1;
        }

        return bank[index];
    }

    public static AudioClip ChimeVariant(int variant)
    {
        EnsureChimes();
        return chimes[ClampVariant(variant, chimes.Length)];
    }

    public static AudioClip BeaconVariant(int variant)
    {
        EnsureBeacons();
        return beacons[ClampVariant(variant, beacons.Length)];
    }

    public static AudioClip SplashVariant(int variant)
    {
        EnsureSplashes();
        return splashes[ClampVariant(variant, splashes.Length)];
    }

    public static AudioClip FizzleVariant(int variant)
    {
        EnsureFizzles();
        return fizzles[ClampVariant(variant, fizzles.Length)];
    }

    public static AudioClip CrackleLoop()
    {
        if (crackle != null)
        {
            return crackle;
        }

        int length = Rate * 2;
        float[] data = new float[length];
        System.Random random = new System.Random(71);
        float low = 0f;
        for (int i = 0; i < length; i++)
        {
            float time = i / (float)Rate;
            float sample = (float)random.NextDouble() * 2f - 1f;
            low = low * 0.94f + sample * 0.06f;
            float pop = 0f;
            if (random.NextDouble() > 0.985)
            {
                pop = ((float)random.NextDouble() * 2f - 1f) * 0.85f;
            }

            float hum = Mathf.Sin(2f * Mathf.PI * 90f * time) * 0.04f;
            data[i] = low * 0.35f + pop + hum;
        }

        FadeEdges(data, (int)(Rate * 0.02f));
        crackle = Clip("LanternCrackle", data);
        return crackle;
    }

    static AudioClip[] Bank(int surface)
    {
        if (surface == 1)
        {
            EnsureDirt();
            return dirtSteps;
        }

        if (surface == 2)
        {
            EnsureRock();
            return rockSteps;
        }

        if (surface == 3)
        {
            EnsureSand();
            return sandSteps;
        }

        EnsureGrass();
        return grassSteps;
    }

    static void EnsureGrass()
    {
        if (grassSteps != null)
        {
            return;
        }

        grassSteps = new AudioClip[VariantCount];
        for (int i = 0; i < VariantCount; i++)
        {
            grassSteps[i] = NoiseStep("Step_Grass_" + i, 40 + i * 17, 0.15f, 18f, 0.78f, 0.22f);
        }
    }

    static void EnsureDirt()
    {
        if (dirtSteps != null)
        {
            return;
        }

        dirtSteps = new AudioClip[VariantCount];
        for (int i = 0; i < VariantCount; i++)
        {
            dirtSteps[i] = NoiseStep("Step_Dirt_" + i, 80 + i * 13, 0.11f, 32f, 0.48f, 0.5f);
        }
    }

    static void EnsureRock()
    {
        if (rockSteps != null)
        {
            return;
        }

        rockSteps = new AudioClip[VariantCount];
        for (int i = 0; i < VariantCount; i++)
        {
            rockSteps[i] = NoiseStep("Step_Rock_" + i, 120 + i * 11, 0.055f, 62f, 0.12f, 0.9f);
        }
    }

    static void EnsureSand()
    {
        if (sandSteps != null)
        {
            return;
        }

        sandSteps = new AudioClip[VariantCount];
        for (int i = 0; i < VariantCount; i++)
        {
            sandSteps[i] = NoiseStep("Step_Sand_" + i, 160 + i * 19, 0.2f, 12f, 0.9f, 0.08f);
        }
    }

    static void EnsureChimes()
    {
        if (chimes != null)
        {
            return;
        }

        float[] tones = { 740f, 880f, 1046f };
        chimes = new AudioClip[tones.Length];
        for (int v = 0; v < tones.Length; v++)
        {
            int length = Rate;
            float[] data = new float[length];
            float fundamental = tones[v];
            for (int i = 0; i < length; i++)
            {
                float time = i / (float)Rate;
                float envelope = Mathf.Exp(-time * (3.6f + v * 0.4f));
                float tone = 0.62f * Mathf.Sin(2f * Mathf.PI * fundamental * time);
                tone += 0.32f * Mathf.Sin(2f * Mathf.PI * fundamental * 1.5f * time);
                data[i] = tone * envelope * 0.7f;
            }

            chimes[v] = Clip("Chime_" + v, data);
        }
    }

    static void EnsureBeacons()
    {
        if (beacons != null)
        {
            return;
        }

        int[] seeds = { 19, 29, 41 };
        beacons = new AudioClip[seeds.Length];
        for (int v = 0; v < seeds.Length; v++)
        {
            int length = (int)(Rate * 1.1f);
            float[] data = new float[length];
            System.Random random = new System.Random(seeds[v]);
            float low = 0f;
            float thumpHz = 62f + v * 14f;
            for (int i = 0; i < length; i++)
            {
                float time = i / (float)Rate;
                float rise = Mathf.Sin(Mathf.Clamp01(time / 0.85f) * Mathf.PI);
                float noise = (float)random.NextDouble() * 2f - 1f;
                low = low * 0.86f + noise * 0.14f;
                float thump = Mathf.Exp(-time * 10f) * Mathf.Sin(2f * Mathf.PI * thumpHz * time);
                data[i] = (low * rise * 0.75f + thump * 0.85f) * 0.8f;
            }

            beacons[v] = Clip("Beacon_" + v, data);
        }
    }

    static void EnsureSplashes()
    {
        if (splashes != null)
        {
            return;
        }

        int[] seeds = { 47, 59, 73 };
        splashes = new AudioClip[seeds.Length];
        for (int v = 0; v < seeds.Length; v++)
        {
            int length = (int)(Rate * (0.36f + v * 0.05f));
            float[] data = new float[length];
            System.Random random = new System.Random(seeds[v]);
            for (int i = 0; i < length; i++)
            {
                float time = i / (float)Rate;
                float envelope = Mathf.Exp(-time * (6.5f + v));
                float noise = (float)random.NextDouble() * 2f - 1f;
                float bubble = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(480f - v * 40f, 140f, time / 0.42f) * time) * Mathf.Exp(-time * 9f);
                data[i] = (noise * 0.55f + bubble * 0.4f) * envelope;
            }

            splashes[v] = Clip("Splash_" + v, data);
        }
    }

    static void EnsureFizzles()
    {
        if (fizzles != null)
        {
            return;
        }

        int[] seeds = { 31, 37, 53 };
        fizzles = new AudioClip[seeds.Length];
        for (int v = 0; v < seeds.Length; v++)
        {
            int length = (int)(Rate * 0.28f);
            float[] data = new float[length];
            System.Random random = new System.Random(seeds[v]);
            float sputterHz = 18f + v * 7f;
            for (int i = 0; i < length; i++)
            {
                float time = i / (float)Rate;
                float envelope = Mathf.Exp(-time * 11f);
                float noise = (float)random.NextDouble() * 2f - 1f;
                float sputter = Mathf.Abs(Mathf.Sin(2f * Mathf.PI * sputterHz * time)) > 0.45f ? 1f : 0.05f;
                float spark = Mathf.Sin(2f * Mathf.PI * (160f + v * 30f) * time) * Mathf.Exp(-time * 16f);
                data[i] = (noise * 0.65f * sputter + spark * 0.25f) * envelope * 0.8f;
            }

            fizzles[v] = Clip("Fizzle_" + v, data);
        }
    }

    static AudioClip NoiseStep(string clipName, int seed, float seconds, float decay, float lowpass, float brightness)
    {
        int length = Mathf.Max(8, (int)(Rate * seconds));
        float[] data = new float[length];
        System.Random random = new System.Random(seed);
        float low = 0f;
        for (int i = 0; i < length; i++)
        {
            float time = i / (float)Rate;
            float envelope = Mathf.Exp(-time * decay);
            float noise = (float)random.NextDouble() * 2f - 1f;
            low = low * lowpass + noise * (1f - lowpass);
            float click = i < 12 ? noise * brightness : 0f;
            data[i] = (low * (1f - brightness * 0.5f) + click) * envelope * 0.7f;
        }

        return Clip(clipName, data);
    }

    static int ClampVariant(int variant, int count)
    {
        if (variant < 0)
        {
            return 0;
        }

        if (variant >= count)
        {
            return count - 1;
        }

        return variant;
    }

    static void FadeEdges(float[] data, int fade)
    {
        int length = data.Length;
        if (fade <= 0 || fade * 2 >= length)
        {
            return;
        }

        for (int i = 0; i < fade; i++)
        {
            float u = i / (float)fade;
            data[i] *= u;
            data[length - 1 - i] *= u;
        }
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
