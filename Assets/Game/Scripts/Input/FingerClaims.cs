using System.Collections.Generic;
using UnityEngine;

namespace LanternKeeper
{
/// <summary>
/// Which control owns each finger. A finger is claimed once, by where it went down, and keeps that claim until it lifts:
/// a stick finger that drifts into the right half never turns the camera, and a finger that tapped a button never does
/// either. Pure logic, no Unity UI, so EditMode tests can drive it.
/// </summary>
public sealed class FingerClaims
{
    public enum Region
    {
        None,
        Stick,
        Look,
        Button,
        Ignored
    }

    /// <summary>Degrees of camera turn per screen pixel at the reference DPI.</summary>
    public const float LookDegreesPerPixel = 0.15f;
    public const float ReferenceDpi = 160f;

    struct Claim
    {
        public Region Region;
        public Vector2 Last;
    }

    readonly Dictionary<int, Claim> claims = new Dictionary<int, Claim>();
    readonly List<int> scratch = new List<int>();
    int stickFinger = int.MinValue;

    public int Count => claims.Count;

    /// <summary>Region for a new finger: a button if it landed on one, else the left half is the stick and the right half the look pad.</summary>
    public static Region Classify(Vector2 screenPosition, float screenWidth, bool overButton)
    {
        if (overButton)
        {
            return Region.Button;
        }

        return screenPosition.x < screenWidth * 0.5f ? Region.Stick : Region.Look;
    }

    /// <summary>Claims a new finger. A second finger landing on the stick side while one already drives the stick is ignored.</summary>
    public Region Begin(int fingerId, Vector2 screenPosition, float screenWidth, bool overButton)
    {
        Claim existing;
        if (claims.TryGetValue(fingerId, out existing))
        {
            return existing.Region;
        }

        Region region = Classify(screenPosition, screenWidth, overButton);
        if (region == Region.Stick)
        {
            if (stickFinger != int.MinValue)
            {
                region = Region.Ignored;
            }
            else
            {
                stickFinger = fingerId;
            }
        }

        claims[fingerId] = new Claim { Region = region, Last = screenPosition };
        return region;
    }

    /// <summary>Marks a finger that was already down before the controls appeared: it drives nothing until it lifts.</summary>
    public void Ignore(int fingerId, Vector2 screenPosition)
    {
        if (!claims.ContainsKey(fingerId))
        {
            claims[fingerId] = new Claim { Region = Region.Ignored, Last = screenPosition };
        }
    }

    public Region RegionOf(int fingerId)
    {
        Claim claim;
        return claims.TryGetValue(fingerId, out claim) ? claim.Region : Region.None;
    }

    /// <summary>Movement since the finger's previous position (screen pixels). Zero for an unknown finger.</summary>
    public Vector2 Move(int fingerId, Vector2 screenPosition)
    {
        Claim claim;
        if (!claims.TryGetValue(fingerId, out claim))
        {
            return Vector2.zero;
        }

        Vector2 delta = screenPosition - claim.Last;
        claim.Last = screenPosition;
        claims[fingerId] = claim;
        return delta;
    }

    public void End(int fingerId)
    {
        if (claims.Remove(fingerId) && fingerId == stickFinger)
        {
            stickFinger = int.MinValue;
        }
    }

    /// <summary>Releases every finger whose id is not in the live list (lost end events, or the overlay was hidden). Returns the released ids.</summary>
    public void ReleaseExcept(IList<int> liveFingers, List<int> released)
    {
        scratch.Clear();
        foreach (KeyValuePair<int, Claim> pair in claims)
        {
            if (!liveFingers.Contains(pair.Key))
            {
                scratch.Add(pair.Key);
            }
        }

        for (int i = 0; i < scratch.Count; i++)
        {
            if (released != null)
            {
                released.Add(scratch[i]);
            }

            End(scratch[i]);
        }
    }

    public void Clear()
    {
        claims.Clear();
        stickFinger = int.MinValue;
    }

    /// <summary>Only a finger that went down on the look pad turns the camera.</summary>
    public static bool TurnsCamera(Region region)
    {
        return region == Region.Look;
    }

    /// <summary>Screen-pixel drag to degrees. DPI-scaled so a swipe of the same physical length turns the same angle on any phone.</summary>
    public static Vector2 LookDegrees(Vector2 pixelDelta, float dpi)
    {
        float scale = dpi > 1f ? Mathf.Clamp(ReferenceDpi / dpi, 0.4f, 1.6f) : 1f;
        return pixelDelta * (LookDegreesPerPixel * scale);
    }
}
}
