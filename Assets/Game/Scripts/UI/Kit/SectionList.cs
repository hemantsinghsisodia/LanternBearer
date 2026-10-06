using System;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// A vertical list of section buttons, each owning one panel. Show(i) activates only panel i and marks button i amber.
[ExecuteAlways]
public class SectionList : MonoBehaviour
{
    [SerializeField] private UITheme theme;
    [SerializeField] private Button[] sectionButtons = new Button[0];
    [SerializeField] private GameObject[] sectionPanels = new GameObject[0];
    [SerializeField] private int current;

    private UnityEngine.Events.UnityAction[] handlers;

    public event Action<int> SectionChanged;

    public Button[] SectionButtons { get { return sectionButtons; } }
    public GameObject[] SectionPanels { get { return sectionPanels; } }
    public int Current { get { return current; } }

    public void Configure(UITheme newTheme, Button[] buttons, GameObject[] panels)
    {
        Unwire();
        theme = newTheme;
        sectionButtons = buttons;
        sectionPanels = panels;
        Wire();
        Show(0);
    }

    public void Show(int index)
    {
        if (sectionPanels == null || sectionPanels.Length == 0)
        {
            return;
        }
        int clamped = Mathf.Clamp(index, 0, sectionPanels.Length - 1);
        bool changed = clamped != current;
        current = clamped;
        for (int i = 0; i < sectionPanels.Length; i++)
        {
            if (sectionPanels[i] != null)
            {
                sectionPanels[i].SetActive(i == current);
            }
            if (sectionButtons != null && i < sectionButtons.Length)
            {
                ThemedButton themed = sectionButtons[i] as ThemedButton;
                if (themed != null)
                {
                    themed.SetMarked(i == current);
                }
            }
        }
        if (changed)
        {
            Action<int> handler = SectionChanged;
            if (handler != null)
            {
                handler(current);
            }
        }
    }

    private void OnEnable()
    {
        Wire();
        Show(current);
    }

    private void OnDisable()
    {
        Unwire();
    }

    private void Wire()
    {
        if (!isActiveAndEnabled || sectionButtons == null)
        {
            return;
        }
        Unwire();
        handlers = new UnityEngine.Events.UnityAction[sectionButtons.Length];
        for (int i = 0; i < sectionButtons.Length; i++)
        {
            int captured = i;
            handlers[i] = () => Show(captured);
            if (sectionButtons[i] != null)
            {
                sectionButtons[i].onClick.AddListener(handlers[i]);
            }
        }
    }

    private void Unwire()
    {
        if (sectionButtons == null || handlers == null)
        {
            return;
        }
        for (int i = 0; i < sectionButtons.Length && i < handlers.Length; i++)
        {
            if (sectionButtons[i] != null)
            {
                sectionButtons[i].onClick.RemoveListener(handlers[i]);
            }
        }
        handlers = null;
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
