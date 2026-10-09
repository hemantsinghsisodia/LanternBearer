using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace LanternKeeper
{
// The Settings screen: a side list of five sections on the left, the selected section's rows on the right under a
// one-line flavour. Changes apply at once and are saved by UserSettings; there is no Apply button.
// Open() shows it and selects the first section; Back (button, Esc or gamepad B) closes it and reselects the opener.
public class SettingsScreen : MonoBehaviour
{
    [SerializeField] private SectionList sectionList;
    [SerializeField] private Button backButton;
    [SerializeField] private Button firstSelected;
    [SerializeField] private ThemedLabel flavourLabel;
    [SerializeField] private string[] flavours = new string[0];
    [SerializeField] private DisplaySection display;
    [SerializeField] private GraphicsSection graphics;
    [SerializeField] private AudioSection audio;
    [SerializeField] private AccessibilitySection accessibility;
    [SerializeField] private ControlsSection controls;
    [SerializeField] private ThemedLabel hintLabel;

    private Selectable opener;
    private Action onClosed;
    private bool dropdownWasExpanded;
    private TMP_Dropdown[] dropdowns;
    private SettingsHintRow hovered;
    private bool preferHover;
    private GameObject lastSelected;
    private string shownHint;

    public bool IsOpen { get { return gameObject.activeInHierarchy; } }
    public SectionList SectionList { get { return sectionList; } }
    public Button BackButton { get { return backButton; } }
    public Button FirstSelected { get { return firstSelected; } }
    public ThemedLabel FlavourLabel { get { return flavourLabel; } }
    public string[] Flavours { get { return flavours; } }
    public DisplaySection Display { get { return display; } }
    public GraphicsSection Graphics { get { return graphics; } }
    public AudioSection Audio { get { return audio; } }
    public AccessibilitySection Accessibility { get { return accessibility; } }
    public ControlsSection Controls { get { return controls; } }
    public ThemedLabel HintLabel { get { return hintLabel; } }
    // The text the hint line is showing now.
    public string CurrentHint { get { return shownHint; } }

    // The hint line at the bottom: one sentence about the row that has focus or is under the mouse.
    public void ConfigureHint(ThemedLabel label)
    {
        hintLabel = label;
    }

    public void Configure(SectionList list, Button back, ThemedLabel flavour, string[] flavourLines,
        DisplaySection displaySection, GraphicsSection graphicsSection, AudioSection audioSection,
        AccessibilitySection accessibilitySection, ControlsSection controlsSection)
    {
        sectionList = list;
        backButton = back;
        flavourLabel = flavour;
        flavours = flavourLines;
        display = displaySection;
        graphics = graphicsSection;
        audio = audioSection;
        accessibility = accessibilitySection;
        controls = controlsSection;
        firstSelected = list != null && list.SectionButtons.Length > 0 ? list.SectionButtons[0] : null;
    }

    public void Open(Selectable openedBy, Action closedCallback)
    {
        opener = openedBy;
        onClosed = closedCallback;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (sectionList != null)
        {
            sectionList.Show(0);
        }
        ShowFlavour(0);
        LinkNavigation();
        hovered = null;
        preferHover = false;
        Select(firstSelected);
        lastSelected = CurrentSelection();
        RefreshHint();
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }
        UserSettings.Flush();
        gameObject.SetActive(false);
        Selectable back = opener;
        Action callback = onClosed;
        opener = null;
        onClosed = null;
        if (back != null && back.gameObject.activeInHierarchy)
        {
            Select(back);
        }
        else if (EventSystem.current != null)
        {
            // Nothing to hand focus to: do not leave it on the screen we just hid.
            EventSystem.current.SetSelectedGameObject(null);
        }
        if (callback != null)
        {
            callback();
        }
    }

    private void OnEnable()
    {
        if (sectionList != null)
        {
            sectionList.SectionChanged += OnSectionChanged;
        }
        if (backButton != null)
        {
            backButton.onClick.AddListener(Close);
        }
        SettingsHintRow.HoverChanged += OnHover;
        if (graphics != null)
        {
            // Preset and Render scale hints change with the value.
            graphics.PresetRow.IndexChanged += OnHintValueChanged;
            graphics.RenderScaleRow.IndexChanged += OnHintValueChanged;
        }
        RefreshHint();
    }

    private void OnDisable()
    {
        if (sectionList != null)
        {
            sectionList.SectionChanged -= OnSectionChanged;
        }
        if (backButton != null)
        {
            backButton.onClick.RemoveListener(Close);
        }
        SettingsHintRow.HoverChanged -= OnHover;
        if (graphics != null)
        {
            graphics.PresetRow.IndexChanged -= OnHintValueChanged;
            graphics.RenderScaleRow.IndexChanged -= OnHintValueChanged;
        }
        hovered = null;
        preferHover = false;
    }

    private void Update()
    {
        // The dropdown may already have closed itself on this same Escape; trust last frame's state.
        if (BackPressed() && !dropdownWasExpanded)
        {
            ConsumeBack();
        }
        dropdownWasExpanded = AnyDropdownExpanded();

        // The hint follows focus: only recomputed when the selected object changes.
        GameObject selected = CurrentSelection();
        if (selected != lastSelected)
        {
            lastSelected = selected;
            preferHover = false;
            RefreshHint();
        }
    }

    private static GameObject CurrentSelection()
    {
        EventSystem system = EventSystem.current;
        return system != null ? system.currentSelectedGameObject : null;
    }

    private void OnHover(SettingsHintRow row, bool entered)
    {
        if (row == null || !row.transform.IsChildOf(transform))
        {
            return;
        }
        if (entered)
        {
            hovered = row;
            preferHover = true;
        }
        else if (hovered == row)
        {
            hovered = null;
            preferHover = false;
        }
        RefreshHint();
    }

    private void OnHintValueChanged(int index)
    {
        RefreshHint();
    }

    // Picks the row (hovered, else the row that holds focus) and shows its hint. Writes the label only when the text differs.
    public void RefreshHint()
    {
        if (hintLabel == null)
        {
            return;
        }
        SettingsHintRow row = null;
        if (preferHover && hovered != null)
        {
            row = hovered;
        }
        if (row == null && lastSelected != null)
        {
            row = lastSelected.GetComponentInParent<SettingsHintRow>();
            if (row != null && !row.transform.IsChildOf(transform))
            {
                row = null;
            }
        }
        if (row == null)
        {
            row = hovered;
        }

        string text;
        if (row != null)
        {
            int value = 0;
            if (SettingsHints.IsValueDependent(row.HintId))
            {
                SwitchRow switchRow = row.GetComponent<SwitchRow>();
                value = switchRow != null ? switchRow.Index : 0;
            }
            text = SettingsHints.Get(row.HintId, value);
        }
        else if (sectionList != null && controls != null && sectionList.Current >= 0
            && sectionList.Current < sectionList.SectionPanels.Length
            && sectionList.SectionPanels[sectionList.Current] == controls.gameObject)
        {
            text = SettingsHints.ControlsTab;
        }
        else
        {
            text = SettingsHints.Neutral;
        }

        if (shownHint != text)
        {
            shownHint = text;
            hintLabel.Text.text = text;
        }
    }

    private bool AnyDropdownExpanded()
    {
        // Cached: GetComponentsInChildren allocates, and this runs every frame. Includes inactive ones; they are never expanded.
        if (dropdowns == null)
        {
            dropdowns = GetComponentsInChildren<TMP_Dropdown>(true);
        }
        for (int i = 0; i < dropdowns.Length; i++)
        {
            if (dropdowns[i] != null && dropdowns[i].IsExpanded)
            {
                return true;
            }
        }
        return false;
    }

    // Back (Esc or gamepad B), one level at a time. An open dropdown closes itself first (returns false, nothing for us to do).
    // Focus inside a section returns to that section's tab; focus on the tab list or Back closes Settings.
    // Returns true if it moved focus or closed the screen.
    public bool ConsumeBack()
    {
        if (!IsOpen)
        {
            return false;
        }
        if (AnyDropdownExpanded())
        {
            return false;
        }
        if (FocusIsInsideSection())
        {
            Select(sectionList.SectionButtons[sectionList.Current]);
            return true;
        }
        Close();
        return true;
    }

    private bool FocusIsInsideSection()
    {
        EventSystem system = EventSystem.current;
        if (system == null || system.currentSelectedGameObject == null || sectionList == null)
        {
            return false;
        }
        GameObject[] panels = sectionList.SectionPanels;
        int current = sectionList.Current;
        return current >= 0 && current < panels.Length && panels[current] != null
            && system.currentSelectedGameObject.transform.IsChildOf(panels[current].transform);
    }

    private static bool BackPressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            return true;
        }
        Gamepad pad = Gamepad.current;
        return pad != null && pad.buttonEast.wasPressedThisFrame;
    }

    private void OnSectionChanged(int index)
    {
        ShowFlavour(index);
        LinkNavigation();
        RefreshHint();
    }

    private void ShowFlavour(int index)
    {
        if (flavourLabel != null && flavours != null && index >= 0 && index < flavours.Length)
        {
            flavourLabel.Text.text = flavours[index];
        }
    }

    // Section buttons chain up and down, the last one drops to Back, and Right enters the section's first row.
    private void LinkNavigation()
    {
        if (sectionList == null)
        {
            return;
        }
        Button[] buttons = sectionList.SectionButtons;
        Selectable firstRow = FirstRow(sectionList.Current);
        for (int i = 0; i < buttons.Length; i++)
        {
            Navigation nav = buttons[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = i > 0 ? buttons[i - 1] : null;
            nav.selectOnDown = i < buttons.Length - 1 ? buttons[i + 1] : backButton;
            nav.selectOnLeft = null;
            nav.selectOnRight = firstRow;
            buttons[i].navigation = nav;
        }
        if (backButton != null && buttons.Length > 0)
        {
            Navigation nav = backButton.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = buttons[buttons.Length - 1];
            nav.selectOnDown = null;
            nav.selectOnLeft = null;
            nav.selectOnRight = null;
            backButton.navigation = nav;
        }
    }

    private Selectable FirstRow(int sectionIndex)
    {
        GameObject[] panels = sectionList.SectionPanels;
        if (sectionIndex < 0 || sectionIndex >= panels.Length || panels[sectionIndex] == null)
        {
            return null;
        }
        Selectable[] selectables = panels[sectionIndex].GetComponentsInChildren<Selectable>(true);
        for (int i = 0; i < selectables.Length; i++)
        {
            // Skip the switch rows' own arrow buttons; the row itself is the target.
            if (selectables[i].navigation.mode != Navigation.Mode.None)
            {
                return selectables[i];
            }
        }
        return null;
    }

    private static void Select(Selectable target)
    {
        EventSystem system = EventSystem.current;
        if (target == null || system == null || !target.gameObject.activeInHierarchy)
        {
            return;
        }
        system.SetSelectedGameObject(null);
        system.SetSelectedGameObject(target.gameObject);
    }
}
}
