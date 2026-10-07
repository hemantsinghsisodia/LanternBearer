using UnityEngine;

namespace LanternKeeper
{
// Settings > Graphics: the preset, VSync and FPS counter. It drives the same GraphicsQuality / GraphicsMenu logic
// the old graphics screen used, which is left unchanged; only the presentation (kit rows) is new.
public class GraphicsSection : MonoBehaviour
{
    static readonly GraphicsLevel[] Levels =
    {
        GraphicsLevel.Low, GraphicsLevel.Medium, GraphicsLevel.High, GraphicsLevel.Ultra
    };

    [SerializeField] private SwitchRow presetRow;
    [SerializeField] private SwitchRow vsyncRow;
    [SerializeField] private SwitchRow fpsRow;

    public SwitchRow PresetRow { get { return presetRow; } }
    public SwitchRow VsyncRow { get { return vsyncRow; } }
    public SwitchRow FpsRow { get { return fpsRow; } }

    public void Configure(SwitchRow preset, SwitchRow vsync, SwitchRow fps)
    {
        presetRow = preset;
        vsyncRow = vsync;
        fpsRow = fps;
    }

    private void OnEnable()
    {
        presetRow.IndexChanged += OnPreset;
        vsyncRow.IndexChanged += OnVsync;
        fpsRow.IndexChanged += OnFps;
        GraphicsQuality.QualityChanged += OnQuality;
        Refresh();
    }

    private void OnDisable()
    {
        presetRow.IndexChanged -= OnPreset;
        vsyncRow.IndexChanged -= OnVsync;
        fpsRow.IndexChanged -= OnFps;
        GraphicsQuality.QualityChanged -= OnQuality;
    }

    public void Refresh()
    {
        presetRow.SetIndexWithoutNotify(System.Array.IndexOf(Levels, GraphicsQuality.Current));
        vsyncRow.SetIndexWithoutNotify(GraphicsQuality.VSync ? 1 : 0);
        fpsRow.SetIndexWithoutNotify(GraphicsMenu.FpsEnabled ? 1 : 0);
    }

    private void OnQuality(GraphicsProfile profile)
    {
        Refresh();
    }

    private void OnPreset(int index)
    {
        GraphicsQuality.Set(Levels[Mathf.Clamp(index, 0, Levels.Length - 1)]);
    }

    private void OnVsync(int index)
    {
        GraphicsQuality.VSync = index == 1;
    }

    private void OnFps(int index)
    {
        GraphicsMenu.FpsEnabled = index == 1;
        GraphicsMenu menu = FindAnyObjectByType<GraphicsMenu>(FindObjectsInactive.Include);
        if (menu != null)
        {
            menu.SyncFpsReadout();
        }
    }
}
}
