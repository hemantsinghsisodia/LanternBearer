namespace LanternKeeper
{
// Decides when the keeper has really climbed into a higher ring. Only grounded height counts (a jump apex never raises the ring),
// and the keeper must stay grounded in the new band for ConfirmSeconds. The ring only ever goes up.
public class RingTracker
{
    public const float ConfirmSeconds = 0.5f;

    Ring pending;
    float timer;
    bool hasGround;

    public Ring Reached { get; private set; }
    // Height above sea of the last place the keeper stood on the ground.
    public float LastGroundedHeight { get; private set; }

    public void Reset()
    {
        Reached = Ring.Low;
        pending = Ring.Low;
        timer = 0f;
        hasGround = false;
        LastGroundedHeight = 0f;
    }

    // feetHeight is the keeper's feet above sea level. Returns true on the frame the reached ring rises.
    public bool Step(bool grounded, float feetHeight, float deltaTime)
    {
        if (grounded)
        {
            LastGroundedHeight = feetHeight;
            hasGround = true;
        }

        if (!hasGround)
        {
            return false;
        }

        Ring band = RingLogic.Classify(LastGroundedHeight);
        if (band <= Reached)
        {
            pending = Reached;
            timer = 0f;
            return false;
        }

        // In the air the clock holds: only time spent standing in the new band counts.
        if (!grounded)
        {
            return false;
        }

        if (band != pending)
        {
            pending = band;
            timer = 0f;
        }

        timer += deltaTime;
        if (timer < ConfirmSeconds)
        {
            return false;
        }

        Reached = RingLogic.Ratchet(Reached, band);
        pending = Reached;
        timer = 0f;
        return true;
    }
}
}
