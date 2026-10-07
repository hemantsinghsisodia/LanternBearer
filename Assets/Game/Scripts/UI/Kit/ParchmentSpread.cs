using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// Two-page parchment spread for the Keeper's Log. Left page: title, subtitle, body. Right page: body.
[ExecuteAlways]
public class ParchmentSpread : MonoBehaviour
{
    [SerializeField] private UITheme theme;
    [SerializeField] private TMP_Text leftTitle;
    [SerializeField] private TMP_Text leftSub;
    [SerializeField] private TMP_Text leftBody;
    [SerializeField] private TMP_Text rightBody;
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private Image ribbon;
    [SerializeField] private CanvasGroup pages;

    // Frame time for TurnFade; tests replace it to step the fade deterministically.
    public static Func<float> DeltaSource = () => Time.unscaledDeltaTime;
    public const float MaxFrameSeconds = 0.1f;

    public event Action Prev;
    public event Action Next;

    public TMP_Text LeftTitle { get { return leftTitle; } }
    public TMP_Text LeftSub { get { return leftSub; } }
    public TMP_Text LeftBody { get { return leftBody; } }
    public TMP_Text RightBody { get { return rightBody; } }
    public Button PrevButton { get { return prevButton; } }
    public Button NextButton { get { return nextButton; } }
    public Image Ribbon { get { return ribbon; } }
    public CanvasGroup Pages { get { return pages; } }

    public void Configure(UITheme newTheme, TMP_Text title, TMP_Text sub, TMP_Text left, TMP_Text right,
        Button prev, Button next, Image ribbonImage, CanvasGroup pagesGroup)
    {
        Unwire();
        theme = newTheme;
        leftTitle = title;
        leftSub = sub;
        leftBody = left;
        rightBody = right;
        prevButton = prev;
        nextButton = next;
        ribbon = ribbonImage;
        pages = pagesGroup;
        if (ribbon != null)
        {
            ribbon.color = theme.ribbon;
        }
        Wire();
    }

    public void SetSpread(string title, string sub, string left, string right)
    {
        leftTitle.text = title;
        leftSub.text = sub;
        leftBody.text = left;
        rightBody.text = right;
    }

    // Fades the pages in from transparent. Uses unscaled time so it works while paused.
    public IEnumerator TurnFade(float seconds = 0.2f)
    {
        if (pages == null)
        {
            yield break;
        }
        float t = 0f;
        pages.alpha = 0f;
        yield return null; // one transparent frame, so the first delta (which can be huge in the editor) is not counted
        while (t < seconds)
        {
            t += Mathf.Min(DeltaSource(), MaxFrameSeconds); // a hitch (editor, scene load) never skips the fade
            pages.alpha = Mathf.Clamp01(t / seconds);
            yield return null;
        }
        pages.alpha = 1f;
    }

    private void OnEnable()
    {
        Wire();
    }

    private void OnDisable()
    {
        Unwire();
        if (pages != null)
        {
            pages.alpha = 1f;
        }
    }

    private void Wire()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }
        Unwire();
        if (prevButton != null)
        {
            prevButton.onClick.AddListener(RaisePrev);
        }
        if (nextButton != null)
        {
            nextButton.onClick.AddListener(RaiseNext);
        }
    }

    private void Unwire()
    {
        if (prevButton != null)
        {
            prevButton.onClick.RemoveListener(RaisePrev);
        }
        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(RaiseNext);
        }
    }

    private void RaisePrev()
    {
        Action handler = Prev;
        if (handler != null)
        {
            handler();
        }
    }

    private void RaiseNext()
    {
        Action handler = Next;
        if (handler != null)
        {
            handler();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (theme == null)
        {
            theme = UnityEditor.AssetDatabase.LoadAssetAtPath<UITheme>("Assets/Game/Art/UI/UITheme.asset");
        }
    }
#endif
}
}
