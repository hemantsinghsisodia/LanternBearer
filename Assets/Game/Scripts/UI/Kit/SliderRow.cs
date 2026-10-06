using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// A label plus a 0-1 slider. ValueChanged fires live while dragging so effects apply immediately.
//
// Persistence is deferred: while ValueChanged handlers run, UserSettings.DeferSave is true, so setters called
// from the handlers write PlayerPrefs and raise UserSettings.Changed but do not call PlayerPrefs.Save().
// UserSettings.Flush() then runs on pointer-up, submit or deselect (via SliderCommitRelay on the slider), or after
// 0.3 s of no change (unscaled time, measured from the one shared UserSettings.LastDeferredChangeTime), whichever comes first. This avoids a disk write on every drag tick.
[ExecuteAlways]
public class SliderRow : MonoBehaviour
{
    public const float IdleSaveSeconds = 0.3f;

    [SerializeField] private UITheme theme;
    [SerializeField] private string label;
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text valueText;

    private Func<float, string> formatter;

    public event Action<float> ValueChanged;

    public Slider Slider { get { return slider; } }
    public string Label { get { return label; } }

    public void Configure(UITheme newTheme, string text, Slider s, TMP_Text labelTmp, TMP_Text valueTmp)
    {
        Unwire();
        theme = newTheme;
        label = text;
        slider = s;
        labelText = labelTmp;
        valueText = valueTmp;
        if (labelText != null)
        {
            labelText.text = label;
        }
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        RefreshValueText(slider.value);
        Wire();
    }

    // Optional display format for the value text (default is a percentage).
    public void SetValueFormatter(Func<float, string> format)
    {
        formatter = format;
        if (slider != null)
        {
            RefreshValueText(slider.value);
        }
    }

    public void SetValueWithoutNotify(float value)
    {
        slider.SetValueWithoutNotify(value);
        RefreshValueText(slider.value);
    }

    // Persists any deferred settings now.
    public void Commit()
    {
        UserSettings.Flush();
    }

    private void OnEnable()
    {
        Wire();
    }

    private void OnDisable()
    {
        Unwire();
        Commit();
    }

    private void Wire()
    {
        if (isActiveAndEnabled && slider != null)
        {
            slider.onValueChanged.RemoveListener(OnSliderChanged);
            slider.onValueChanged.AddListener(OnSliderChanged);
        }
    }

    private void Unwire()
    {
        if (slider != null)
        {
            slider.onValueChanged.RemoveListener(OnSliderChanged);
        }
    }

    private void Update()
    {
        UserSettings.FlushIfIdle(IdleSaveSeconds);
    }

    private void OnSliderChanged(float value)
    {
        RefreshValueText(value);
        bool previous = UserSettings.DeferSave;
        UserSettings.DeferSave = true;
        try
        {
            Action<float> handler = ValueChanged;
            if (handler != null)
            {
                handler(value);
            }
        }
        finally
        {
            UserSettings.DeferSave = previous;
        }
    }

    private void RefreshValueText(float value)
    {
        if (valueText != null)
        {
            valueText.text = formatter != null ? formatter(value) : Mathf.RoundToInt(value * 100f) + "%";
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
