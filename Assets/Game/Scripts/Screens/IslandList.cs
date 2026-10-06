using System;
using UnityEngine;
using UnityEngine.UI;

namespace LanternKeeper
{
// The right column of the main menu: a header (Islands label and the difficulty switch) over one row per island.
// The selected row is amber; choosing a row (click or submit) raises Chosen.
public class IslandList : MonoBehaviour
{
    [SerializeField] private UITheme theme;
    [SerializeField] private IslandRow[] rows = new IslandRow[0];
    [SerializeField] private SwitchRow difficultyRow;

    private IslandInfo[] infos = new IslandInfo[0];
    private int selected;

    public event Action<int> Chosen;

    public IslandRow[] Rows { get { return rows; } }
    public SwitchRow DifficultyRow { get { return difficultyRow; } }
    public int SelectedIndex { get { return selected; } }

    public void Configure(UITheme newTheme, IslandRow[] islandRows, SwitchRow difficulty)
    {
        theme = newTheme;
        rows = islandRows;
        difficultyRow = difficulty;
    }

    // Amber roofs: as many as pages found, never more than the beacons; a level with no beacons shows none.
    public static int AmberRoofs(int pagesFound, int beacons)
    {
        return Mathf.Clamp(pagesFound, 0, Mathf.Max(0, beacons));
    }

    // The highest-index unlocked island, or the first when none is unlocked.
    public static int LatestUnlocked(bool[] unlocked)
    {
        for (int i = unlocked.Length - 1; i >= 0; i--)
        {
            if (unlocked[i])
            {
                return i;
            }
        }
        return 0;
    }

    private void OnEnable()
    {
        for (int i = 0; i < rows.Length; i++)
        {
            int index = i;
            rows[i].Button.onClick.RemoveAllListeners();
            rows[i].Button.onClick.AddListener(() => OnRowClicked(index));
        }
    }

    private void OnRowClicked(int index)
    {
        Action<int> handler = Chosen;
        if (handler != null)
        {
            handler(index);
        }
    }

    public void Show(IslandInfo[] islandInfos, int selectedIndex)
    {
        infos = islandInfos;
        selected = selectedIndex;
        Redraw();
    }

    public void SetSelected(int index)
    {
        selected = index;
        Redraw();
    }

    private void Redraw()
    {
        Color amber = theme.amber;
        Color off = Color.Lerp(theme.brassLine, Color.black, 0.45f);
        Color dim = theme.textMuted;
        dim.a = 0.55f;
        for (int i = 0; i < rows.Length; i++)
        {
            bool has = i < infos.Length;
            rows[i].gameObject.SetActive(has);
            if (has)
            {
                rows[i].Show(infos[i], i == selected, amber, off, dim, theme.textMuted);
            }
        }
    }

    // Chains the unlocked rows top to bottom. Up from the first row goes to up (the difficulty switch);
    // Left from any row goes to left (Play).
    public void LinkRows(Selectable up, Selectable left)
    {
        Button previous = null;
        for (int i = 0; i < rows.Length; i++)
        {
            Button button = rows[i].Button;
            if (!button.interactable)
            {
                continue;
            }
            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnUp = previous != null ? previous : up;
            nav.selectOnDown = null;
            nav.selectOnLeft = left;
            nav.selectOnRight = null;
            button.navigation = nav;
            if (previous != null)
            {
                Navigation above = previous.navigation;
                above.selectOnDown = button;
                previous.navigation = above;
            }
            previous = button;
        }
    }

    public Selectable RowSelectable(int index)
    {
        return index >= 0 && index < rows.Length ? rows[index].Button : null;
    }
}
}
