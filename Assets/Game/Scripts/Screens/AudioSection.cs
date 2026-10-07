using UnityEngine;

namespace LanternKeeper
{
// Settings > Audio: master, music, effects and ambience volume, 0-100%.
public class AudioSection : MonoBehaviour
{
    [SerializeField] private SliderRow masterRow;
    [SerializeField] private SliderRow musicRow;
    [SerializeField] private SliderRow effectsRow;
    [SerializeField] private SliderRow ambienceRow;

    public SliderRow MasterRow { get { return masterRow; } }
    public SliderRow MusicRow { get { return musicRow; } }
    public SliderRow EffectsRow { get { return effectsRow; } }
    public SliderRow AmbienceRow { get { return ambienceRow; } }

    public void Configure(SliderRow master, SliderRow music, SliderRow effects, SliderRow ambience)
    {
        masterRow = master;
        musicRow = music;
        effectsRow = effects;
        ambienceRow = ambience;
    }

    private void OnEnable()
    {
        masterRow.ValueChanged += OnMaster;
        musicRow.ValueChanged += OnMusic;
        effectsRow.ValueChanged += OnEffects;
        ambienceRow.ValueChanged += OnAmbience;
        Refresh();
    }

    private void OnDisable()
    {
        masterRow.ValueChanged -= OnMaster;
        musicRow.ValueChanged -= OnMusic;
        effectsRow.ValueChanged -= OnEffects;
        ambienceRow.ValueChanged -= OnAmbience;
    }

    public void Refresh()
    {
        masterRow.SetValueWithoutNotify(UserSettings.MasterVolume);
        musicRow.SetValueWithoutNotify(UserSettings.MusicVolume);
        effectsRow.SetValueWithoutNotify(UserSettings.EffectsVolume);
        ambienceRow.SetValueWithoutNotify(UserSettings.AmbienceVolume);
    }

    private static void OnMaster(float value) { UserSettings.MasterVolume = value; }
    private static void OnMusic(float value) { UserSettings.MusicVolume = value; }
    private static void OnEffects(float value) { UserSettings.EffectsVolume = value; }
    private static void OnAmbience(float value) { UserSettings.AmbienceVolume = value; }
}
}
