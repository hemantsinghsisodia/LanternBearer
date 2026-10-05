using UnityEngine;

namespace LanternKeeper
{
// Per-island palette, fog and grade values for the visual pass. Plain data: nothing reads it at runtime yet.
[CreateAssetMenu(fileName = "LookProfile", menuName = "Lantern Keeper/Look Profile")]
public class LookProfile : ScriptableObject
{
    public string levelId = "";
    public Color sky = Color.black;
    public Color sea = Color.black;
    public Color land = Color.black;
    public Color moonRim = Color.black;
    public Color extra = Color.clear;
    public bool moonOn = true;
    public Color moon = Color.white;
    public Color fogColour = Color.black;
    public float fogDensity = 0.012f;
    public float moonRimStrength = 1f;
    public Color skyHorizon = Color.black;
    public Color skyGround = Color.black;
    public float moonIntensity = 0.35f;
    public float ambientIntensity = 1f;
    public DawnLook dawn;
    public bool mist;
    public bool rain;
    public float gradeSaturation;
    public float gradeContrast;
    public float gradeExposure;

    // D1 environment palette. Color.clear (alpha 0) or null means "derive from the base colours" through LookMapping.
    public Color waterShallow = Color.clear;
    public Color waterDeep = Color.clear;
    public Color foamColour = Color.clear;
    public Color ridgeColour = Color.clear;
    public Color tideBand = Color.clear;
    public Color[] groundTones;

    // Per-island grass hue (authored albedo): a darker root and a lighter, warmer tip. Alpha 0 falls back to the derived land-based tones.
    public Color grassRoot = Color.clear;
    public Color grassTip = Color.clear;

    public bool Validate(out string problem)
    {
        if (string.IsNullOrEmpty(levelId))
        {
            problem = "levelId is empty";
            return false;
        }

        if (fogDensity < 0f || fogDensity > 0.1f)
        {
            problem = "fogDensity " + fogDensity + " is outside [0, 0.1]";
            return false;
        }

        if (moonRimStrength < 0f || moonRimStrength > 2f)
        {
            problem = "moonRimStrength " + moonRimStrength + " is outside [0, 2]";
            return false;
        }

        if (moonIntensity < 0f || moonIntensity > 2f)
        {
            problem = "moonIntensity " + moonIntensity + " is outside [0, 2]";
            return false;
        }

        if (ambientIntensity < 0f || ambientIntensity > 3f)
        {
            problem = "ambientIntensity " + ambientIntensity + " is outside [0, 3]";
            return false;
        }

        if (dawn == null)
        {
            problem = "dawn is not assigned";
            return false;
        }

        if (gradeSaturation < -100f || gradeSaturation > 100f)
        {
            problem = "gradeSaturation " + gradeSaturation + " is outside [-100, 100]";
            return false;
        }

        if (gradeContrast < -100f || gradeContrast > 100f)
        {
            problem = "gradeContrast " + gradeContrast + " is outside [-100, 100]";
            return false;
        }

        if (gradeExposure < -3f || gradeExposure > 3f)
        {
            problem = "gradeExposure " + gradeExposure + " is outside [-3, 3]";
            return false;
        }

        if (moonOn && moon.a <= 0f)
        {
            problem = "moonOn is set but the moon alpha is 0";
            return false;
        }

        problem = null;
        return true;
    }
}
}
