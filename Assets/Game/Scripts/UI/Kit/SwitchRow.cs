using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LanternKeeper
{
// A label with 2 or 3 options. Left and right input cycle (wrapping); click and submit cycle forward.
// Up and down still navigate. Focus shows the theme's focus outline.
public class SwitchRow : Selectable, ISubmitHandler, IPointerClickHandler
{
    [SerializeField] private UITheme theme;
    [SerializeField] private string label;
    [SerializeField] private string[] options = new string[0];
    [SerializeField] private int index;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;
    [SerializeField] private GameObject focusFrame;

    public event Action<int> IndexChanged;

    public string Label { get { return label; } }
    public string[] Options { get { return options; } }
    public TMP_Text ValueText { get { return valueText; } }

    public int Index
    {
        get { return index; }
        set { SetIndex(value, true); }
    }

    public void Configure(UITheme newTheme, string text, string[] choices, int startIndex, TMP_Text labelTmp, TMP_Text valueTmp,
        Button left, Button right, GameObject focus)
    {
        Unwire();
        theme = newTheme;
        label = text;
        options = choices;
        labelText = labelTmp;
        valueText = valueTmp;
        leftButton = left;
        rightButton = right;
        focusFrame = focus;
        transition = Transition.None;
        if (labelText != null)
        {
            labelText.text = label;
        }
        index = Mathf.Clamp(startIndex, 0, Mathf.Max(0, options.Length - 1));
        Refresh();
        ShowFocus(false);
        Wire();
    }

    public void SetIndexWithoutNotify(int value)
    {
        SetIndex(value, false);
    }

    // direction: +1 cycles forward, -1 backward, wrapping at both ends.
    public void Cycle(int direction)
    {
        if (options == null || options.Length == 0)
        {
            return;
        }
        int next = ((index + direction) % options.Length + options.Length) % options.Length;
        SetIndex(next, true);
    }

    public override void OnMove(AxisEventData eventData)
    {
        if (eventData.moveDir == MoveDirection.Left)
        {
            Cycle(-1);
            eventData.Use();
        }
        else if (eventData.moveDir == MoveDirection.Right)
        {
            Cycle(1);
            eventData.Use();
        }
        else
        {
            base.OnMove(eventData);
        }
    }

    public void OnSubmit(BaseEventData eventData)
    {
        Cycle(1);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && IsInteractable())
        {
            Cycle(1);
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        Wire();
    }

    protected override void OnDisable()
    {
        Unwire();
        base.OnDisable();
    }

    private void Wire()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }
        if (leftButton != null)
        {
            leftButton.onClick.RemoveListener(CycleBack);
            leftButton.onClick.AddListener(CycleBack);
        }
        if (rightButton != null)
        {
            rightButton.onClick.RemoveListener(CycleForward);
            rightButton.onClick.AddListener(CycleForward);
        }
    }

    private void Unwire()
    {
        if (leftButton != null)
        {
            leftButton.onClick.RemoveListener(CycleBack);
        }
        if (rightButton != null)
        {
            rightButton.onClick.RemoveListener(CycleForward);
        }
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        base.DoStateTransition(state, instant);
        ShowFocus(state == SelectionState.Selected || state == SelectionState.Highlighted || state == SelectionState.Pressed);
    }

    private void CycleBack()
    {
        Cycle(-1);
    }

    private void CycleForward()
    {
        Cycle(1);
    }

    private void SetIndex(int value, bool notify)
    {
        int count = options == null ? 0 : options.Length;
        int clamped = count == 0 ? 0 : Mathf.Clamp(value, 0, count - 1);
        bool changed = clamped != index;
        index = clamped;
        Refresh();
        if (notify && changed)
        {
            Action<int> handler = IndexChanged;
            if (handler != null)
            {
                handler(index);
            }
        }
    }

    private void Refresh()
    {
        if (valueText != null && options != null && options.Length > 0)
        {
            valueText.text = options[Mathf.Clamp(index, 0, options.Length - 1)];
        }
    }

    private void ShowFocus(bool on)
    {
        if (focusFrame != null && focusFrame.activeSelf != on)
        {
            focusFrame.SetActive(on);
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        if (theme == null)
        {
            theme = UnityEditor.AssetDatabase.LoadAssetAtPath<UITheme>("Assets/Game/Art/UI/UITheme.asset");
        }
    }
#endif
}
}
