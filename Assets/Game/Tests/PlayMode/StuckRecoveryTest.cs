using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LanternKeeper.Tests
{
// The keeper must never end up wedged in a solid: StuckRecovery frees a keeper that is inside a rock while move input is held, leaves
// a keeper that merely walks (or leans on a wall) alone, and a knock-back beside a wall stops at the wall instead of driving the
// CharacterController into it. A synthetic keeper (CharacterController + PlayerController) on a plain floor; game types by reflection.
// Every frame advances the game by the same step (captureDeltaTime), so timings do not depend on how slowly the editor renders.
public class StuckRecoveryTest
{
    const float Frame = 1f / 60f;
    const float Radius = 0.28f;
    const float Height = 1.7f;
    // Far from every island, so a scene left loaded by an earlier test cannot touch the test world.
    static readonly Vector3 Origin = new Vector3(4000f, 4000f, 4000f);

    GameObject root;
    float previousCaptureDelta;
    float previousMaxDelta;

    static Type GameType(string name)
    {
        Type type = Type.GetType("LanternKeeper." + name + ", Assembly-CSharp");
        Assert.IsNotNull(type, name + " type not found");
        return type;
    }

    static void SetExternalMove(Vector2 move)
    {
        FieldInfo field = GameType("PlayerController").GetField("ExternalMove", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(field, "PlayerController.ExternalMove");
        field.SetValue(null, move);
    }

    [SetUp]
    public void SetUp()
    {
        // An earlier test can leave a scene loaded with a paused, locked or dying GameManager, which blanks the keeper's input.
        PropertyInfo instance = GameType("GameManager").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
        Component leftover = instance.GetValue(null) as Component;
        if (leftover != null)
        {
            UnityEngine.Object.DestroyImmediate(leftover.gameObject);
        }

        FieldInfo externalYaw = GameType("CameraFollow").GetField("UseExternalYaw", BindingFlags.Public | BindingFlags.Static);
        externalYaw.SetValue(null, false);
        previousCaptureDelta = Time.captureDeltaTime;
        previousMaxDelta = Time.maximumDeltaTime;
        Time.captureDeltaTime = Frame;
        Time.maximumDeltaTime = 0.05f;
        root = new GameObject("StuckRecoveryTestRoot");
        root.transform.position = Origin;
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(root.transform, false);
        floor.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        floor.transform.localScale = new Vector3(100f, 1f, 100f);
    }

    [TearDown]
    public void TearDown()
    {
        SetExternalMove(Vector2.zero);
        UnityEngine.Object.Destroy(root);
        Time.captureDeltaTime = previousCaptureDelta;
        Time.maximumDeltaTime = previousMaxDelta;
    }

    GameObject Solid(string name, Vector3 centre, Vector3 size)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(root.transform, false);
        cube.transform.localPosition = centre;
        cube.transform.localScale = size;
        Physics.SyncTransforms();
        return cube;
    }

    GameObject Keeper(Vector3 position)
    {
        GameObject keeper = new GameObject("TestKeeper");
        keeper.transform.SetParent(root.transform, false);
        keeper.transform.localPosition = position;
        CharacterController controller = keeper.AddComponent<CharacterController>();
        controller.height = Height;
        controller.radius = Radius;
        controller.center = new Vector3(0f, 0.88f, 0f);
        controller.skinWidth = 0.08f;
        keeper.AddComponent(GameType("PlayerController"));
        return keeper;
    }

    // True when the keeper's capsule (a little slimmer than the controller, feet lifted off the floor) overlaps a solid other than the keeper.
    static bool Embedded(GameObject keeper)
    {
        float radius = Radius * 0.9f;
        Vector3 centre = keeper.transform.position + new Vector3(0f, 0.88f, 0f);
        Vector3 bottom = centre + Vector3.down * (Height * 0.5f) + Vector3.up * (radius + 0.15f);
        Vector3 top = centre + Vector3.up * (Height * 0.5f) - Vector3.up * radius;
        Collider[] hits = Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (!hits[i].transform.IsChildOf(keeper.transform))
            {
                return true;
            }
        }

        return false;
    }

    static int Recoveries(GameObject keeper)
    {
        Component recovery = keeper.GetComponent(GameType("StuckRecovery"));
        Assert.IsNotNull(recovery, "the keeper gets a StuckRecovery from PlayerController.Awake");
        return (int)recovery.GetType().GetProperty("Recoveries", BindingFlags.Public | BindingFlags.Instance).GetValue(recovery);
    }

    [UnityTest]
    public IEnumerator StuckKeeperIsFreed()
    {
        // Wedged between two boulders 0.4 m apart (the capsule is 0.56 m wide, so it overlaps both and the controller's own overlap
        // recovery cannot resolve it), with input held across the gap: he cannot move.
        Solid("BoulderA", new Vector3(0.7f, 1.5f, 0f), new Vector3(1f, 3f, 20f));
        Solid("BoulderB", new Vector3(-0.7f, 1.5f, 0f), new Vector3(1f, 3f, 20f));
        GameObject keeper = Keeper(new Vector3(0f, 0f, 0f));
        yield return null;
        Physics.SyncTransforms();
        Assert.IsTrue(Embedded(keeper), "the keeper starts inside the boulder");
        SetExternalMove(new Vector2(1f, 0f));
        int freeAt = -1;
        for (int frame = 0; frame < 120; frame++)
        {
            yield return null;
            if (!Embedded(keeper))
            {
                freeAt = frame;
                break;
            }
        }

        Assert.GreaterOrEqual(freeAt, 0, "the keeper is still inside the boulders 2 s after input was held (at " + keeper.transform.localPosition.ToString("F2")
            + ", recoveries " + Recoveries(keeper) + ", input held " + GameType("PlayerController").GetProperty("MoveInputHeld").GetValue(keeper.GetComponent(GameType("PlayerController"))) + ")");
        Assert.GreaterOrEqual(Recoveries(keeper), 1, "StuckRecovery freed him (free at frame " + freeAt + ", at " + keeper.transform.localPosition.ToString("F2") + ")");
    }

    [UnityTest]
    public IEnumerator StuckRecoveryIgnoresNormalWalking()
    {
        GameObject keeper = Keeper(new Vector3(-20f, 0f, 0f));
        yield return null;
        SetExternalMove(new Vector2(1f, 0f));
        for (int frame = 0; frame < 180; frame++)
        {
            yield return null;
        }

        Assert.Greater(keeper.transform.localPosition.x, -20f + 6f, "he walked at least 6 m in 3 s");
        Assert.AreEqual(0, Recoveries(keeper), "walking is not being stuck");

        // Leaning on a wall for 5 s (jumping into a wall, or a ledge) is not an overlap either: no teleport.
        Solid("Wall", new Vector3(keeper.transform.localPosition.x + 4f, 2f, 0f), new Vector3(1f, 4f, 10f));
        float wallFace = keeper.transform.localPosition.x + 4f - 0.5f;
        for (int frame = 0; frame < 300; frame++)
        {
            yield return null;
        }

        Assert.AreEqual(0, Recoveries(keeper), "leaning on a wall is not being stuck");
        Assert.LessOrEqual(keeper.transform.localPosition.x, wallFace, "he is still on the near side of the wall");
        Assert.GreaterOrEqual(keeper.transform.localPosition.x, wallFace - 1f, "he stayed at the wall, not moved away");
    }

    [UnityTest]
    public IEnumerator KnockbackNeverEmbedsInWall()
    {
        Solid("Wall", new Vector3(2.5f, 2f, 0f), new Vector3(1f, 4f, 10f));
        GameObject keeper = Keeper(Vector3.zero);
        yield return null;
        Component player = keeper.GetComponent(GameType("PlayerController"));
        MethodInfo knock = player.GetType().GetMethod("Knockback", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(knock, "PlayerController.Knockback");
        // A 3 m strike straight at a wall 2 m away, then one from an angle.
        Vector3[] directions = { Vector3.right, new Vector3(1f, 0f, 0.7f), new Vector3(1f, 0f, -0.4f) };
        for (int d = 0; d < directions.Length; d++)
        {
            keeper.transform.localPosition = new Vector3(0f, 0f, 0f);
            Physics.SyncTransforms();
            knock.Invoke(player, new object[] { directions[d], 3f });
            for (int frame = 0; frame < 40; frame++)
            {
                yield return null;
                Assert.IsFalse(Embedded(keeper), "strike " + d + " frame " + frame + ": the controller is inside the wall");
            }

            Assert.LessOrEqual(keeper.transform.localPosition.x, 2f - Radius + 0.01f, "strike " + d + " stops at the wall");
        }
    }
}
}
