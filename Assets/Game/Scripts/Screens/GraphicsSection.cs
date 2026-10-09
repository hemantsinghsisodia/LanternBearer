using UnityEngine;

namespace LanternKeeper
{
// Settings > Graphics: the preset, render scale, VSync and FPS counter. The FPS counter reads the same switch (GraphicsMenu.FpsEnabled) from the HUD's FpsReadout, which polls it.
public class GraphicsSection : MonoBehaviour
{
    static readonly GraphicsLevel[] Levels =
    {
        GraphicsLevel.Low, GraphicsLevel.Medium, GraphicsLevel.High, GraphicsLevel.Ultra
    };

    [SerializeField] private SwitchRow presetRow;
    [SerializeField] private SwitchRow vsyncRow;
    [SerializeField] private SwitchRow fpsRow;
    [SerializeField] private SwitchRow renderScaleRow;

    public SwitchRow PresetRow { get { return presetRow; } }
    public SwitchRow VsyncRow { get { return vsyncRow; } }
    public SwitchRow FpsRow { get { return fpsRow; } }

    public SwitchRow RenderScaleRow { get { return renderScaleRow; } }

    public void Configure(SwitchRow preset, SwitchRow renderScale, SwitchRow vsync, SwitchRow fps)
    {
        renderScaleRow = renderScale;
        presetRow = preset;
        vsyncRow = vsync;
        fpsRow = fps;
    }

    private void OnEnable()
    {
        presetRow.IndexChanged += OnPreset;
        renderScaleRow.IndexChanged += OnRenderScale;
        vsyncRow.IndexChanged += OnVsync;
        fpsRow.IndexChanged += OnFps;
        GraphicsQuality.QualityChanged += OnQuality;
        Refresh();
    }

    private void OnDisable()
    {
        presetRow.IndexChanged -= OnPreset;
        renderScaleRow.IndexChanged -= OnRenderScale;
        vsyncRow.IndexChanged -= OnVsync;
        fpsRow.IndexChanged -= OnFps;
        GraphicsQuality.QualityChanged -= OnQuality;
    }

    public void Refresh()
    {
        presetRow.SetIndexWithoutNotify(System.Array.IndexOf(Levels, GraphicsQuality.Current));
        renderScaleRow.SetIndexWithoutNotify(SettingsMath.RenderScaleIndex(UserSettings.RenderScale));
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

    private void OnRenderScale(int index)
    {
        UserSettings.RenderScale = SettingsMath.RenderScales[Mathf.Clamp(index, 0, SettingsMath.RenderScales.Length - 1)];
    }

    private void OnVsync(int index)
    {
        GraphicsQuality.VSync = index == 1;
    }

    private void OnFps(int index)
    {
        GraphicsMenu.FpsEnabled = index == 1;
    }
}
}
