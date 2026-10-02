using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class WindTests
{
    private const float Step = 0.01f;

    [Test]
    public void PhasesCycleInOrder()
    {
        WindCycle wind = new WindCycle(1, 0f, 1f, false);
        Assert.AreEqual(WindPhase.Calm, wind.Phase);

        WindPhase expected = WindPhase.Calm;
        int transitions = 0;
        for (int i = 0; i < 20000 && transitions < 6; i++)
        {
            wind.Tick(Step);
            if (wind.Phase != expected)
            {
                WindPhase next = expected == WindPhase.Calm ? WindPhase.Warning
                    : expected == WindPhase.Warning ? WindPhase.Gust : WindPhase.Calm;
                Assert.AreEqual(next, wind.Phase);
                expected = next;
                transitions++;
            }
        }

        Assert.AreEqual(6, transitions);
    }

    [Test]
    public void DurationsInRange()
    {
        for (int hardIndex = 0; hardIndex < 2; hardIndex++)
        {
            bool hard = hardIndex == 1;
            float calmMin = hard ? 10f : 12f;
            float calmMax = hard ? 16f : 20f;
            WindCycle wind = new WindCycle(7, 0f, 1f, hard);
            WindPhase last = wind.Phase;
            float elapsed = 0f;
            int cycles = 0;
            for (int i = 0; i < 100000 && cycles < 8; i++)
            {
                wind.Tick(Step);
                elapsed += Step;
                if (wind.Phase == last)
                {
                    continue;
                }

                if (last == WindPhase.Calm)
                {
                    Assert.GreaterOrEqual(elapsed, calmMin - 0.02f);
                    Assert.LessOrEqual(elapsed, calmMax + 0.02f);
                }
                else if (last == WindPhase.Warning)
                {
                    Assert.AreEqual(2f, elapsed, 0.02f);
                }
                else
                {
                    Assert.GreaterOrEqual(elapsed, 3f - 0.02f);
                    Assert.LessOrEqual(elapsed, 4f + 0.02f);
                    cycles++;
                }

                elapsed = 0f;
                last = wind.Phase;
            }

            Assert.AreEqual(8, cycles);
        }
    }

    [Test]
    public void DirectionConstantDuringWarningAndGust()
    {
        WindCycle wind = new WindCycle(3, 90f, 1f, false);
        for (int cycle = 0; cycle < 4; cycle++)
        {
            while (wind.Phase != WindPhase.Warning)
            {
                wind.Tick(Step);
            }

            Vector3 dir = wind.Direction;
            while (wind.Phase != WindPhase.Calm)
            {
                Assert.AreEqual(dir, wind.Direction);
                wind.Tick(Step);
            }
        }
    }

    [Test]
    public void DirectionWithin60OfPrevailing()
    {
        float prevailing = 135f;
        Vector3 prevDir = new Vector3(Mathf.Sin(prevailing * Mathf.Deg2Rad), 0f, Mathf.Cos(prevailing * Mathf.Deg2Rad));
        for (int seed = 0; seed < 30; seed++)
        {
            WindCycle wind = new WindCycle(seed, prevailing, 1f, false);
            for (int cycle = 0; cycle < 5; cycle++)
            {
                while (wind.Phase != WindPhase.Warning)
                {
                    wind.Tick(0.05f);
                }

                Assert.AreEqual(1f, wind.Direction.magnitude, 0.001f);
                Assert.AreEqual(0f, wind.Direction.y, 0.0001f);
                Assert.LessOrEqual(Vector3.Angle(prevDir, wind.Direction), 60.01f);
                while (wind.Phase != WindPhase.Calm)
                {
                    wind.Tick(0.05f);
                }
            }
        }
    }

    [Test]
    public void StrengthRamps()
    {
        WindCycle wind = new WindCycle(5, 0f, 1f, false);
        Assert.AreEqual(0f, wind.Strength01, 0.0001f);
        while (wind.Phase != WindPhase.Gust)
        {
            Assert.AreEqual(0f, wind.Strength01, 0.0001f);
            wind.Tick(Step);
        }

        // Phase just flipped; gust time is within one step of zero.
        wind.Tick(0.25f);
        Assert.AreEqual(0.5f, wind.Strength01, 0.05f);
        wind.Tick(0.5f);
        Assert.AreEqual(1f, wind.Strength01, 0.0001f);
        while (wind.Phase == WindPhase.Gust)
        {
            wind.Tick(Step);
        }

        Assert.AreEqual(0f, wind.Strength01, 0.0001f);
    }

    [Test]
    public void PeakPushBelowWalkSpeedOnAllDifficulties()
    {
        for (int difficulty = 0; difficulty <= 2; difficulty++)
        {
            float scale = StormTuning.GustMultiplier(difficulty);
            Vector3 push = WindCycle.Push(Vector3.forward, 1f, scale, false, false);
            Assert.Less(push.magnitude, StormTuning.WalkSpeed);
        }
    }

    [Test]
    public void AirborneAndShelterScale()
    {
        Vector3 full = WindCycle.Push(Vector3.right, 1f, 1f, false, false);
        Vector3 air = WindCycle.Push(Vector3.right, 1f, 1f, true, false);
        Vector3 shelter = WindCycle.Push(Vector3.right, 1f, 1f, false, true);
        Assert.AreEqual(StormTuning.PeakPush, full.magnitude, 0.0001f);
        Assert.AreEqual(0.5f, air.magnitude / full.magnitude, 0.0001f);
        Assert.AreEqual(0.2f, shelter.magnitude / full.magnitude, 0.0001f);
    }

    [Test]
    public void PushGatedWhenBlocked()
    {
        Assert.IsTrue(WindCycle.PushAllowed(false, false, false, false));
        Assert.IsFalse(WindCycle.PushAllowed(true, false, false, false));
        Assert.IsFalse(WindCycle.PushAllowed(false, true, false, false));
        Assert.IsFalse(WindCycle.PushAllowed(false, false, true, false));
        Assert.IsFalse(WindCycle.PushAllowed(false, false, false, true));
    }

    [Test]
    public void ZeroDeltaDoesNotAdvance()
    {
        WindCycle wind = new WindCycle(9, 0f, 1f, false);
        float before = wind.TimeToNextWarning;
        for (int i = 0; i < 100; i++)
        {
            wind.Tick(0f);
        }

        Assert.AreEqual(WindPhase.Calm, wind.Phase);
        Assert.AreEqual(before, wind.TimeToNextWarning, 0f);
    }

    [Test]
    public void TimingSemanticsInEveryPhase()
    {
        WindCycle wind = new WindCycle(11, 0f, 1f, false);

        // Calm: TimeToNextWarning counts down, WarningEndsIn is that plus 2.
        float calm = wind.TimeToNextWarning;
        Assert.GreaterOrEqual(calm, 12f);
        Assert.LessOrEqual(calm, 20f);
        Assert.AreEqual(calm + 2f, wind.WarningEndsIn, 0.0001f);
        wind.Tick(1f);
        Assert.AreEqual(calm - 1f, wind.TimeToNextWarning, 0.0001f);
        Assert.AreEqual(calm + 1f, wind.WarningEndsIn, 0.0001f);

        // Warning: starts now, ends in the remaining warning.
        while (wind.Phase != WindPhase.Warning)
        {
            wind.Tick(Step);
        }

        Assert.AreEqual(0f, wind.TimeToNextWarning, 0f);
        float warnRemaining = wind.WarningEndsIn;
        Assert.AreEqual(2f, warnRemaining, 0.02f);
        wind.Tick(0.5f);
        Assert.AreEqual(0f, wind.TimeToNextWarning, 0f);
        Assert.AreEqual(warnRemaining - 0.5f, wind.WarningEndsIn, 0.0001f);

        // Gust: remaining gust plus the next calm; the value is exact, so it
        // shrinks by dt each tick and then continues as the calm countdown.
        while (wind.Phase != WindPhase.Gust)
        {
            wind.Tick(Step);
        }

        float inGust = wind.TimeToNextWarning;
        Assert.GreaterOrEqual(inGust, 3f + 12f - 0.02f);
        Assert.LessOrEqual(inGust, 4f + 20f);
        Assert.AreEqual(inGust + 2f, wind.WarningEndsIn, 0.0001f);
        wind.Tick(1f);
        Assert.AreEqual(inGust - 1f, wind.TimeToNextWarning, 0.0001f);

        while (wind.Phase != WindPhase.Calm)
        {
            wind.Tick(Step);
        }

        float afterGust = wind.TimeToNextWarning;
        Assert.GreaterOrEqual(afterGust, 12f - 0.02f);
        Assert.LessOrEqual(afterGust, 20f);
    }
}
}
