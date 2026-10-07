using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class ShadeVisualTests
{
    const string PrefabPath = "Assets/Game/Prefabs/Gameplay/Shade.prefab";
    const string BodyMaterialPath = "Assets/Game/Art/Creatures/Shade/ShadeWraith.mat";
    const string ReduceKey = "LanternKeeperReduceFlashing";

    GameObject instance;
    GameObject lightningHost;
    bool hadReduceKey;
    int oldReduce;

    [SetUp]
    public void SetUp()
    {
        // Remember the player's Reduce flashing so the test can flip it and put it back.
        hadReduceKey = PlayerPrefs.HasKey(ReduceKey);
        oldReduce = PlayerPrefs.GetInt(ReduceKey, 0);
    }

    [TearDown]
    public void TearDown()
    {
        if (instance != null)
        {
            UnityEngine.Object.DestroyImmediate(instance);
            instance = null;
        }

        if (lightningHost != null)
        {
            UnityEngine.Object.DestroyImmediate(lightningHost);
            lightningHost = null;
        }

        if (hadReduceKey)
        {
            PlayerPrefs.SetInt(ReduceKey, oldReduce);
        }
        else
        {
            PlayerPrefs.DeleteKey(ReduceKey);
        }

        Undo.ClearAll();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Undo.ClearAll();
    }

    static GameObject Prefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.IsNotNull(prefab, "Shade.prefab");
        return prefab;
    }

    static Type VisualType()
    {
        Type type = Type.GetType("LanternKeeper.ShadeVisual, Assembly-CSharp");
        Assert.IsNotNull(type, "ShadeVisual type");
        return type;
    }

    static string BurnHex()
    {
        FieldInfo field = VisualType().GetField("BurnEdgeHex", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(field, "BurnEdgeHex");
        return (string)field.GetValue(null);
    }

    static object Level(string name)
    {
        Type type = Type.GetType("LanternKeeper.GraphicsLevel, Assembly-CSharp");
        Assert.IsNotNull(type, "GraphicsLevel");
        return Enum.Parse(type, name);
    }

    static float Call(string method, params object[] args)
    {
        MethodInfo info = VisualType().GetMethod(method, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(info, method);
        return (float)info.Invoke(null, args);
    }

    static Type ShadeType()
    {
        Type type = Type.GetType("LanternKeeper.Shade, Assembly-CSharp");
        Assert.IsNotNull(type, "Shade type");
        return type;
    }

    static void SetState(Component shade, int stateValue)
    {
        FieldInfo field = ShadeType().GetField("state", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "Shade.state");
        field.SetValue(shade, Enum.ToObject(field.FieldType, stateValue));
    }

    static void Tick(Component visual, float dt)
    {
        MethodInfo method = VisualType().GetMethod("Tick");
        Assert.IsNotNull(method, "ShadeVisual.Tick");
        method.Invoke(visual, new object[] { dt });
    }

    static float Get(Component visual, string property)
    {
        PropertyInfo info = VisualType().GetProperty(property);
        Assert.IsNotNull(info, property);
        return (float)info.GetValue(visual);
    }

    // The ShadeState enum values: Chase 0, Creep 1, Freeze 2, Retreat 3, Reforming 4, Stunned 5.
    const int Chase = 0;
    const int Freeze = 2;
    const int Stunned = 5;

    Component NewVisual(out Component shade)
    {
        instance = (GameObject)PrefabUtility.InstantiatePrefab(Prefab());
        shade = instance.GetComponent(ShadeType());
        Assert.IsNotNull(shade, "Shade component");
        Component visual = instance.GetComponent(VisualType());
        Assert.IsNotNull(visual, "ShadeVisual component");
        return visual;
    }

    [Test]
    public void PrefabUsesShadeShader()
    {
        GameObject prefab = Prefab();
        Assert.IsNotNull(prefab.GetComponent(VisualType()), "ShadeVisual on the root");

        Renderer body = prefab.transform.Find("Body").GetComponent<Renderer>();
        Assert.IsNotNull(body.sharedMaterial);
        Assert.AreEqual("LanternKeeper/Shade", body.sharedMaterial.shader.name);
        Assert.IsTrue(body.sharedMaterial.enableInstancing, "instancing on the body material");
        StringAssert.StartsWith("Assets/Game/Art/Creatures/Shade/", AssetDatabase.GetAssetPath(body.sharedMaterial));
        Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, body.shadowCastingMode);

        foreach (Renderer eye in prefab.transform.Find("Eyes").GetComponentsInChildren<Renderer>(true))
        {
            Assert.AreEqual("LanternKeeper/ShadeEye", eye.sharedMaterial.shader.name, eye.name);
        }

        ParticleSystemRenderer trail = prefab.transform.Find("Smoke").GetComponent<ParticleSystemRenderer>();
        Assert.AreEqual("LanternKeeper/ShadeSmoke", trail.sharedMaterial.shader.name);
        Assert.AreEqual(ParticleSystemSimulationSpace.World, prefab.transform.Find("Smoke").GetComponent<ParticleSystem>().main.simulationSpace);
    }

    // Review focus 4: the outline follows the capped flash, never the raw one.
    [Test]
    public void OutlineFollowsCappedFlash()
    {
        PlayerPrefs.SetInt(ReduceKey, 1);
        lightningHost = new GameObject("LightningHost");
        Component lightning = lightningHost.AddComponent(Type.GetType("LanternKeeper.Lightning, Assembly-CSharp"));
        Type lightningType = lightning.GetType();
        // Edit mode does not run the Unity messages: do what the player loop would.
        lightningType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(lightning, null);
        lightningType.GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(lightning, null);
        lightningType.GetMethod("DebugFlash", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(lightning, null);

        Component shade;
        Component visual = NewVisual(out shade);
        Tick(visual, 0.016f);
        float cap = SettingsMath.FlashCap(true);
        float outline = Get(visual, "Outline");
        Assert.Greater(outline, 0.1f, "the forced flash draws an outline");
        Assert.LessOrEqual(outline, cap + 0.0001f, "capped by Reduce flashing");

        PlayerPrefs.SetInt(ReduceKey, 0);
        Tick(visual, 0.016f);
        Assert.AreEqual(1f, Get(visual, "Outline"), 0.0001f, "full flash with Reduce flashing off");
        Assert.LessOrEqual(SettingsMath.FlashCap(false), 1f);
    }

    // Review focus 5: the burn clears when the Shade is released.
    [Test]
    public void FreezeBurnClearsOnRelease()
    {
        Component shade;
        Component visual = NewVisual(out shade);
        SetState(shade, Freeze);
        float elapsed = 0f;
        while (Get(visual, "Burn") < 1f && elapsed < 2f)
        {
            Tick(visual, 0.02f);
            elapsed += 0.02f;
        }

        Assert.AreEqual(1f, Get(visual, "Burn"), 0.0001f, "burn reaches 1 while frozen");

        SetState(shade, Chase);
        for (int i = 0; i < 17; i++)
        {
            Tick(visual, 0.02f);
        }

        // 0.34 s after release.
        Assert.AreEqual(0f, Get(visual, "Burn"), 0.0001f, "burn is gone within 0.35 s of release");
    }

    // The stunned look comes from an explicit _Stunned property, not from the blue channel of Shade.cs's tint.
    [Test]
    public void StunnedSignalIsExplicit()
    {
        Component shade;
        Component visual = NewVisual(out shade);
        Tick(visual, 0.02f);
        Assert.AreEqual(0f, Get(visual, "Stunned"), 0f, "not stunned");

        SetState(shade, Stunned);
        for (int i = 0; i < 10; i++)
        {
            Tick(visual, 0.02f);
        }

        Assert.AreEqual(1f, Get(visual, "Stunned"), 0.0001f, "eases to 1 while stunned");
        VisualType().GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(visual, null);
        Renderer body = instance.transform.Find("Body").GetComponent<Renderer>();
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        body.GetPropertyBlock(block);
        Assert.AreEqual(1f, block.GetFloat("_Stunned"), 0.0001f, "_Stunned in the property block");

        SetState(shade, Chase);
        for (int i = 0; i < 10; i++)
        {
            Tick(visual, 0.02f);
        }

        Assert.AreEqual(0f, Get(visual, "Stunned"), 0.0001f, "eases back to 0");

        string source = System.IO.File.ReadAllText("Assets/Game/Shaders/Shade.shader");
        StringAssert.Contains("_Stunned", source);
        StringAssert.DoesNotContain("baseColor.b", source, "the blue channel trick is gone");
    }

    [Test]
    public void BurnStepIsLinearAndClamped()
    {
        float burn = 0f;
        for (int i = 0; i < 100; i++)
        {
            burn = Call("StepBurn", burn, true, 0.02f);
        }

        Assert.AreEqual(1f, burn, 0f);
        Assert.AreEqual(0.5f, Call("StepBurn", 1f, false, 0.15f), 0.0001f);
        Assert.AreEqual(0f, Call("StepBurn", 0.1f, false, 1f), 0f);
    }

    [Test]
    public void OutlineColourIsLightningBlue()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(BodyMaterialPath);
        Assert.IsNotNull(material, "ShadeWraith.mat");
        Color outline = material.GetColor("_OutlineColor");
        Color blue = LookPalette.FromHex(LookPalette.LightningBlue);
        Assert.AreEqual(blue.r, outline.r, 0.003f);
        Assert.AreEqual(blue.g, outline.g, 0.003f);
        Assert.AreEqual(blue.b, outline.b, 0.003f);
        Assert.AreEqual("BCD2FF", LookPalette.ToHex(outline));
    }

    // Amber (#FFB15C) means safe warmth, so the burn edge must read as ash and ember, not as lantern light.
    [Test]
    public void BurnEdgeIsNotAmber()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(BodyMaterialPath);
        Color burn = material.GetColor("_BurnColor");
        Assert.AreEqual(BurnHex(), LookPalette.ToHex(burn));
        float h, s, v, ah, asat, av;
        Color.RGBToHSV(burn, out h, out s, out v);
        Color.RGBToHSV(LookPalette.FromHex(LookPalette.LanternAmber), out ah, out asat, out av);
        Assert.AreEqual("FFB15C", LookPalette.LanternAmber);
        Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(h * 360f, ah * 360f)), 8f, "hue is redder than amber");
        Assert.Less(s, asat - 0.1f, "less saturated than amber");
        Assert.Less(v, av - 0.2f, "dimmer than amber");
    }

    [Test]
    public void LowShortensAndThinsTheTrail()
    {
        Assert.Less(Call("TrailLifetimeScale", Level("Low")), 1f);
        Assert.AreEqual(1f, Call("TrailLifetimeScale", Level("Ultra")), 0f);
        Assert.Less(Call("TrailRate", 10f, 4f, Level("Low")), Call("TrailRate", 10f, 4f, Level("Ultra")));
        Assert.Less(Call("TrailRate", 10f, 0f, Level("Ultra")), Call("TrailRate", 10f, 5f, Level("Ultra")), "faster, thicker trail");
    }

    [Test]
    public void SpeedFollowsMovement()
    {
        Component shade;
        Component visual = NewVisual(out shade);
        Tick(visual, 0.02f);
        for (int i = 0; i < 40; i++)
        {
            instance.transform.position += new Vector3(0f, 0f, 4.5f * 0.02f);
            Tick(visual, 0.02f);
        }

        Assert.AreEqual(4.5f, Get(visual, "Speed"), 0.3f);
    }

    [Test]
    public void ShadeColliderAndLogicUnchanged()
    {
        GameObject prefab = Prefab();
        // The wraith carried no collider before the shader work (Shade.cs steers by raycasts), so none may appear.
        Assert.AreEqual(0, prefab.GetComponentsInChildren<Collider>(true).Length);
        Assert.IsNotNull(prefab.transform.Find("Body"), "Body");
        Assert.IsNotNull(prefab.transform.Find("Eyes/EyeLeft"), "EyeLeft");
        Assert.IsNotNull(prefab.transform.Find("Eyes/EyeRight"), "EyeRight");
        Assert.IsNotNull(prefab.transform.Find("Smoke"), "Smoke");
        Assert.IsNotNull(prefab.GetComponent(ShadeType()), "Shade logic component");

        // The mesh is still the hollow wraith.
        MeshFilter filter = prefab.transform.Find("Body").GetComponent<MeshFilter>();
        StringAssert.EndsWith("Shade.fbx", AssetDatabase.GetAssetPath(filter.sharedMesh));

        // Pure logic is untouched.
        Assert.AreEqual(4.5f, ShadeLogic.SpeedFor(ShadeState.Chase), 0f);
        Assert.AreEqual(0.8f, ShadeLogic.SpeedFor(ShadeState.Creep), 0f);
        Assert.AreEqual(3f, ShadeLogic.SpeedFor(ShadeState.Retreat), 0f);
        Assert.AreEqual(0f, ShadeLogic.SpeedFor(ShadeState.Freeze), 0f);
        Assert.AreEqual(ShadeState.Chase, ShadeLogic.StateFor(0.1f, 0f));
        Assert.AreEqual(ShadeState.Creep, ShadeLogic.StateFor(0.3f, 0f));
        Assert.AreEqual(ShadeState.Freeze, ShadeLogic.StateFor(0.7f, 0f));
        Assert.AreEqual(ShadeState.Retreat, ShadeLogic.StateFor(0.7f, 2f));
        Assert.AreEqual(1f, ShadeLogic.TouchRadius, 0f);
    }
}
}
