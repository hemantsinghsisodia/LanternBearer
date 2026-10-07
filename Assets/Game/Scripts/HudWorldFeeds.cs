using UnityEngine;
using UnityEngine.InputSystem;

namespace LanternKeeper
{
// Drives the intro card: it shows on first visit (the game holds itself paused) and again from the pause menu's How to Play button.
// A finished round wins: the card goes and the result panel takes the screen.
sealed class IntroFeed
{
    string shownKey;

    public void Update(IntroCard card, PauseScreen pause, GameManager manager)
    {
        if (card == null)
        {
            return;
        }

        bool howToOpen = pause != null && pause.HowToOpen;
        if ((!manager.IntroShowing && !howToOpen) || manager.IsRoundOver)
        {
            card.Hide();
            shownKey = null;
            return;
        }

        string key = manager.LevelId + (manager.IntroShowing ? "|intro" : "|help");
        if (!card.IsShowing || key != shownKey)
        {
            shownKey = key;
            card.Show(manager.IslandLabelHud, manager.IntroLines, manager.IntroShowing ? "Press any key to begin" : "Press any key to go back");
        }

        if (howToOpen && !manager.IntroShowing && Time.unscaledTime >= pause.HowToUnlockTime && DismissInputPressed())
        {
            card.Hide();
            pause.CloseHowTo();
        }
    }

    // Escape and P are left to GameManager, which routes them through ConsumePauseBack. Keep in step with
    // GameManager.AnyInputPressedThisFrame (first-visit card, whose release is deferred to LateUpdate); this one only
    // closes How to Play, which leaves the game paused, so no deferral is needed.
    static bool DismissInputPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.anyKey.wasPressedThisFrame && !keyboard.escapeKey.wasPressedThisFrame && !keyboard.pKey.wasPressedThisFrame)
        {
            return true;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
        {
            return true;
        }

        Gamepad pad = Gamepad.current;
        return pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame);
    }
}

// Reads Island 3's tide into the tide chip and a "Tide turning" toast. Islands without a Tide never show the chip.
sealed class TideFeed
{
    Tide tide;
    bool searched;
    bool wasRising;
    bool warned;

    public void Update(TideChip chip, Toasts toasts)
    {
        if (tide == null)
        {
            if (searched)
            {
                return;
            }

            searched = true;
            tide = Tide.Instance != null ? Tide.Instance : Object.FindAnyObjectByType<Tide>();
            if (tide == null)
            {
                return;
            }

            wasRising = tide.IsRising;
        }

        bool rising = tide.IsRising;
        if (chip != null)
        {
            chip.Show(true, rising, tide.Normalized);
        }

        if (rising != wasRising)
        {
            wasRising = rising;
            warned = false;
        }

        GameManager manager = GameManager.Instance;
        bool roundOver = manager != null && (manager.IsRoundOver || manager.Won);
        if (!warned && !roundOver && Mathf.CeilToInt(tide.SecondsToTurn) <= 5)
        {
            warned = true;
            if (toasts != null)
            {
                toasts.Show("Tide turning", ToastKind.Tide);
            }
        }
    }
}

// Reads Island 4's wind and thunder into the storm chip. Islands without wind or lightning never show it or pulse on a Shade steal.
sealed class StormFeed
{
    Transform cameraTransform;
    bool searched;
    bool present;

    public void Update(StormChip chip, GameObject host)
    {
        if (!searched)
        {
            searched = true;
            present = Wind.Instance != null || Lightning.Instance != null;
            if (present && host.GetComponent<StealPulse>() == null)
            {
                host.AddComponent<StealPulse>();
            }
        }

        if (!present || chip == null)
        {
            return;
        }

        float angle = 0f;
        float strength = 0f;
        Wind wind = Wind.Instance;
        if (wind != null)
        {
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            forward.y = 0f;
            Vector3 direction = wind.Direction;
            direction.y = 0f;
            if (forward.sqrMagnitude > 0.0001f && direction.sqrMagnitude > 0.0001f)
            {
                angle = Vector3.SignedAngle(forward, direction, Vector3.up);
            }

            strength = wind.Phase == WindPhase.Gust ? wind.Strength01 : 0f;
        }

        float thunder = 0f;
        Lightning lightning = Lightning.Instance;
        if (lightning != null && lightning.Phase == LightningPhase.Thunder)
        {
            thunder = UserSettings.ReduceFlashing ? 0.8f : 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 14f));
        }

        chip.Show(true, angle, strength, thunder);
    }
}
}
