using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LanternKeeper
{
// A label plus a TMP_Dropdown, styled by UIBuilder. SetOptions fills the list without raising IndexChanged.
[ExecuteAlways]
public class DropdownRow : MonoBehaviour
{
    [SerializeField] private UITheme theme;
    [SerializeField] private string label;
    [SerializeField] private TMP_Dropdown dropdown;
    [SerializeField] private TMP_Text labelText;

    public event Action<int> IndexChanged;

    public TMP_Dropdown Dropdown { get { return dropdown; } }
    public string Label { get { return label; } }

    public void Configure(UITheme newTheme, string text, TMP_Dropdown d, TMP_Text labelTmp)
    {
        Unwire();
        theme = newTheme;
        label = text;
        dropdown = d;
        labelText = labelTmp;
        if (labelText != null)
        {
            labelText.text = label;
        }
        Wire();
    }

    public void SetOptions(IList<string> options, int index)
    {
        dropdown.ClearOptions();
        List<string> list = new List<string>(options);
        dropdown.AddOptions(list);
        dropdown.SetValueWithoutNotify(Mathf.Clamp(index, 0, Mathf.Max(0, list.Count - 1)));
        dropdown.RefreshShownValue();
    }

    private void OnEnable()
    {
        Wire();
    }

    private void OnDisable()
    {
        Unwire();
    }

    private void Wire()
    {
        if (isActiveAndEnabled && dropdown != null)
        {
            dropdown.onValueChanged.RemoveListener(OnDropdownChanged);
            dropdown.onValueChanged.AddListener(OnDropdownChanged);
        }
    }

    private void Unwire()
    {
        if (dropdown != null)
        {
            dropdown.onValueChanged.RemoveListener(OnDropdownChanged);
        }
    }

    private void OnDropdownChanged(int value)
    {
        Action<int> handler = IndexChanged;
        if (handler != null)
        {
            handler(value);
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
