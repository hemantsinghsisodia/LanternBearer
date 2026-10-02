using NUnit.Framework;
using UnityEngine;

namespace LanternKeeper.Tests
{
public class LightningTests
{
    private const float Step = 0.01f;
    private const float NoWarning = -1f;

    [Test]
    public void IntervalsWithinRange()
    {
        for (int difficulty = 0; difficulty <= 2; difficulty++)
        {
            Vector2 interval = StormTuning.LightningInterval(difficulty);
            LightningSchedule schedule = new LightningSchedule(100 + difficulty, interval);
            float now = 0f;
            float lastFlash = 0f;
            int strikes = 0;
            for (int i = 0; i < 1000000 && strikes < 50; i++)
            {
                schedule.Tick(Step, false, NoWarning, NoWarning);
                now += Step;
                if (!schedule.FlashedThisTick)
                {
                    continue;
                }

                float gap = now - lastFlash;
                Assert.GreaterOrEqual(gap, interval.x - 0.02f);
                Assert.LessOrEqual(gap, interval.y + 0.02f);
                lastFlash = now;
                strikes++;
            }

            Assert.AreEqual(50, strikes);
        }
    }

    [Test]
    public void PhaseOrderAndDurations()
    {
        LightningSchedule schedule = new LightningSchedule(5, new Vector2(30f, 45f));
        Assert.AreEqual(LightningPhase.Waiting, schedule.Phase);
        Assert.AreEqual(0f, schedule.Flash01, 0f);

        LightningPhase last = LightningPhase.Waiting;
        LightningPhase[] expected = { LightningPhase.Thunder, LightningPhase.Flash, LightningPhase.Afterglow, LightningPhase.Waiting };
        float elapsed = 0f;
        int index = 0;
        for (int i = 0; i < 100000 && index < expected.Length; i++)
        {
            schedule.Tick(Step, false, NoWarning, NoWarning);
            elapsed += Step;
            if (schedule.Phase == last)
            {
                if (last == LightningPhase.Flash)
                {
                    Assert.AreEqual(1f, schedule.Flash01, 0.0001f);
                }

                continue;
            }

            Assert.AreEqual(expected[index], schedule.Phase);
            if (last == LightningPhase.Thunder)
            {
                Assert.AreEqual(LightningSchedule.ThunderLead, elapsed, 0.02f);
            }
            else if (last == LightningPhase.Flash)
            {
                Assert.AreEqual(LightningSchedule.FlashSeconds, elapsed, 0.02f);
            }
            else if (last == LightningPhase.Afterglow)
            {
                Assert.AreEqual(LightningSchedule.AfterglowSeconds, elapsed, 0.02f);
            }

            elapsed = 0f;
            last = schedule.Phase;
            index++;
        }

        Assert.AreEqual(expected.Length, index);

        // Afterglow decays from 1 to 0.
        LightningSchedule glow = new LightningSchedule(5, new Vector2(30f, 45f));
        while (glow.Phase != LightningPhase.Afterglow)
        {
            glow.Tick(Step, false, NoWarning, NoWarning);
        }

        Assert.Greater(glow.Flash01, 0.9f);
        Assert.LessOrEqual(glow.Flash01, 1f);
        glow.Tick(0.25f, false, NoWarning, NoWarning);
        Assert.AreEqual(0.5f, glow.Flash01, 0.05f);
    }

    [Test]
    public void FlashedThisTickExactlyOncePerStrike()
    {
        LightningSchedule schedule = new LightningSchedule(8, new Vector2(30f, 45f));
        int flashes = 0;
        int flashPhaseTicks = 0;
        for (int i = 0; i < 20000; i++)
        {
            schedule.Tick(Step, false, NoWarning, NoWarning);
            if (schedule.FlashedThisTick)
            {
                flashes++;
                Assert.AreEqual(LightningPhase.Flash, schedule.Phase);
            }

            if (schedule.Phase == LightningPhase.Flash)
            {
                flashPhaseTicks++;
            }
        }

        Assert.GreaterOrEqual(flashes, 3);
        Assert.Greater(flashPhaseTicks, flashes);

        // A huge dt that skips several phases still reports the flash on that one tick only.
        LightningSchedule skip = new LightningSchedule(8, new Vector2(30f, 45f));
        skip.Tick(45f, false, NoWarning, NoWarning);
        Assert.IsTrue(skip.FlashedThisTick);
        skip.Tick(0f, false, NoWarning, NoWarning);
        Assert.IsFalse(skip.FlashedThisTick);
        skip.Tick(Step, false, NoWarning, NoWarning);
        Assert.IsFalse(skip.FlashedThisTick);
    }

    [Test]
    public void StrikeShiftedPastWindWarning()
    {
        float interval = 30f;
        LightningSchedule schedule = new LightningSchedule(2, new Vector2(interval, interval));

        // The undelayed strike would flash at t = 30 with thunder from 28.5. A warning runs 28.0 to 30.0.
        float warningStart = 28f;
        float warningEnd = 30f;
        float now = 0f;
        float flashAt = -1f;
        for (int i = 0; i < 6000 && flashAt < 0f; i++)
        {
            float startsIn = now < warningStart ? warningStart - now : (now < warningEnd ? 0f : NoWarning);
            float endsIn = now < warningEnd ? warningEnd - now : NoWarning;
            schedule.Tick(Step, false, startsIn, endsIn);
            now += Step;
            if (now > warningStart - 2f && now < warningEnd)
            {
                // Neither the thunder lead nor the flash may fall inside the warning.
                Assert.AreEqual(LightningPhase.Waiting, schedule.Phase);
            }

            if (schedule.FlashedThisTick)
            {
                flashAt = now;
            }
        }

        Assert.Greater(flashAt, 0f);
        Assert.GreaterOrEqual(flashAt, warningEnd + 0.1f + LightningSchedule.ThunderLead - 0.03f);
        Assert.LessOrEqual(flashAt, warningEnd + 0.1f + LightningSchedule.ThunderLead + 0.05f);

        // The next interval counts from the actual flash.
        float second = -1f;
        float t2 = flashAt;
        for (int i = 0; i < 6000 && second < 0f; i++)
        {
            schedule.Tick(Step, false, NoWarning, NoWarning);
            t2 += Step;
            if (schedule.FlashedThisTick)
            {
                second = t2;
            }
        }

        Assert.AreEqual(interval, second - flashAt, 0.05f);
    }

    [Test]
    public void StrikeNeverInsideWarningWindow()
    {
        // Fake wind cycle: calm 12 s, warning 2 s, gust 3 s (17 s total), fed in WindCycle's timing semantics.
        // A 5-6 s lightning interval makes strikes frequently collide with warnings.
        LightningSchedule schedule = new LightningSchedule(3, new Vector2(5f, 6f));
        float cycle = 17f;
        int flashes = 0;
        for (int i = 0; i < 60000; i++)
        {
            float t = (i * Step) % cycle;
            float startsIn;
            float endsIn;
            if (t < 12f)
            {
                startsIn = 12f - t;
                endsIn = startsIn + 2f;
            }
            else if (t < 14f)
            {
                startsIn = 0f;
                endsIn = 14f - t;
            }
            else
            {
                startsIn = cycle - t + 12f;
                endsIn = startsIn + 2f;
            }

            schedule.Tick(Step, false, startsIn, endsIn);
            bool inWarning = t >= 12f && t < 14f;
            if (schedule.FlashedThisTick)
            {
                flashes++;
                Assert.IsFalse(inWarning);
            }

            if (inWarning)
            {
                Assert.AreEqual(LightningPhase.Waiting, schedule.Phase);
            }
        }

        Assert.Greater(flashes, 10);
    }

    [Test]
    public void NoStrikeWhenRoundOver()
    {
        LightningSchedule schedule = new LightningSchedule(4, new Vector2(30f, 45f));
        for (int i = 0; i < 20000; i++)
        {
            schedule.Tick(Step, true, NoWarning, NoWarning);
            Assert.AreEqual(LightningPhase.Waiting, schedule.Phase);
            Assert.IsFalse(schedule.FlashedThisTick);
            Assert.AreEqual(0f, schedule.Flash01, 0f);
        }

        // Round ends mid-strike: no flash afterwards.
        LightningSchedule mid = new LightningSchedule(4, new Vector2(30f, 45f));
        while (mid.Phase != LightningPhase.Thunder)
        {
            mid.Tick(Step, false, NoWarning, NoWarning);
        }

        mid.Tick(0.5f, true, NoWarning, NoWarning);
        Assert.AreEqual(LightningPhase.Waiting, mid.Phase);
        for (int i = 0; i < 20000; i++)
        {
            mid.Tick(Step, true, NoWarning, NoWarning);
            Assert.IsFalse(mid.FlashedThisTick);
            Assert.AreEqual(LightningPhase.Waiting, mid.Phase);
        }

        // A big dt with roundOver true never fires either.
        LightningSchedule big = new LightningSchedule(4, new Vector2(30f, 45f));
        big.Tick(500f, true, NoWarning, NoWarning);
        Assert.IsFalse(big.FlashedThisTick);
        Assert.AreEqual(LightningPhase.Waiting, big.Phase);
    }

    [Test]
    public void ZeroDeltaDoesNotAdvance()
    {
        LightningSchedule schedule = new LightningSchedule(6, new Vector2(30f, 45f));
        LightningSchedule twin = new LightningSchedule(6, new Vector2(30f, 45f));
        schedule.Tick(20f, false, NoWarning, NoWarning);
        twin.Tick(20f, false, NoWarning, NoWarning);
        for (int i = 0; i < 100; i++)
        {
            schedule.Tick(0f, false, NoWarning, NoWarning);
            Assert.IsFalse(schedule.FlashedThisTick);
        }

        Assert.AreEqual(twin.Phase, schedule.Phase);

        // The zero ticks left no trace: both flash on the same tick.
        int a = -1;
        int b = -1;
        for (int i = 0; i < 6000; i++)
        {
            schedule.Tick(Step, false, NoWarning, NoWarning);
            twin.Tick(Step, false, NoWarning, NoWarning);
            if (a < 0 && schedule.FlashedThisTick)
            {
                a = i;
            }

            if (b < 0 && twin.FlashedThisTick)
            {
                b = i;
            }
        }

        Assert.GreaterOrEqual(a, 0);
        Assert.AreEqual(b, a);
    }
}
}
