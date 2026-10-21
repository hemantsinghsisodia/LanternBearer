using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
// The on-screen touch controls, live on Island 1, driven by a simulated Touchscreen (state events queued and processed by the player loop).
// Game types (Assembly-CSharp) are reached by reflection; GameInput and TouchMode live in the Input assembly. TearDown resets every static the overlay touches.
public class TouchControlsTest
{
    static readonly string[] IntroKeys =
    {
        "LanternKeeperIntro_island1", "LanternKeeperIntro_island2", "LanternKeeperIntro_island3", "LanternKeeperIntro_island4", "LanternKeeperIntro_island5"
    };

    readonly Dictionary<string, int> savedInts = new Dictionary<string, int>();
    readonly Dictionary<int, Vector2> starts = new Dictionary<int, Vector2>();
    Touchscreen screen;
    Keyboard addedKeyboard;
    Mouse disabledMouse;
    int savedGraphics;

    static Type GameType(string name)
    {
        Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name + " type not found");
        return type;
    }

    static UnityEngine.Object Find(string typeName)
    {
        return UnityEngine.Object.FindAnyObjectByType(GameType(typeName));
    }

    static T Get<T>(object target, string member)
    {
        Type type = target.GetType();
        PropertyInfo property = type.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
        if (property != null)
        {
            return (T)property.GetValue(target);
        }

        FieldInfo field = type.GetField(member, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(field, type.Name + "." + member + " not found");
        return (T)field.GetValue(target);
    }

    static void SetProperty(object target, string member, object value)
    {
        PropertyInfo property = target.GetType().GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(property, target.GetType().Name + "." + member + " not found");
        property.SetValue(target, value);
    }

    static T GetStatic<T>(string typeName, string member)
    {
        Type type = GameType(typeName);
        FieldInfo field = type.GetField(member, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(field, typeName + "." + member + " not found");
        return (T)field.GetValue(null);
    }

    static void SetStatic(string typeName, string member, object value)
    {
        FieldInfo field = GameType(typeName).GetField(member, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(field, typeName + "." + member + " not found");
        field.SetValue(null, value);
    }

    static object Overlay()
    {
        PropertyInfo property = GameType("TouchControlsOverlay").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(property, "TouchControlsOverlay.Instance");
        return property.GetValue(null);
    }

    [SetUp]
    public void SetUp()
    {
        LitKeyGuard.Save();
        GameInput.ResetForTests();
        savedInts.Clear();
        for (int i = 0; i < IntroKeys.Length; i++)
        {
            savedInts[IntroKeys[i]] = PlayerPrefs.HasKey(IntroKeys[i]) ? PlayerPrefs.GetInt(IntroKeys[i]) : -1;
            // A first-visit intro card pauses the game and hides the controls.
            PlayerPrefs.SetInt(IntroKeys[i], 1);
        }

        savedGraphics = PlayerPrefs.GetInt("LanternKeeperGraphics", 0);
        PlayerPrefs.SetInt("LanternKeeperGraphics", 0);
        starts.Clear();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (screen != null)
        {
            InputSystem.RemoveDevice(screen);
            screen = null;
        }

        if (addedKeyboard != null)
        {
            InputSystem.RemoveDevice(addedKeyboard);
            addedKeyboard = null;
        }
        else if (Keyboard.current != null)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
        }

        if (disabledMouse != null)
        {
            InputSystem.EnableDevice(disabledMouse);
            disabledMouse = null;
        }

        TouchMode.SetActiveForTests(false);
        GameInput.ResetForTests();
        SetStatic("PlayerController", "ExternalMove", Vector2.zero);
        SetStatic("PlayerController", "ExternalSprint", false);
        SetStatic("CameraFollow", "ExternalLookDelta", Vector2.zero);
        LitKeyGuard.Restore();
        foreach (KeyValuePair<string, int> pair in savedInts)
        {
            if (pair.Value < 0)
            {
                PlayerPrefs.DeleteKey(pair.Key);
            }
            else
            {
                PlayerPrefs.SetInt(pair.Key, pair.Value);
            }
        }

        PlayerPrefs.SetInt("LanternKeeperGraphics", savedGraphics);
        PlayerPrefs.Save();
        AsyncOperation load = SceneManager.LoadSceneAsync("MainMenu");
        while (!load.isDone)
        {
            yield return null;
        }

        yield return null;
    }

    static IEnumerator Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return null;
        }
    }

    static IEnumerator Seconds(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end)
        {
            yield return null;
        }
    }

    // Loads Island 1 with touch mode on and waits for the controls to be fully up.
    IEnumerator Prepare()
    {
        AsyncOperation load = SceneManager.LoadSceneAsync("Island1");
        while (!load.isDone)
        {
            yield return null;
        }

        yield return Frames(3);
        // A real mouse move would (correctly) switch touch mode off in the middle of a test.
        if (Mouse.current != null && Mouse.current.enabled)
        {
            disabledMouse = Mouse.current;
            InputSystem.DisableDevice(disabledMouse);
        }

        screen = InputSystem.AddDevice<Touchscreen>();
        TouchMode.SetActiveForTests(true);
        yield return WaitOverlay(true);
    }

    IEnumerator WaitOverlay(bool shown)
    {
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < deadline)
        {
            object overlay = Overlay();
            if (overlay != null)
            {
                bool isShown = Get<bool>(overlay, "IsShown");
                float alpha = Get<float>(overlay, "Alpha");
                if (shown ? isShown && alpha >= 0.99f : !isShown && alpha <= 0.01f)
                {
                    yield break;
                }
            }

            yield return null;
        }

        Assert.Fail("the overlay did not become " + (shown ? "shown" : "hidden") + " within 10 s");
    }

    void Touch(int id, Vector2 position, UnityEngine.InputSystem.TouchPhase phase)
    {
        if (phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            starts[id] = position;
        }

        InputSystem.QueueStateEvent(screen, new TouchState
        {
            touchId = id,
            phase = phase,
            position = position,
            startPosition = starts[id]
        });
    }

    // Presses at `from`, then drags to `to` over `steps` frames and holds `holdFrames`. The finger stays down; call Lift to release it.
    IEnumerator Drag(int id, Vector2 from, Vector2 to, int steps, int holdFrames)
    {
        Touch(id, from, UnityEngine.InputSystem.TouchPhase.Began);
        yield return null;
        for (int i = 1; i <= steps; i++)
        {
            Touch(id, Vector2.Lerp(from, to, i / (float)steps), UnityEngine.InputSystem.TouchPhase.Moved);
            yield return null;
        }

        for (int i = 0; i < holdFrames; i++)
        {
            Touch(id, to, UnityEngine.InputSystem.TouchPhase.Stationary);
            yield return null;
        }
    }

    IEnumerator Lift(int id, Vector2 at)
    {
        Touch(id, at, UnityEngine.InputSystem.TouchPhase.Ended);
        yield return null;
        yield return null;
    }

    IEnumerator Tap(int id, Vector2 at)
    {
        Touch(id, at, UnityEngine.InputSystem.TouchPhase.Began);
        yield return null;
        yield return Lift(id, at);
    }

    static Component Keeper()
    {
        Component player = (Component)Find("PlayerController");
        Assert.IsNotNull(player, "PlayerController");
        return player;
    }

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator LeftDragMovesTheKeeper()
    {
        yield return Prepare();
        Component keeper = Keeper();
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!Get<bool>(keeper, "IsGrounded") && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Vector3 before = keeper.transform.position;
        float w = Screen.width;
        float h = Screen.height;
        Vector2 from = new Vector2(w * 0.2f, h * 0.35f);
        Vector2 to = from + new Vector2(0f, h * 0.25f);
        yield return Drag(1, from, to, 6, 0);
        Assert.AreNotEqual(Vector2.zero, GetStatic<Vector2>("PlayerController", "ExternalMove"), "the stick drives ExternalMove");
        yield return Seconds(1.0f);
        Vector3 after = keeper.transform.position;
        Vector3 moved = after - before;
        moved.y = 0f;
        Assert.Greater(moved.magnitude, 0.75f, "a held stick walks the keeper");

        yield return Lift(1, to);
        Assert.AreEqual(Vector2.zero, GetStatic<Vector2>("PlayerController", "ExternalMove"), "releasing the stick clears ExternalMove");
    }

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator RightDragTurnsTheCamera()
    {
        yield return Prepare();
        Component camera = (Component)Find("CameraFollow");
        Assert.IsNotNull(camera, "CameraFollow");
        float before = Get<float>(camera, "Yaw");
        float w = Screen.width;
        float h = Screen.height;
        Vector2 from = new Vector2(w * 0.65f, h * 0.5f);
        Vector2 to = from + new Vector2(w * 0.2f, 0f);
        yield return Drag(2, from, to, 8, 2);
        yield return Lift(2, to);
        float after = Get<float>(camera, "Yaw");
        Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(before, after)), 2f, "a right-side drag turns the camera");
        Assert.AreEqual(Vector2.zero, GetStatic<Vector2>("CameraFollow", "ExternalLookDelta"), "the camera consumed the delta");
    }

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator ButtonTapDoesNotTurnTheCamera()
    {
        yield return Prepare();
        Component camera = (Component)Find("CameraFollow");
        float before = Get<float>(camera, "Yaw");
        object overlay = Overlay();
        Vector2 sprint = Get<Vector2>(overlay, "SprintButtonScreenPosition");
        // Drag well off the button: the finger keeps its button claim, so the camera must not move.
        Vector2 to = sprint + new Vector2(-Screen.width * 0.15f, 0f);
        yield return Drag(3, sprint, to, 6, 1);
        yield return Lift(3, to);
        float after = Get<float>(camera, "Yaw");
        Assert.Less(Mathf.Abs(Mathf.DeltaAngle(before, after)), 0.01f, "a finger that landed on a button never turns the camera");
    }

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator JumpButtonLaunchesTheKeeper()
    {
        yield return Prepare();
        Component keeper = Keeper();
        float deadline = Time.realtimeSinceStartup + 10f;
        while (!Get<bool>(keeper, "IsGrounded") && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.IsTrue(Get<bool>(keeper, "IsGrounded"), "the keeper is on the ground before the jump");
        yield return Frames(5);
        float startY = keeper.transform.position.y;
        object overlay = Overlay();
        Vector2 jump = Get<Vector2>(overlay, "JumpButtonScreenPosition");
        Touch(1, jump, UnityEngine.InputSystem.TouchPhase.Began);
        float top = startY;
        for (int i = 0; i < 45; i++)
        {
            yield return null;
            top = Mathf.Max(top, keeper.transform.position.y);
            if (i == 1)
            {
                Touch(1, jump, UnityEngine.InputSystem.TouchPhase.Ended);
            }
        }

        Assert.Greater(top - startY, 0.2f, "Jump lifts the keeper off the ground");
        yield return Lift(1, jump);
    }

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator LightButtonLightsABeaconInRange()
    {
        yield return Prepare();
        Component keeper = Keeper();
        Component[] beacons = (Component[])UnityEngine.Object.FindObjectsByType(GameType("Beacon"));
        Assert.Greater(beacons.Length, 0, "beacons on Island1");
        Component beacon = beacons[0];
        CharacterController controller = keeper.GetComponent<CharacterController>();
        Assert.IsNotNull(controller, "CharacterController");
        controller.enabled = false;
        keeper.transform.position = beacon.transform.position + new Vector3(1.2f, 0.2f, 0f);
        controller.enabled = true;
        yield return Frames(6);
        Assert.IsTrue(Get<bool>(beacon, "IsPlayerInRange"), "the keeper stands in range of the beacon");
        Assert.IsFalse(Get<bool>(beacon, "IsLit"));

        object overlay = Overlay();
        Vector2 light = Get<Vector2>(overlay, "LightButtonScreenPosition");
        yield return Tap(1, light);
        float deadline = Time.realtimeSinceStartup + 3f;
        while (!Get<bool>(beacon, "IsLit") && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.IsTrue(Get<bool>(beacon, "IsLit"), "tapping Light lights the beacon in range");
    }

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator KeyboardPressHidesTheOverlay()
    {
        yield return Prepare();
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            addedKeyboard = InputSystem.AddDevice<Keyboard>();
            keyboard = addedKeyboard;
        }

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.K));
        yield return Frames(2);
        Assert.IsFalse(TouchMode.Active, "a key press switches touch mode off");
        yield return WaitOverlay(false);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        yield return Frames(2);

        // A touch brings it back.
        Touch(1, new Vector2(Screen.width * 0.2f, Screen.height * 0.3f), UnityEngine.InputSystem.TouchPhase.Began);
        yield return Frames(2);
        Assert.IsTrue(TouchMode.Active, "a touch switches touch mode back on");
        yield return Lift(1, new Vector2(Screen.width * 0.2f, Screen.height * 0.3f));
        yield return WaitOverlay(true);
    }

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator OverlayStaysHiddenWithoutTouchMode()
    {
        AsyncOperation load = SceneManager.LoadSceneAsync("Island1");
        while (!load.isDone)
        {
            yield return null;
        }

        yield return Frames(30);
        object overlay = Overlay();
        Assert.IsNotNull(overlay, "the bootstrap created the overlay");
        Assert.IsFalse(Get<bool>(overlay, "IsShown"), "no overlay on desktop");
        Assert.AreEqual(0f, Get<float>(overlay, "Alpha"), 1e-4f);
    }
}
}
