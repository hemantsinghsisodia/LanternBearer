using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LanternKeeper.Tests
{
// The credits roll built into Island 5: every line fits the column at 130% text, and the roll is wired to the HUD.
public class CreditsLayoutTests
{
    const string ScenePath = "Assets/Game/Scenes/Island5.unity";

    static Scene Open(out bool opened)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene loaded = SceneManager.GetSceneAt(i);
            if (loaded.isLoaded && loaded.path == ScenePath)
            {
                opened = false;
                return loaded;
            }
        }

        opened = true;
        return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
    }

    [Test]
    public void CreditsRollFitsAt130Percent()
    {
        bool opened;
        Scene scene = Open(out opened);
        float savedScale = UserSettings.TextScale;
        UserSettings.DeferSave = true;
        Component hud = null;
        System.Type hudType = System.Type.GetType("LanternKeeper.HUD, Assembly-CSharp");
        Assert.IsNotNull(hudType, "HUD type");
        CreditsRoll roll = null;
        try
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (hud == null) hud = root.GetComponentInChildren(hudType, true);
                if (roll == null) roll = root.GetComponentInChildren<CreditsRoll>(true);
            }

            Assert.IsNotNull(hud, "Island5 HUD");
            Assert.IsNotNull(roll, "Island5 has the CreditsRoll");
            SerializedObject so = new SerializedObject(hud);
            Assert.AreSame(roll, so.FindProperty("credits").objectReferenceValue, "HUD.credits is wired");

            UserSettings.TextScale = 1f;
            roll.Build();
            Assert.LessOrEqual(roll.Seconds, 75f, "the roll lasts 75 s or less at 100% text (" + roll.Seconds.ToString("F1") + " s)");

            UserSettings.TextScale = 1.3f;
            roll.Build();
            Canvas.ForceUpdateCanvases();
            TMP_Text[] texts = roll.Content.GetComponentsInChildren<TMP_Text>(false);
            Assert.Greater(texts.Length, 80, "lines built");
            float width = roll.Content.rect.width;
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextOverflowing, "'" + text.text + "' overflows");
                Vector2 wanted = text.GetPreferredValues(text.text, text.rectTransform.rect.width, 0f);
                Assert.LessOrEqual(wanted.y, text.rectTransform.rect.height + 1f, "'" + text.text + "' needs more height than it has");
                Assert.LessOrEqual(text.rectTransform.rect.width, width + 1f, "'" + text.text + "' is wider than the column");
            }

            Assert.Greater(roll.ScrollLength, 3000f, "scroll length");
        }
        finally
        {
            if (roll != null)
            {
                roll.Clear();
            }

            UserSettings.TextScale = savedScale;
            UserSettings.DeferSave = false;
            if (opened)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
}
