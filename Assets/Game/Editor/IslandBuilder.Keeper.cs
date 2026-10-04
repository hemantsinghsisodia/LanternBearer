using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace LanternKeeper
{
public static partial class IslandBuilder
{
    const string KeeperFbxPath = "Assets/Game/Models/Keeper/Keeper.fbx";
    const string KeeperControllerPath = "Assets/Game/Models/Keeper/Keeper.controller";
    const string KeeperPrefabPath = "Assets/Game/Prefabs/Characters/Keeper.prefab";
    const string KeeperTextureFolder = "Assets/Game/Models/Keeper/Textures";

    static bool keeperImportReady;

    static void PrepareKeeperImport()
    {
        if (keeperImportReady || !File.Exists(ProjectFile(KeeperFbxPath)))
        {
            return;
        }

        AssetDatabase.ImportAsset(KeeperFbxPath, ImportAssetOptions.ForceUpdate);
        ModelImporter importer = AssetImporter.GetAtPath(KeeperFbxPath) as ModelImporter;
        if (importer == null)
        {
            return;
        }

        importer.globalScale = 1f;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        // Feet are parented to Root on this Quaternius rig, so Unity rejects a Humanoid avatar.
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        ApplyKeeperLoops(importer);
        ExtractKeeperTextures(importer);
        importer.SaveAndReimport();
        Debug.Log("Keeper uses a Generic avatar. The Poly Pizza rig parents Foot.L and Foot.R to Root, so a Humanoid avatar cannot map LeftFoot.");

        EnsureKeeperController();
        keeperImportReady = true;
    }

    static void ExtractKeeperTextures(ModelImporter importer)
    {
        SerializedObject serialized = new SerializedObject(importer);
        SerializedProperty embedded = serialized.FindProperty("m_HasEmbeddedTextures");
        bool hasEmbedded = embedded != null && embedded.boolValue;
        string looseFolder = FindLooseKeeperTextureFolder();
        if (hasEmbedded)
        {
            EnsureFolder(KeeperTextureFolder);
            bool extracted = importer.ExtractTextures(KeeperTextureFolder);
            Debug.Log("Extracted embedded keeper textures into " + KeeperTextureFolder + " (" + extracted + ").");
            return;
        }

        if (looseFolder == null)
        {
            Debug.Log("Keeper FBX has no embedded textures and no loose maps beside the model.");
            return;
        }

        SerializedProperty search = serialized.FindProperty("m_SearchTexturesGlobally");
        if (search != null && !search.boolValue)
        {
            search.boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        Debug.Log("Keeper texture search widened for loose maps in " + looseFolder + ".");
    }

    static string FindLooseKeeperTextureFolder()
    {
        string fbm = "Assets/Game/Models/Keeper/Keeper.fbm";
        if (FolderHasTextures(fbm))
        {
            return fbm;
        }

        if (FolderHasTextures(KeeperTextureFolder))
        {
            return KeeperTextureFolder;
        }

        if (FolderHasTextures("Assets/Game/Models/Keeper"))
        {
            return "Assets/Game/Models/Keeper";
        }

        return null;
    }

    static bool FolderHasTextures(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            return false;
        }

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        return guids != null && guids.Length > 0;
    }

    static void ApplyKeeperLoops(ModelImporter importer)
    {
        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.clipAnimations;
        }

        for (int i = 0; i < clips.Length; i++)
        {
            string leaf = ClipLeaf(clips[i].name);
            if (leaf == "Idle" || leaf == "Walk" || leaf == "Run")
            {
                clips[i].loopTime = true;
                clips[i].loopPose = leaf != "Idle";
            }
            else if (leaf == "LanternHold")
            {
                clips[i].loopTime = true;
                clips[i].loopPose = true;
            }
            else if (leaf == "Lean")
            {
                // Frame 0 is the rest pose; frames 1-2 hold the bent spine, so the clip adds only the bend.
                clips[i].loopTime = true;
                clips[i].loopPose = true;
                clips[i].firstFrame = 1f;
                clips[i].lastFrame = 2f;
                clips[i].hasAdditiveReferencePose = true;
                clips[i].additiveReferencePoseFrame = 0f;
            }
        }

        importer.clipAnimations = clips;
    }

    static bool KeeperAvatarIsHuman()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(KeeperFbxPath);
        for (int i = 0; i < assets.Length; i++)
        {
            Avatar avatar = assets[i] as Avatar;
            if (avatar != null && avatar.isValid && avatar.isHuman)
            {
                return true;
            }
        }

        return false;
    }

    static HumanBone[] KeeperHumanBones()
    {
        return new[]
        {
            HumanBoneOf("Hips", "Hips"),
            HumanBoneOf("Spine", "Abdomen"),
            HumanBoneOf("Chest", "Torso"),
            HumanBoneOf("UpperChest", "Chest"),
            HumanBoneOf("Neck", "Neck"),
            HumanBoneOf("Head", "Head"),
            HumanBoneOf("LeftShoulder", "Shoulder.L"),
            HumanBoneOf("LeftUpperArm", "UpperArm.L"),
            HumanBoneOf("LeftLowerArm", "LowerArm.L"),
            HumanBoneOf("LeftHand", "Wrist.L"),
            HumanBoneOf("RightShoulder", "Shoulder.R"),
            HumanBoneOf("RightUpperArm", "UpperArm.R"),
            HumanBoneOf("RightLowerArm", "LowerArm.R"),
            HumanBoneOf("RightHand", "Wrist.R"),
            HumanBoneOf("LeftUpperLeg", "UpperLeg.L"),
            HumanBoneOf("LeftLowerLeg", "LowerLeg.L"),
            HumanBoneOf("LeftFoot", "Foot.L"),
            HumanBoneOf("LeftToes", "PT.L"),
            HumanBoneOf("RightUpperLeg", "UpperLeg.R"),
            HumanBoneOf("RightLowerLeg", "LowerLeg.R"),
            HumanBoneOf("RightFoot", "Foot.R"),
            HumanBoneOf("RightToes", "PT.R")
        };
    }

    static HumanBone HumanBoneOf(string human, string bone)
    {
        HumanBone mapped = new HumanBone();
        mapped.humanName = human;
        mapped.boneName = bone;
        HumanLimit limit = new HumanLimit();
        limit.useDefaultValues = true;
        mapped.limit = limit;
        return mapped;
    }

    const string KeeperArmMaskPath = "Assets/Game/Models/Keeper/KeeperArmMask.mask";
    const string KeeperSpineMaskPath = "Assets/Game/Models/Keeper/KeeperSpineMask.mask";
    const string KeeperUpperMaskPath = "Assets/Game/Models/Keeper/KeeperUpperMask.mask";

    static RuntimeAnimatorController EnsureKeeperController()
    {
        AnimationClip idle = FindKeeperClip("Idle");
        AnimationClip walk = FindKeeperClip("Walk");
        AnimationClip run = FindKeeperClip("Run");
        if (idle == null || walk == null || run == null)
        {
            Debug.LogError("Keeper clips missing. Idle=" + (idle != null) + " Walk=" + (walk != null) + " Run=" + (run != null));
            return null;
        }

        AnimationClip hold = FindKeeperClip("LanternHold");
        AnimationClip lean = FindKeeperClip("Lean");
        AnimationClip hit = FindKeeperClip("HitRecieve");
        AnimationClip interact = FindKeeperClip("Interact");
        AnimationClip death = FindKeeperClip("Death");
        AnimationClip shake = FindKeeperClip("HitRecieve_2");
        if (hold == null || lean == null || hit == null || interact == null || death == null || shake == null)
        {
            Debug.LogError("Keeper action clips missing. LanternHold=" + (hold != null) + " Lean=" + (lean != null) + " HitRecieve=" + (hit != null) + " Interact=" + (interact != null) + " Death=" + (death != null) + " HitRecieve_2=" + (shake != null));
            return null;
        }

        AvatarMask armMask = EnsureKeeperMask(KeeperArmMaskPath, new[] { "Shoulder.R", "UpperArm.R", "LowerArm.R", "Wrist.R" }, true);
        AvatarMask spineMask = EnsureKeeperMask(KeeperSpineMaskPath, new[] { "Abdomen", "Torso", "Chest", "Neck", "Head" }, false);
        // Everything under Abdomen: spine, neck, head, both shoulders, arms, hands and fingers. Not Hips, not the legs.
        AvatarMask upperMask = EnsureKeeperMask(KeeperUpperMaskPath, new[] { "Abdomen" }, true);

        // Rebuild in place so the controller keeps its GUID and the prefab Animator reference survives.
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(KeeperControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(KeeperControllerPath);
        }
        else
        {
            ResetKeeperController(controller);
        }

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Stagger", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Light", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("ShakeOff", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        BlendTree tree = new BlendTree();
        tree.name = "Locomotion";
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        AssetDatabase.AddObjectToAsset(tree, controller);
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, 4.5f);
        tree.AddChild(run, 9f);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState state = machine.AddState("Move");
        state.motion = tree;
        machine.defaultState = state;

        AnimatorStateMachine armMachine = AddKeeperLayer(controller, "LanternArm", armMask, AnimatorLayerBlendingMode.Override, 1f);
        AnimatorState armState = armMachine.AddState("LanternHold");
        armState.motion = hold;
        armMachine.defaultState = armState;

        AnimatorStateMachine leanMachine = AddKeeperLayer(controller, "Lean", spineMask, AnimatorLayerBlendingMode.Additive, 0f);
        AnimatorState leanState = leanMachine.AddState("Lean");
        leanState.motion = lean;
        leanMachine.defaultState = leanState;

        AnimatorStateMachine actions = AddKeeperLayer(controller, "Actions", null, AnimatorLayerBlendingMode.Override, 1f);
        AnimatorState empty = actions.AddState("Empty");
        actions.defaultState = empty;
        AddActionState(actions, empty, "Stagger", hit, "Stagger", true);
        AddActionState(actions, empty, "ShakeOff", shake, "ShakeOff", true);
        AddActionState(actions, empty, "Die", death, "Die", false);

        // The beacon reach is upper body only, so the legs keep running under it.
        AnimatorStateMachine upper = AddKeeperLayer(controller, "UpperActions", upperMask, AnimatorLayerBlendingMode.Override, 1f);
        AnimatorState upperEmpty = upper.AddState("Empty");
        upper.defaultState = upperEmpty;
        AddActionState(upper, upperEmpty, "Light", interact, "Light", true);
        // Death wins: once Dead is set the reach is dropped so it cannot show over the Death clip.
        AnimatorStateTransition drop = upper.AddAnyStateTransition(upperEmpty);
        drop.hasExitTime = false;
        drop.hasFixedDuration = true;
        drop.duration = 0.1f;
        drop.canTransitionToSelf = false;
        drop.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    static void ResetKeeperController(AnimatorController controller)
    {
        for (int i = controller.layers.Length - 1; i >= 1; i--)
        {
            controller.RemoveLayer(i);
        }

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        ChildAnimatorState[] states = machine.states;
        for (int i = 0; i < states.Length; i++)
        {
            machine.RemoveState(states[i].state);
        }

        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = parameters.Length - 1; i >= 0; i--)
        {
            controller.RemoveParameter(i);
        }

        Object[] subs = AssetDatabase.LoadAllAssetsAtPath(KeeperControllerPath);
        for (int i = 0; i < subs.Length; i++)
        {
            if (subs[i] is BlendTree)
            {
                Object.DestroyImmediate(subs[i], true);
            }
        }
    }

    static AnimatorStateMachine AddKeeperLayer(AnimatorController controller, string name, AvatarMask mask, AnimatorLayerBlendingMode mode, float weight)
    {
        controller.AddLayer(name);
        AnimatorControllerLayer[] layers = controller.layers;
        AnimatorControllerLayer layer = layers[layers.Length - 1];
        layer.avatarMask = mask;
        layer.blendingMode = mode;
        layer.defaultWeight = weight;
        controller.layers = layers;
        return controller.layers[layers.Length - 1].stateMachine;
    }

    static AnimatorState AddActionState(AnimatorStateMachine machine, AnimatorState empty, string name, AnimationClip clip, string trigger, bool returns)
    {
        AnimatorState state = machine.AddState(name);
        state.motion = clip;
        AnimatorStateTransition entry = machine.AddAnyStateTransition(state);
        entry.hasExitTime = false;
        entry.hasFixedDuration = true;
        entry.duration = 0.1f;
        entry.canTransitionToSelf = returns;
        entry.AddCondition(AnimatorConditionMode.If, 0f, trigger);
        if (returns)
        {
            // Death always wins: a stagger, reach or shake-off cannot start once Dead is set.
            entry.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
            AnimatorStateTransition exit = state.AddTransition(empty);
            exit.hasExitTime = true;
            exit.exitTime = 0.9f;
            exit.hasFixedDuration = true;
            exit.duration = 0.15f;
        }

        return state;
    }

    // Generic-rig mask: every transform listed, active only for the named bones (and, when asked, their descendants).
    static AvatarMask EnsureKeeperMask(string path, string[] boneNames, bool includeDescendants)
    {
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, path);
        }

        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(KeeperFbxPath);
        Transform[] all = model.GetComponentsInChildren<Transform>(true);
        HashSet<Transform> active = new HashSet<Transform>();
        for (int i = 0; i < boneNames.Length; i++)
        {
            Transform bone = FindDeep(model.transform, boneNames[i]);
            if (bone == null)
            {
                Debug.LogError("Keeper rig has no bone " + boneNames[i] + " for " + path);
                continue;
            }

            active.Add(bone);
            if (includeDescendants)
            {
                Transform[] below = bone.GetComponentsInChildren<Transform>(true);
                for (int d = 0; d < below.Length; d++)
                {
                    active.Add(below[d]);
                }
            }
        }

        for (int part = 0; part < (int)AvatarMaskBodyPart.LastBodyPart; part++)
        {
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)part, false);
        }

        mask.transformCount = all.Length;
        for (int i = 0; i < all.Length; i++)
        {
            mask.SetTransformPath(i, AnimationUtility.CalculateTransformPath(all[i], model.transform));
            mask.SetTransformActive(i, active.Contains(all[i]));
        }

        EditorUtility.SetDirty(mask);
        AssetDatabase.SaveAssetIfDirty(mask);
        return mask;
    }

    static AnimationClip FindKeeperClip(string leaf)
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(KeeperFbxPath);
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip == null || clip.name.StartsWith("__preview"))
            {
                continue;
            }

            if (ClipLeaf(clip.name) == leaf)
            {
                return clip;
            }
        }

        return null;
    }

    static string ClipLeaf(string name)
    {
        int bar = name.LastIndexOf('|');
        return bar >= 0 ? name.Substring(bar + 1) : name;
    }

    static bool KeeperClipsHaveFootstepEvents()
    {
        string[] leaves = { "Idle", "Walk", "Run" };
        for (int i = 0; i < leaves.Length; i++)
        {
            AnimationClip clip = FindKeeperClip(leaves[i]);
            if (clip != null && clip.events != null && clip.events.Length > 0)
            {
                return true;
            }
        }

        return false;
    }

    static GameObject BuildImportedKeeper(ArtKit art)
    {
        if (!File.Exists(ProjectFile(KeeperFbxPath)))
        {
            return null;
        }

        PrepareKeeperImport();
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(KeeperFbxPath);
        if (model == null)
        {
            return null;
        }

        GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(model);
        root.name = "Keeper";
        TintKeeper(root);
        StripColliders(root);
        Transform hand = FindDeep(root.transform, "Wrist.R");
        if (hand == null)
        {
            hand = FindDeep(root.transform, "RightHand");
        }

        if (hand == null)
        {
            hand = root.transform;
        }

        AttachLantern(art, root.transform, hand);
        AttachDust(art, root.transform);
        Animator animator = root.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            animator = root.AddComponent<Animator>();
        }

        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(KeeperControllerPath);
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        KeeperAnimator driver = root.GetComponent<KeeperAnimator>();
        if (driver == null)
        {
            driver = root.AddComponent<KeeperAnimator>();
        }

        SerializedObject driverObject = new SerializedObject(driver);
        driverObject.FindProperty("animator").objectReferenceValue = animator;
        driverObject.ApplyModifiedPropertiesWithoutUndo();

        CharacterController controller = root.GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = root.AddComponent<CharacterController>();
        }

        controller.height = 1.7f;
        controller.radius = 0.28f;
        controller.center = new Vector3(0f, 0.88f, 0f);
        controller.stepOffset = 0.4f;
        controller.slopeLimit = 52f;
        PlayerController player = root.GetComponent<PlayerController>();
        if (player == null)
        {
            player = root.AddComponent<PlayerController>();
        }

        player.UseDistanceFootsteps = !KeeperClipsHaveFootstepEvents();
        return root;
    }

    const string KeeperMaterialFolder = "Assets/Game/Art/Keeper";
    const string LanternMaterialFolder = "Assets/Game/Art/Lantern";

    static void TintKeeper(GameObject root)
    {
        Material cloth = EnsureKeeperMaterials();
        Material leather = AssetDatabase.LoadAssetAtPath<Material>(KeeperMaterialFolder + "/KeeperLeather.mat");
        Material skin = AssetDatabase.LoadAssetAtPath<Material>(KeeperMaterialFolder + "/KeeperSkin.mat");
        Material dark = AssetDatabase.LoadAssetAtPath<Material>(KeeperMaterialFolder + "/KeeperDark.mat");
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] source = renderers[i].sharedMaterials;
            Material[] authored = new Material[source.Length];
            for (int m = 0; m < source.Length; m++)
            {
                string slot = source[m] != null ? source[m].name : "";
                authored[m] = KeeperMaterialForSlot(slot, cloth, leather, skin, dark);
            }

            renderers[i].sharedMaterials = authored;
        }
    }

    // Slot names come from the FBX. Black (trousers, hair) uses KeeperDark: it must not use cloth, which sways.
    // Every brown/gold slot reads as dark leather.
    static Material KeeperMaterialForSlot(string slot, Material cloth, Material leather, Material skin, Material dark)
    {
        if (slot == "Black")
        {
            return dark;
        }

        if (slot == "KeeperCloth")
        {
            return cloth;
        }

        if (slot == "Skin")
        {
            return skin;
        }

        return leather;
    }

    static Material EnsureKeeperMaterials()
    {
        EnsureFolder("Assets/Game/Art");
        EnsureFolder(KeeperMaterialFolder);
        EnsureFolder(LanternMaterialFolder);
        Material cloth = EnsureKeeperLitMaterial(KeeperMaterialFolder + "/KeeperCloth.mat", "2E3A4F", 0.18f, 0f, 0f);
        cloth.SetFloat("_SwayStrength", 0.12f);
        cloth.SetFloat("_VertexAO", 1f);
        cloth.SetFloat("_EdgeLighten", 0.25f);
        cloth.SetFloat("_RimStrength", 0.7f);
        EditorUtility.SetDirty(cloth);
        EnsureKeeperLitMaterial(KeeperMaterialFolder + "/KeeperLeather.mat", "2B2119", 0.3f, 0f, 0f);
        EnsureKeeperLitMaterial(KeeperMaterialFolder + "/KeeperDark.mat", "232C3B", 0.18f, 0f, 0f);
        Material skin = EnsureKeeperLitMaterial(KeeperMaterialFolder + "/KeeperSkin.mat", "A5806A", 0.28f, 0f, 0.2f);
        skin.SetFloat("_VertexAO", 1f);
        EditorUtility.SetDirty(skin);
        Material iron = EnsureKeeperLitMaterial(LanternMaterialFolder + "/LanternIron.mat", "2A2A2E", 0.35f, 0.6f, 0f);
        iron.SetFloat("_RimStrength", 1f);
        EditorUtility.SetDirty(iron);
        EnsureLanternGlassMaterial();
        AssetDatabase.SaveAssets();
        return cloth;
    }

    static Material EnsureKeeperLitMaterial(string path, string hex, float smoothness, float metallic, float desaturate)
    {
        Shader shader = Shader.Find("LanternKeeper/KeeperLit");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.shader = shader;
        mat.SetColor("_BaseColor", LookPalette.FromHex(hex));
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Desaturate", desaturate);
        mat.SetColor("_RimColor", new Color(0.58f, 0.72f, 0.95f, 1f));
        mat.SetFloat("_RimPower", 2.4f);
        mat.SetFloat("_RimStrength", 0.7f);
        mat.SetFloat("_SwayStrength", 0f);
        mat.SetFloat("_VertexAO", 0f);
        mat.SetFloat("_EdgeLighten", 0f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material EnsureLanternGlassMaterial()
    {
        string path = LanternMaterialFolder + "/LanternGlass.mat";
        Shader shader = Shader.Find("LanternKeeper/AdditiveUnlit");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.shader = shader;
        Color core = LookPalette.FromHex(LookPalette.GlowCore);
        mat.SetColor("_BaseColor", new Color(core.r, core.g, core.b, 0.35f));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static string Sanitize(string value)
    {
        char[] chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }

    static GameObject BuildPrimitiveKeeper(ArtKit art)
    {
        GameObject root = new GameObject("Keeper");
        GameObject body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.92f, 0f);
        Prim(PrimitiveType.Capsule, "Torso", body.transform, new Vector3(0f, 0.15f, 0f), new Vector3(0.38f, 0.32f, 0.26f), art.cloak);
        Prim(PrimitiveType.Sphere, "Hood", body.transform, new Vector3(0f, 0.62f, -0.02f), new Vector3(0.36f, 0.34f, 0.34f), art.cloak);
        GameObject head = Prim(PrimitiveType.Sphere, "Head", body.transform, new Vector3(0f, 0.58f, 0.06f), new Vector3(0.2f, 0.22f, 0.2f), art.skin);
        Prim(PrimitiveType.Cube, "EyeL", head.transform, new Vector3(-0.35f, 0.12f, 0.42f), new Vector3(0.16f, 0.08f, 0.06f), art.iron);
        Prim(PrimitiveType.Cube, "EyeR", head.transform, new Vector3(0.35f, 0.12f, 0.42f), new Vector3(0.16f, 0.08f, 0.06f), art.iron);
        AddLimb(root.transform, "Leg.L", new Vector3(-0.12f, 0.78f, 0f), art.cloak, art.skin);
        AddLimb(root.transform, "Leg.R", new Vector3(0.12f, 0.78f, 0f), art.cloak, art.skin);
        Transform armR = AddLimb(root.transform, "Arm.R", new Vector3(0.28f, 1.15f, 0f), art.cloak, art.skin);
        AddLimb(root.transform, "Arm.L", new Vector3(-0.28f, 1.15f, 0f), art.cloak, art.skin);
        AttachLantern(art, root.transform, armR);
        AttachDust(art, root.transform);
        StripColliders(root);
        CharacterController controller = root.AddComponent<CharacterController>();
        controller.height = 1.7f;
        controller.radius = 0.28f;
        controller.center = new Vector3(0f, 0.88f, 0f);
        root.AddComponent<PlayerController>();
        root.AddComponent<KeeperAnimator>();
        Debug.Log("Keeper used the primitive humanoid fallback. No imported rig was available.");
        return root;
    }

    static Transform AddLimb(Transform root, string name, Vector3 hip, Material cloth, Material skin)
    {
        GameObject limb = new GameObject(name);
        limb.transform.SetParent(root, false);
        limb.transform.localPosition = hip;
        Prim(PrimitiveType.Capsule, "Upper", limb.transform, new Vector3(0f, -0.16f, 0f), new Vector3(0.1f, 0.16f, 0.1f), cloth);
        GameObject lower = new GameObject("Lower");
        lower.transform.SetParent(limb.transform, false);
        lower.transform.localPosition = new Vector3(0f, -0.34f, 0f);
        Prim(PrimitiveType.Capsule, "Mesh", lower.transform, new Vector3(0f, -0.16f, 0f), new Vector3(0.09f, 0.16f, 0.09f), cloth);
        Transform hand = new GameObject("Hand").transform;
        hand.SetParent(lower.transform, false);
        hand.localPosition = new Vector3(0f, -0.34f, 0.04f);
        Prim(PrimitiveType.Sphere, "Palm", hand, Vector3.zero, Vector3.one * 0.08f, skin);
        return hand;
    }

    const string LanternFbxPath = "Assets/Game/Art/Lantern/Lantern.fbx";
    const string LanternFlameMaterialPath = "Assets/Game/Art/Lantern/LanternFlame.mat";
    // Flame quad size in metres at full fuel (width, height). The flame shader billboards it.
    static readonly Vector3 LanternFlameSize = new Vector3(0.05f, 0.1f, 1f);

    static void AttachLantern(ArtKit art, Transform root, Transform hand)
    {
        // HandSocket sits in the closed fist of the re-exported rig; the bare wrist is only a fallback.
        Transform socket = FindDeep(root, "HandSocket");
        GameObject pivot = new GameObject("LanternPivot");
        pivot.transform.SetParent(socket != null ? socket : hand, false);
        pivot.transform.localPosition = Vector3.zero;
        pivot.transform.localRotation = Quaternion.identity;
        pivot.transform.localScale = Vector3.one;

        GameObject fbx = AssetDatabase.LoadAssetAtPath<GameObject>(LanternFbxPath);
        GameObject lantern;
        Transform anchor;
        if (fbx != null)
        {
            lantern = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            lantern.name = "Lantern";
            lantern.transform.SetParent(pivot.transform, false);
            lantern.transform.localPosition = Vector3.zero;
            lantern.transform.localRotation = Quaternion.identity;
            lantern.transform.localScale = Vector3.one;
            anchor = FindDeep(lantern.transform, "FlameAnchor");
            AssignLanternMaterials(lantern.transform);
        }
        else
        {
            Debug.LogWarning("Lantern.fbx is missing, using the cube lantern fallback.");
            lantern = Prim(PrimitiveType.Cube, "Lantern", pivot.transform, Vector3.zero, new Vector3(0.12f, 0.16f, 0.12f), art.lanternMat);
            anchor = null;
        }

        if (anchor == null)
        {
            anchor = lantern.transform;
        }

        GameObject lightObject = new GameObject("LanternLight");
        lightObject.transform.SetParent(lantern.transform, false);
        Light light = lightObject.AddComponent<Light>();
        ConfigureLanternPoint(light);
        PlaceLanternLight(lightObject.transform, anchor);
        if (lantern.GetComponent<Lantern>() == null)
        {
            lantern.AddComponent<Lantern>();
        }

        EnsureLanternHalo(lantern.transform);
        EnsureLanternFlame(lantern, anchor);
        StripColliders(pivot);
        EnsureChestFill(root);
    }

    static void AssignLanternMaterials(Transform lantern)
    {
        Material iron = AssetDatabase.LoadAssetAtPath<Material>(LanternMaterialFolder + "/LanternIron.mat");
        Material glass = EnsureLanternGlassMaterial();
        Renderer[] renderers = lantern.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            string name = renderers[i].name;
            if (name == "Iron" && iron != null)
            {
                renderers[i].sharedMaterial = iron;
            }
            else if (name == "Glass")
            {
                renderers[i].sharedMaterial = glass;
            }
        }
    }

    static Material EnsureLanternFlameMaterial()
    {
        Shader shader = Shader.Find("LanternKeeper/Flame");
        if (shader == null)
        {
            return null;
        }

        Material mat = FlameMat(LanternFlameMaterialPath, shader, 1.6f, 0f, 1.5f, 0.14f);
        AssetDatabase.SaveAssetIfDirty(mat);
        return mat;
    }

    // The flame quad, at FlameAnchor, with LanternFlame on the Lantern object.
    static void EnsureLanternFlame(GameObject lantern, Transform anchor)
    {
        Transform flame = lantern.transform.Find("Flame");
        if (flame == null)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Flame";
            quad.transform.SetParent(lantern.transform, false);
            flame = quad.transform;
        }

        Collider collider = flame.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        // Base of the full-height quad sits on the wick (FlameAnchor); Lantern-local up is gravity-up once LanternSway hangs it.
        flame.localPosition = lantern.transform.InverseTransformPoint(anchor.position) + Vector3.up * (LanternFlameSize.y * 0.5f);
        flame.localRotation = Quaternion.identity;
        flame.localScale = LanternFlameSize;
        MeshRenderer flameRenderer = flame.GetComponent<MeshRenderer>();
        flameRenderer.sharedMaterial = EnsureLanternFlameMaterial();
        flameRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        flameRenderer.receiveShadows = false;
        flameRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        flameRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        Renderer glass = null;
        Renderer[] renderers = lantern.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i].name == "Glass")
            {
                glass = renderers[i];
            }
        }

        LanternFlame flameComponent = lantern.GetComponent<LanternFlame>();
        if (flameComponent == null)
        {
            flameComponent = lantern.AddComponent<LanternFlame>();
        }

        SerializedObject so = new SerializedObject(flameComponent);
        so.FindProperty("lantern").objectReferenceValue = lantern.GetComponent<Lantern>();
        so.FindProperty("flame").objectReferenceValue = flame;
        so.FindProperty("flameRenderer").objectReferenceValue = flameRenderer;
        so.FindProperty("glassRenderer").objectReferenceValue = glass;
        so.FindProperty("baseScale").vector3Value = LanternFlameSize;
        so.FindProperty("basePosition").vector3Value = flame.localPosition;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void ConfigureLanternPoint(Light light)
    {
        light.type = LightType.Point;
        light.color = LookPalette.FromHex(LookPalette.LanternAmber);
        light.range = 14f;
        light.intensity = LanternMaxIntensity;
        light.shadows = LightShadows.None;
    }

    // Lantern.maxIntensity at full fuel. Range stays at the Lantern defaults (gameplay reveal radius).
    const float LanternMaxIntensity = 5.5f;
    const string LanternHaloTexturePath = "Assets/Game/Art/Look/LanternHalo.png";
    const string LanternHaloMaterialPath = "Assets/Game/Art/Look/LanternHalo.mat";

    static Material EnsureLanternHaloMaterial()
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(LanternHaloMaterialPath);
        if (existing != null)
        {
            return existing;
        }

        EnsureFolder("Assets/Game/Art/Look");
        if (!File.Exists(ProjectFile(LanternHaloTexturePath)))
        {
            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    // Gaussian bloom, faded to exactly zero at the quad edge so no square shows.
                    float fade = 1f - r * r;
                    // Colour drifts from the glow core toward deeper amber with distance, so the tail stays warm on a blue scene.
                    tex.SetPixel(x, y, new Color(1f, Mathf.Lerp(1f, 0.5f, r), Mathf.Lerp(1f, 0.14f, r), Mathf.Exp(-r * r * 3.5f) * fade * fade));
                }
            }

            File.WriteAllBytes(ProjectFile(LanternHaloTexturePath), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(LanternHaloTexturePath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(LanternHaloTexturePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        Shader shader = Shader.Find("LanternKeeper/AdditiveUnlit");
        Material mat = new Material(shader);
        Color core = LookPalette.FromHex(LookPalette.GlowCore);
        mat.SetColor("_BaseColor", new Color(core.r, core.g, core.b, 0.7f));
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(LanternHaloTexturePath));
        AssetDatabase.CreateAsset(mat, LanternHaloMaterialPath);
        return mat;
    }

    static void EnsureLanternHalo(Transform lanternObject)
    {
        Transform anchor = FindDeep(lanternObject, "FlameAnchor");
        if (anchor == null)
        {
            anchor = lanternObject;
        }

        Transform existing = FindDeep(lanternObject, "LanternHalo");
        GameObject halo;
        if (existing != null)
        {
            halo = existing.gameObject;
        }
        else
        {
            halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            halo.name = "LanternHalo";
            halo.transform.SetParent(lanternObject, false);
        }

        Collider collider = halo.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        halo.transform.position = anchor.position;
        halo.transform.localScale = Vector3.one * 0.9f;
        MeshRenderer renderer = halo.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = EnsureLanternHaloMaterial();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        if (halo.GetComponent<LanternHalo>() == null)
        {
            halo.AddComponent<LanternHalo>();
        }
    }

    static void PlaceLanternLight(Transform lightTransform, Transform anchor)
    {
        lightTransform.position = anchor.position;
    }

    static void EnsureChestFill(Transform root)
    {
        Transform chest = FindDeep(root, "Chest");
        Transform anchor = chest != null ? chest : root;
        Transform existing = FindDeep(root, "ChestFill");
        GameObject fillObject = existing != null ? existing.gameObject : new GameObject("ChestFill");
        if (existing == null)
        {
            fillObject.transform.SetParent(anchor, false);
        }

        Vector3 forward = Vector3.forward;
        Vector3 side = Vector3.right;
        if (chest != null)
        {
            forward = chest.forward;
            side = chest.right;
        }

        forward.y = 0f;
        side.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
        {
            forward = Vector3.forward;
        }

        if (side.sqrMagnitude < 0.01f)
        {
            side = Vector3.right;
        }

        Vector3 origin = chest != null ? chest.position : root.position + Vector3.up * 1.25f;
        // Outside the cloak, behind and to the left of the chest. The third-person camera
        // sits behind the keeper, so this is the cloth that otherwise stays unlit.
        fillObject.transform.position = origin - forward.normalized * 0.48f - side.normalized * 0.36f + Vector3.up * 0.12f;
        Light fill = fillObject.GetComponent<Light>();
        if (fill == null)
        {
            fill = fillObject.AddComponent<Light>();
        }

        fill.type = LightType.Point;
        fill.color = LookPalette.FromHex(LookPalette.LanternAmber);
        fill.range = 2.8f;
        fill.intensity = 1.15f;
        fill.shadows = LightShadows.None;
    }

    [MenuItem("Lantern Keeper/Refresh Keeper Look")]
    public static void RefreshKeeperLook()
    {
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("Exit Play mode before refreshing the keeper.");
            return;
        }

        keeperImportReady = false;
        PrepareKeeperImport();
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(KeeperFbxPath);
        if (model == null)
        {
            Debug.LogError("Keeper model is missing.");
            return;
        }

        GameObject temp = (GameObject)PrefabUtility.InstantiatePrefab(model);
        TintKeeper(temp);
        Object.DestroyImmediate(temp);

        GameObject contents = PrefabUtility.LoadPrefabContents(KeeperPrefabPath);
        try
        {
            Animator animator = contents.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(KeeperControllerPath);
            }

            Transform lanternLight = FindDeep(contents.transform, "LanternLight");
            if (lanternLight != null)
            {
                Transform refreshAnchor = FindDeep(contents.transform, "FlameAnchor");
                if (refreshAnchor != null)
                {
                    PlaceLanternLight(lanternLight, refreshAnchor);
                }

                Light point = lanternLight.GetComponent<Light>();
                if (point != null)
                {
                    ConfigureLanternPoint(point);
                }

                Lantern lanternComponent = lanternLight.GetComponentInParent<Lantern>();
                if (lanternComponent != null)
                {
                    SerializedObject lanternSo = new SerializedObject(lanternComponent);
                    SerializedProperty maxIntensity = lanternSo.FindProperty("maxIntensity");
                    maxIntensity.floatValue = LanternMaxIntensity;
                    lanternSo.ApplyModifiedPropertiesWithoutUndo();
                    EnsureLanternHalo(lanternComponent.transform);
                }
            }

            EnsureChestFill(contents.transform);
            PrefabUtility.SaveAsPrefabAsset(contents, KeeperPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Refreshed keeper materials and lantern lights on " + KeeperPrefabPath);
    }

    static void AttachDust(ArtKit art, Transform root)
    {
        GameObject dust = new GameObject("Dust");
        dust.transform.SetParent(root, false);
        dust.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        MakeParticles(dust, art.unlit, 0.55f, 0.35f, 0.35f, new Color(0.75f, 0.75f, 0.7f, 0.22f), 0f, 0, true, true, 0.2f);
    }

    static Texture2D PolyOrNoise(string fileName, Color fallback, int seed)
    {
        Texture2D loaded = LoadPoly(fileName, false);
        if (loaded != null)
        {
            return loaded;
        }

        Debug.LogWarning("Poly Haven texture missing, using generated fallback: " + fileName);
        return Noise("Assets/Game/Textures/PolyHaven/Fallback_" + fileName.Replace(".jpg", ".png"), fallback, 0.16f, 256, seed);
    }

    static Texture2D PolyOrNormal(string fileName, int seed)
    {
        Texture2D loaded = LoadPoly(fileName, true);
        if (loaded != null)
        {
            return loaded;
        }

        Debug.LogWarning("Poly Haven normal missing, using generated fallback: " + fileName);
        return NormalMap("Assets/Game/Textures/PolyHaven/Fallback_" + fileName.Replace(".jpg", ".png"), 256, 2.4f, seed);
    }

    static Texture2D LoadPoly(string fileName, bool normal)
    {
        string assetPath = "Assets/Game/Textures/PolyHaven/" + fileName;
        if (!File.Exists(ProjectFile(assetPath)))
        {
            return null;
        }

        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024;
            importer.isReadable = !normal;
            importer.convertToNormalmap = false;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    static Texture2D SmoothnessMask(string roughFile, string maskAsset, int seed)
    {
        Texture2D rough = LoadPoly(roughFile, false);
        if (rough == null)
        {
            return null;
        }

        TextureImporter importer = AssetImporter.GetAtPath("Assets/Game/Textures/PolyHaven/" + roughFile) as TextureImporter;
        if (importer != null && !importer.isReadable)
        {
            importer.isReadable = true;
            importer.SaveAndReimport();
            rough = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Textures/PolyHaven/" + roughFile);
        }

        Color[] source;
        try
        {
            source = rough.GetPixels();
        }
        catch
        {
            Debug.LogWarning("Could not read roughness pixels for " + roughFile);
            return null;
        }

        Texture2D mask = new Texture2D(rough.width, rough.height, TextureFormat.RGBA32, false, true);
        Color[] pixels = new Color[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            float smoothness = Mathf.Clamp01(1f - source[i].grayscale);
            pixels[i] = new Color(0f, 0.9f, 0.4f, smoothness);
        }

        mask.SetPixels(pixels);
        mask.Apply();
        File.WriteAllBytes(ProjectFile(maskAsset), mask.EncodeToPNG());
        Object.DestroyImmediate(mask);
        AssetDatabase.ImportAsset(maskAsset, ImportAssetOptions.ForceUpdate);
        TextureImporter maskImporter = AssetImporter.GetAtPath(maskAsset) as TextureImporter;
        if (maskImporter != null)
        {
            maskImporter.sRGBTexture = false;
            maskImporter.wrapMode = TextureWrapMode.Repeat;
            maskImporter.textureType = TextureImporterType.Default;
            maskImporter.alphaSource = TextureImporterAlphaSource.FromInput;
            maskImporter.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(maskAsset);
    }

    static string ProjectFile(string assetPath)
    {
        return Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    static Material FlameMat(string path, Shader shader, float speed, float roll, float intensity, float distort)
    {
        Material mat = UnlitMat(path, shader, Color.white);
        mat.SetFloat("_Speed", speed);
        mat.SetFloat("_Roll", roll);
        mat.SetFloat("_Intensity", intensity);
        mat.SetFloat("_Distort", distort);
        mat.SetColor("_Core", new Color(1f, 0.96f, 0.7f, 1f));
        mat.SetColor("_Mid", new Color(1f, 0.42f, 0.06f, 1f));
        mat.SetColor("_Tip", new Color(0.55f, 0.04f, 0.01f, 0f));
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Texture2D ReedBlade(string path)
    {
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1);
                float v = y / (float)(size - 1);
                float half = Mathf.Lerp(0.08f, 0.015f, v);
                bool on = v > 0.02f && v < 0.98f && Mathf.Abs(u - 0.5f) < half;
                Color color = new Color(0.32f, 0.4f, 0.16f, on ? 1f : 0f);
                tex.SetPixel(x, y, color);
            }
        }

        tex.Apply();
        return SaveTexture(path, tex, false, false, false);
    }

    static GameObject BuildPebbles(ArtKit art)
    {
        GameObject root = new GameObject("PebbleCluster");
        Prim(PrimitiveType.Sphere, "A", root.transform, new Vector3(0f, 0.04f, 0f), new Vector3(0.16f, 0.08f, 0.14f), art.rock);
        Prim(PrimitiveType.Sphere, "B", root.transform, new Vector3(0.12f, 0.03f, 0.06f), new Vector3(0.1f, 0.06f, 0.09f), art.rock);
        Prim(PrimitiveType.Sphere, "C", root.transform, new Vector3(-0.08f, 0.03f, -0.05f), new Vector3(0.08f, 0.05f, 0.07f), art.beaconStone);
        StripColliders(root);
        return root;
    }

    static GameObject BuildLog(ArtKit art)
    {
        GameObject root = new GameObject("FallenLog");
        Prim(PrimitiveType.Capsule, "Log", root.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.16f, 0.55f, 0.16f), art.wood, Quaternion.Euler(0f, 20f, 90f));
        return root;
    }

    static GameObject BuildStump(ArtKit art)
    {
        GameObject root = new GameObject("Stump");
        Prim(PrimitiveType.Cylinder, "Stump", root.transform, new Vector3(0f, 0.14f, 0f), new Vector3(0.32f, 0.14f, 0.32f), art.wood);
        return root;
    }
}
}
