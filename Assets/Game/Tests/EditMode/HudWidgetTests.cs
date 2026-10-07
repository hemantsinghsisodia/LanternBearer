using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper.Tests
{
// The Phase F2 HUD widgets, checked on the built GameHud prefab (Lantern Keeper > Build UI).
public class HudWidgetTests
{
    const string PrefabPath = "Assets/Game/Prefabs/UI/GameHud.prefab";
    const string ThemePath = "Assets/Game/Art/UI/UITheme.asset";
    const string IconFolder = "Assets/Game/Art/UI/Hud/";

    static readonly string[] IconNames =
    {
        "Icon_Lantern", "Icon_Flame", "Icon_Glow", "Icon_Roof", "Icon_Moth", "Icon_Shield", "Icon_Ember",
        "Icon_TideArrow", "Icon_WindArrow", "Icon_Ring", "Icon_Bolt"
    };

    GameObject hud;
    UITheme theme;

    [SetUp]
    public void SetUp()
    {
        theme = AssetDatabase.LoadAssetAtPath<UITheme>(ThemePath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.IsNotNull(prefab, "GameHud prefab is missing; run Lantern Keeper > Build UI");
        hud = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
    }

    [TearDown]
    public void TearDown()
    {
        if (hud != null)
        {
            Object.DestroyImmediate(hud);
            hud = null;
        }
        Undo.ClearAll();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Undo.ClearAll();
    }

    T One<T>() where T : Component
    {
        T[] all = hud.GetComponentsInChildren<T>(true);
        Assert.AreEqual(1, all.Length, typeof(T).Name + " count");
        return all[0];
    }

    [Test]
    public void PrefabHasAllWidgets()
    {
        One<FuelGaugeWidget>();
        One<BeaconRoofs>();
        One<DrainIcons>();
        One<TimerLabel>();
        One<IslandLabel>();
        One<TideChip>();
        One<StormChip>();
        One<FpsReadout>();
        One<InkPanel>();
        Assert.IsNotNull(hud.GetComponent<TextScaler>(), "TextScaler at the root");

        InkPanel cluster = One<InkPanel>();
        Assert.IsNotNull(cluster.GetComponentInChildren<FuelGaugeWidget>(true), "gauge in the cluster");
        Assert.IsNotNull(cluster.GetComponentInChildren<BeaconRoofs>(true), "roofs in the cluster");

        RectTransform tide = (RectTransform)One<TideChip>().transform;
        RectTransform storm = (RectTransform)One<StormChip>().transform;
        Assert.AreEqual(tide.anchorMin, storm.anchorMin, "tide and storm share a slot");
        Assert.AreEqual(tide.anchoredPosition, storm.anchoredPosition, "tide and storm share a slot");
        Assert.IsFalse(tide.gameObject.activeSelf, "tide starts hidden");
        Assert.IsFalse(storm.gameObject.activeSelf, "storm starts hidden");
    }

    [Test]
    public void IconsAreSpritesAtMost128()
    {
        for (int i = 0; i < IconNames.Length; i++)
        {
            string path = IconFolder + IconNames[i] + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.IsNotNull(importer, path);
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, path);
            Assert.LessOrEqual(importer.maxTextureSize, 128, path);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>(path), path + " loads as a sprite");
        }
    }

    [Test]
    public void RoofsMatchCounts()
    {
        BeaconRoofs roofs = One<BeaconRoofs>();
        roofs.Show(9, 4);
        Assert.AreEqual(9, roofs.VisibleCount);
        Assert.AreEqual(4, roofs.LitCount);
        roofs.Show(5, 5);
        Assert.AreEqual(5, roofs.VisibleCount);
        Assert.AreEqual(5, roofs.LitCount);
        roofs.Show(0, 0);
        Assert.AreEqual(0, roofs.VisibleCount);
        // Unlit roofs are dim brass.
        roofs.Show(3, 1);
        int dim = 0;
        for (int i = 0; i < roofs.Roofs.Count; i++)
        {
            if (roofs.Roofs[i].gameObject.activeSelf && roofs.Roofs[i].color == theme.brassLine)
            {
                dim++;
            }
        }
        Assert.AreEqual(2, dim);
    }

    [Test]
    public void RoofsNeverExceedTemplateLimit()
    {
        BeaconRoofs roofs = One<BeaconRoofs>();
        roofs.Show(30, 30);
        Assert.AreEqual(BeaconRoofs.MaxRoofs, roofs.VisibleCount);
        Assert.AreEqual(BeaconRoofs.MaxRoofs, roofs.LitCount);
        roofs.Show(-3, 5);
        Assert.AreEqual(0, roofs.VisibleCount);
        // 12 roofs fit inside the cluster panel.
        roofs.Show(12, 0);
        RectTransform row = (RectTransform)roofs.transform;
        for (int i = 0; i < 12; i++)
        {
            RectTransform r = (RectTransform)roofs.Roofs[i].transform;
            Assert.LessOrEqual(r.anchoredPosition.x + r.sizeDelta.x, row.rect.width + 0.01f, "roof " + i + " inside the row");
        }
    }

    [Test]
    public void DrainIconsToggle()
    {
        DrainIcons icons = One<DrainIcons>();
        HudMath.DrainIconsView none = HudMath.DrainIcons(new HudMath.DrainState());
        icons.Show(none);
        Assert.IsFalse(icons.MothVisible);
        Assert.IsFalse(icons.ShieldVisible);
        Assert.IsFalse(icons.EmberVisible);
        Assert.IsFalse(icons.MultiplierVisible);

        HudMath.DrainState state = new HudMath.DrainState { MothsDraining = 3, Sprinting = true, DrainRelative = 1.3f };
        icons.Show(HudMath.DrainIcons(state));
        Assert.IsTrue(icons.MothVisible);
        Assert.AreEqual("×3", icons.MothText);
        Assert.IsTrue(icons.EmberVisible);
        Assert.IsFalse(icons.ShieldVisible);
        Assert.IsTrue(icons.MultiplierVisible);
        Assert.AreEqual("×1.3", icons.MultiplierText);

        state.InSafeLight = true;
        icons.Show(HudMath.DrainIcons(state));
        Assert.IsTrue(icons.ShieldVisible);
        Assert.IsFalse(icons.EmberVisible);
        Assert.IsFalse(icons.MultiplierVisible);
    }

    [Test]
    public void FuelGaugeFlameFollowsFuel()
    {
        FuelGaugeWidget gauge = One<FuelGaugeWidget>();
        gauge.Show(1f, 100f, false);
        float fullScale = gauge.FlameScale;
        float fullBar = gauge.BarFill;
        Color fullColour = gauge.FlameColour;
        Assert.AreEqual(1f, fullScale, 0.001f);
        Assert.AreEqual(1f, fullBar, 0.001f);
        Assert.AreEqual(theme.amber, fullColour, "full fuel is amber");

        gauge.Show(0.5f, 100f, false);
        Assert.AreEqual(0.675f, gauge.FlameScale, 0.001f);
        Assert.AreEqual(0.5f, gauge.BarFill, 0.001f);
        Assert.AreEqual(theme.amber, gauge.FlameColour, "still amber above 30%");

        gauge.Show(0.1f, 100f, false);
        Assert.Less(gauge.FlameScale, 0.5f);
        Assert.Greater(gauge.FlameColour.r - gauge.FlameColour.g, fullColour.r - fullColour.g, "redder when low");
        Assert.AreEqual(0.1f, gauge.BarFill, 0.001f);
        Assert.Less(gauge.GlowAlpha, 0.1f);
    }

    [Test]
    public void FuelChangeFlashShowsSignedAmount()
    {
        FuelGaugeWidget gauge = One<FuelGaugeWidget>();
        Assert.IsFalse(gauge.ChangeVisible);
        gauge.Changed(-10f);
        Assert.IsTrue(gauge.ChangeVisible);
        Assert.AreEqual("−10", gauge.ChangeText);
        gauge.Changed(4f);
        Assert.AreEqual("−6", gauge.ChangeText, "changes within a second add up");
    }

    [Test]
    public void FuelFlashColourFollowsCoalescedTotalAndHidesAtZero()
    {
        FuelGaugeWidget gauge = One<FuelGaugeWidget>();
        gauge.Changed(-10f);
        gauge.Changed(4f);
        Assert.AreEqual(FuelGaugeWidget.FlameRed, gauge.ChangeColour, "total is still negative");
        gauge.Changed(20f);
        Assert.AreEqual("+14", gauge.ChangeText);
        Assert.AreEqual(theme.amber, gauge.ChangeColour, "total turned positive");
        gauge.Changed(-14f);
        Assert.IsFalse(gauge.ChangeVisible, "a total of 0 is not shown");
    }

    [Test]
    public void FpsPrefsKeyMatchesGraphicsMenu()
    {
        System.Type menu = System.Type.GetType("LanternKeeper.GraphicsMenu, Assembly-CSharp");
        Assert.IsNotNull(menu);
        object key = menu.GetField("FpsKey").GetRawConstantValue();
        Assert.AreEqual(FpsReadout.FpsKey, key);
    }

    [Test]
    public void TimerIslandAndFpsShowText()
    {
        TimerLabel timer = One<TimerLabel>();
        timer.Show(75f);
        Assert.AreEqual("01:15", timer.Text);
        timer.Show(-1f);
        Assert.AreEqual("--:--", timer.Text);
        timer.SetFormatter(s => "t" + (int)s);
        timer.Show(75f);
        Assert.AreEqual("t75", timer.Text);

        IslandLabel island = One<IslandLabel>();
        island.Show("Island 2 · The Reeds");
        Assert.AreEqual("Island 2 · The Reeds", island.Text);

        FpsReadout fps = One<FpsReadout>();
        bool on = true;
        fps.SetEnabledSource(() => on);
        fps.Refresh();
        Assert.IsTrue(fps.Shown);
        for (int i = 0; i < 40; i++)
        {
            fps.Tick(1f / 60f);
        }
        Assert.AreEqual("60 FPS", fps.Text);
        on = false;
        fps.Refresh();
        Assert.IsFalse(fps.Shown);
    }

    [Test]
    public void TideAndStormChips()
    {
        TideChip tide = One<TideChip>();
        tide.Show(true, true, 0.25f);
        Assert.IsTrue(tide.Active);
        Assert.AreEqual(0f, Mathf.DeltaAngle(0f, tide.ArrowAngle), 0.01f);
        Assert.AreEqual(0.25f, tide.Level, 0.001f);
        Assert.AreEqual("Rising", tide.Text);
        tide.Show(true, false, 0.8f);
        Assert.AreEqual(180f, Mathf.Abs(Mathf.DeltaAngle(0f, tide.ArrowAngle)), 0.01f);
        Assert.AreEqual("Falling", tide.Text);
        tide.Show(false, true, 0.5f);
        Assert.IsFalse(tide.Active);

        StormChip storm = One<StormChip>();
        storm.Show(true, 30f, 0.6f, 0f);
        Assert.IsTrue(storm.Active);
        Assert.AreEqual(-30f, Mathf.DeltaAngle(0f, storm.ArrowAngle), 0.01f);
        Assert.AreEqual(0.6f, storm.ArcFill, 0.001f);
        Assert.IsFalse(storm.ThunderVisible);
        storm.Show(true, 0f, 0f, 0.7f);
        Assert.IsTrue(storm.ThunderVisible);
        Assert.AreEqual(0.7f, storm.ThunderAlpha, 0.001f);
        storm.Show(false, 0f, 0f, 0f);
        Assert.IsFalse(storm.Active);
    }

    [Test]
    public void OnlyThemeFontsAndMin18px()
    {
        TMP_FontAsset[] allowed = { theme.titleFont, theme.flavourFont, theme.uiFont, theme.uiFontStrong };
        TMP_Text[] texts = hud.GetComponentsInChildren<TMP_Text>(true);
        Assert.Greater(texts.Length, 5);
        foreach (TMP_Text text in texts)
        {
            Assert.Contains(text.font, allowed, "theme font on " + text.name);
            Assert.GreaterOrEqual(text.fontSize, UITheme.MinReadableHudFontPx, "size of " + text.name);
            Assert.IsNotNull(text.GetComponent<ThemedLabel>(), text.name + " is a ThemedLabel (follows TextScaler)");
        }
    }

    [Test]
    public void EveryTextHasABacking()
    {
        InkPanel cluster = One<InkPanel>();
        foreach (TMP_Text text in hud.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.transform.IsChildOf(cluster.transform))
            {
                continue;
            }
            bool backed = false;
            for (Transform walk = text.transform.parent; walk != null && walk != hud.transform; walk = walk.parent)
            {
                if (walk.Find("Backing") != null)
                {
                    backed = true;
                    break;
                }
            }
            Assert.IsTrue(backed, text.name + " has an ink backing");
        }
    }

    [Test]
    public void SignalColoursExact()
    {
        Assert.AreEqual("FFB15C", LookPalette.ToHex(theme.amber));
        Assert.AreEqual("B48CFF", LookPalette.ToHex(theme.drainViolet));
        Assert.AreEqual("BCD2FF", LookPalette.ToHex(theme.lightningBlue));

        DrainIcons icons = One<DrainIcons>();
        Assert.AreEqual("B48CFF", LookPalette.ToHex(icons.transform.Find("Moth/Icon").GetComponent<Image>().color), "moth is violet");
        Assert.AreEqual("FFB15C", LookPalette.ToHex(icons.transform.Find("Shield").GetComponent<Image>().color), "shield is amber");

        BeaconRoofs roofs = One<BeaconRoofs>();
        roofs.Show(2, 1);
        Assert.AreEqual("FFB15C", LookPalette.ToHex(roofs.Roofs[0].color), "lit roof is amber");

        StormChip storm = One<StormChip>();
        Assert.AreEqual("BCD2FF", LookPalette.ToHex(storm.transform.Find("WindGauge/Arrow").GetComponent<Image>().color), "storm is lightning blue");
        Assert.AreEqual("BCD2FF", LookPalette.ToHex(storm.transform.Find("Thunder/Bolt").GetComponent<Image>().color), "bolt is lightning blue");

        FuelGaugeWidget gauge = One<FuelGaugeWidget>();
        gauge.Show(0.9f, 100f, false);
        Assert.AreEqual("FFB15C", LookPalette.ToHex(gauge.FlameColour), "flame is amber");
    }
}
}
