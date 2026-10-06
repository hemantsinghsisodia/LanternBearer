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

    private Selectable opener;
    private Action onClosed;

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
        Select(firstSelected);
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
    }

    private void Update()
    {
        if (!BackPressed())
        {
            return;
        }
        // An open dropdown list uses Escape to close itself.
        TMP_Dropdown[] dropdowns = GetComponentsInChildren<TMP_Dropdown>(false);
        for (int i = 0; i < dropdowns.Length; i++)
        {
            if (dropdowns[i].IsExpanded)
            {
                return;
            }
        }
        Close();
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
