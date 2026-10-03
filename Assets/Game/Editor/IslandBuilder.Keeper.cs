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

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(KeeperControllerPath) != null)
        {
            AssetDatabase.DeleteAsset(KeeperControllerPath);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(KeeperControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
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
        EditorUtility.SetDirty(controller);
        return controller;
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

    static void TintKeeper(GameObject root)
    {
        List<Texture2D> albedoMaps = new List<Texture2D>();
        List<Texture2D> normalMaps = new List<Texture2D>();
        CollectKeeperMaps(albedoMaps, normalMaps);
        Debug.Log("Keeper texture library: albedo=" + albedoMaps.Count + " normal=" + normalMaps.Count + ".");
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] source = renderers[i].sharedMaterials;
            Material[] tinted = new Material[source.Length];
            List<string> missing = new List<string>();
            for (int m = 0; m < source.Length; m++)
            {
                if (source[m] == null)
                {
                    continue;
                }

                string path = "Assets/Game/Materials/Generated/Keeper_" + Sanitize(renderers[i].gameObject.name) + "_" + m + ".mat";
                Material copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (copy == null)
                {
                    copy = new Material(source[m]);
                    AssetDatabase.CreateAsset(copy, path);
                }
                else
                {
                    copy.shader = source[m].shader;
                    copy.CopyPropertiesFromMaterial(source[m]);
                }

                string assigned = AssignKeeperMaps(copy, source[m], renderers[i].gameObject.name, albedoMaps, normalMaps);
                WarmNight(copy, renderers[i].gameObject.name, source[m].name);
                EditorUtility.SetDirty(copy);
                tinted[m] = copy;
                if (!MaterialHasAlbedo(copy))
                {
                    missing.Add(source[m].name);
                }
                else if (assigned.Length > 0)
                {
                    Debug.Log("Keeper " + renderers[i].gameObject.name + " " + source[m].name + " " + assigned);
                }
            }

            renderers[i].sharedMaterials = tinted;
            if (missing.Count > 0)
            {
                Debug.LogWarning("Keeper renderer " + renderers[i].gameObject.name + " has no albedo on " + string.Join(", ", missing.ToArray()) + ". Using a flat colour fallback.");
            }
        }
    }

    static void CollectKeeperMaps(List<Texture2D> albedoMaps, List<Texture2D> normalMaps)
    {
        string[] folders = { "Assets/Game/Models/Keeper" };
        for (int f = 0; f < folders.Length; f++)
        {
            if (!AssetDatabase.IsValidFolder(folders[f]))
            {
                continue;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folders[f] });
            for (int i = 0; i < guids.Length; i++)
            {
                AddKeeperMap(AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guids[i])), albedoMaps, normalMaps);
            }
        }

        Object[] embedded = AssetDatabase.LoadAllAssetsAtPath(KeeperFbxPath);
        for (int i = 0; i < embedded.Length; i++)
        {
            AddKeeperMap(embedded[i] as Texture2D, albedoMaps, normalMaps);
        }
    }

    static void AddKeeperMap(Texture2D texture, List<Texture2D> albedoMaps, List<Texture2D> normalMaps)
    {
        if (texture == null || albedoMaps.Contains(texture) || normalMaps.Contains(texture))
        {
            return;
        }

        if (IsKeeperNormalName(texture.name))
        {
            normalMaps.Add(texture);
        }
        else
        {
            albedoMaps.Add(texture);
        }
    }

    static bool IsKeeperNormalName(string textureName)
    {
        string lower = textureName.ToLowerInvariant();
        return lower.Contains("normal") || lower.Contains("nrm") || lower.Contains("bump") || lower.EndsWith("_n");
    }

    static string AssignKeeperMaps(Material copy, Material source, string rendererName, List<Texture2D> albedoMaps, List<Texture2D> normalMaps)
    {
        Texture albedo = TextureOn(source, "_BaseMap");
        if (albedo == null)
        {
            albedo = TextureOn(source, "_MainTex");
        }

        Texture normal = TextureOn(source, "_BumpMap");
        if (albedo == null)
        {
            albedo = PickKeeperMap(albedoMaps, source.name, rendererName);
        }

        if (normal == null)
        {
            normal = PickKeeperMap(normalMaps, source.name, rendererName);
        }

        string report = "";
        if (albedo != null && copy.HasProperty("_BaseMap"))
        {
            copy.SetTexture("_BaseMap", albedo);
            if (copy.HasProperty("_MainTex"))
            {
                copy.SetTexture("_MainTex", albedo);
            }

            report = "albedo=" + albedo.name;
        }

        if (normal != null && copy.HasProperty("_BumpMap"))
        {
            copy.SetTexture("_BumpMap", normal);
            if (copy.HasProperty("_BumpScale"))
            {
                copy.SetFloat("_BumpScale", 1f);
            }

            copy.EnableKeyword("_NORMALMAP");
            report += (report.Length > 0 ? " " : "") + "normal=" + normal.name;
        }

        return report;
    }

    static Texture TextureOn(Material material, string property)
    {
        if (material == null || !material.HasProperty(property))
        {
            return null;
        }

        return material.GetTexture(property);
    }

    static Texture2D PickKeeperMap(List<Texture2D> maps, string materialName, string rendererName)
    {
        if (maps.Count == 0)
        {
            return null;
        }

        string material = materialName.ToLowerInvariant();
        string renderer = rendererName.ToLowerInvariant();
        for (int i = 0; i < maps.Count; i++)
        {
            string name = maps[i].name.ToLowerInvariant();
            if (material.Length > 0 && name.Contains(material))
            {
                return maps[i];
            }
        }

        for (int i = 0; i < maps.Count; i++)
        {
            string name = maps[i].name.ToLowerInvariant();
            if (renderer.Length > 0 && name.Contains(renderer))
            {
                return maps[i];
            }
        }

        if (maps.Count == 1)
        {
            return maps[0];
        }

        return null;
    }

    static bool MaterialHasAlbedo(Material material)
    {
        return material != null && material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null;
    }

    static void WarmNight(Material material, string rendererName, string sourceName)
    {
        string property = material.HasProperty("_BaseColor") ? "_BaseColor" : material.HasProperty("_Color") ? "_Color" : "";
        if (property.Length == 0)
        {
            return;
        }

        string name = rendererName.ToLowerInvariant();
        string slot = sourceName == null ? "" : sourceName.ToLowerInvariant();
        Color color = material.GetColor(property);
        bool cloth = false;
        if (MaterialHasAlbedo(material))
        {
            if (!IsNearWhite(color))
            {
                color = Color.white;
            }
        }
        else if (name.Contains("head"))
        {
            color = Color.Lerp(color, new Color(0.64f, 0.46f, 0.35f), 0.62f);
        }
        else if (name.Contains("feet"))
        {
            color = slot.Contains("dark") ? new Color(0.3f, 0.17f, 0.1f) : new Color(0.4f, 0.24f, 0.14f);
        }
        else if (name.Contains("leg"))
        {
            color = new Color(0.22f, 0.2f, 0.22f);
        }
        else if (slot.Contains("skin"))
        {
            color = new Color(0.64f, 0.46f, 0.35f);
        }
        else if (slot.Contains("gold"))
        {
            cloth = true;
            color = new Color(0.5f, 0.44f, 0.38f);
        }
        else if (slot.Contains("metal"))
        {
            color = new Color(0.4f, 0.4f, 0.42f);
        }
        else if (slot.Contains("white"))
        {
            color = new Color(0.68f, 0.62f, 0.54f);
        }
        else if (slot.Contains("light"))
        {
            cloth = true;
            color = new Color(0.46f, 0.4f, 0.36f);
        }
        else if (slot.Contains("dark") || slot == "brown")
        {
            cloth = true;
            color = new Color(0.38f, 0.34f, 0.3f);
        }
        else
        {
            cloth = true;
            color = new Color(0.44f, 0.39f, 0.34f);
        }

        color.a = 1f;
        ApplyKeeperLit(material, cloth);
        if (material.HasProperty(property))
        {
            material.SetColor(property, color);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", name.Contains("head") ? 0.28f : 0.18f);
        }
    }

    static void ApplyKeeperLit(Material material, bool cloth)
    {
        Shader shader = Shader.Find("LanternKeeper/KeeperLit");
        if (shader == null)
        {
            Debug.LogError("LanternKeeper/KeeperLit is missing. Keeper kept its previous shader.");
            return;
        }

        Texture albedo = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
        Texture normal = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;
        material.shader = shader;
        if (albedo != null)
        {
            material.SetTexture("_BaseMap", albedo);
        }

        if (normal != null)
        {
            material.SetTexture("_BumpMap", normal);
        }

        material.SetFloat("_Desaturate", cloth ? 0.28f : 0f);
        material.SetColor("_RimColor", new Color(0.58f, 0.72f, 0.95f, 1f));
        material.SetFloat("_RimPower", 2.4f);
        material.SetFloat("_RimStrength", 0.7f);
    }

    static bool IsNearWhite(Color color)
    {
        return color.r >= 0.82f && color.g >= 0.82f && color.b >= 0.82f;
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

    static void AttachLantern(ArtKit art, Transform root, Transform hand)
    {
        GameObject pivot = new GameObject("LanternPivot");
        pivot.transform.SetParent(hand, false);
        Vector3 lossy = hand.lossyScale;
        pivot.transform.localScale = new Vector3(1f / Mathf.Max(0.0001f, lossy.x), 1f / Mathf.Max(0.0001f, lossy.y), 1f / Mathf.Max(0.0001f, lossy.z));
        // The offset is meant in metres, so convert it into the hand's scaled space like the scale above.
        // The imported armature is scaled ~93x; without this the lantern ends up ~9 m from the keeper.
        pivot.transform.localPosition = new Vector3(
            0.02f / Mathf.Max(0.0001f, lossy.x),
            -0.05f / Mathf.Max(0.0001f, lossy.y),
            0.08f / Mathf.Max(0.0001f, lossy.z));
        GameObject lantern = Prim(PrimitiveType.Cube, "Lantern", pivot.transform, Vector3.zero, new Vector3(0.12f, 0.16f, 0.12f), art.lanternMat);
        Prim(PrimitiveType.Cube, "Cap", lantern.transform, new Vector3(0f, 0.62f, 0f), new Vector3(0.7f, 0.18f, 0.7f), art.cloak);
        GameObject lightObject = new GameObject("LanternLight");
        lightObject.transform.SetParent(lantern.transform, false);
        Light light = lightObject.AddComponent<Light>();
        ConfigureLanternPoint(light);
        PlaceLanternLight(lightObject.transform, hand, root);
        lantern.AddComponent<Lantern>();
        EnsureLanternHalo(lantern.transform);
        StripColliders(pivot);
        EnsureChestFill(root);
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
        Transform pivot = lanternObject.parent != null ? lanternObject.parent : lanternObject;
        Transform existing = FindDeep(pivot, "LanternHalo");
        GameObject halo;
        if (existing != null)
        {
            halo = existing.gameObject;
        }
        else
        {
            halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            halo.name = "LanternHalo";
            halo.transform.SetParent(pivot, false);
        }

        Collider collider = halo.GetComponent<Collider>();
        if (collider != null)
        {
            Object.DestroyImmediate(collider);
        }

        halo.transform.localPosition = lanternObject.localPosition;
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

    static void PlaceLanternLight(Transform lightTransform, Transform hand, Transform root)
    {
        Vector3 outward = hand.position - root.position;
        outward.y = 0.12f;
        if (outward.sqrMagnitude < 0.0004f)
        {
            outward = Vector3.right;
        }

        lightTransform.position = hand.position + outward.normalized * 0.32f + Vector3.up * 0.05f;
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

            Transform hand = FindDeep(contents.transform, "Wrist.R");
            if (hand == null)
            {
                hand = FindDeep(contents.transform, "RightHand");
            }

            if (hand == null)
            {
                hand = contents.transform;
            }

            Transform lanternLight = FindDeep(contents.transform, "LanternLight");
            if (lanternLight != null)
            {
                PlaceLanternLight(lanternLight, hand, contents.transform);
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
