using System.Collections.Generic;
using UnityEngine;

namespace LanternKeeper
{
public class KeeperAnimator : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] string speedParameter = "Speed";
    [SerializeField] string groundedParameter = "Grounded";
    [SerializeField] string verticalSpeedParameter = "VerticalSpeed";
    [SerializeField] string jumpParameter = "Jump";
    [SerializeField] float speedDampTime = 0.1f;
    [SerializeField] float landDuration = 0.12f;
    [SerializeField] float landSquash = 0.92f;

    static readonly int SwayId = Shader.PropertyToID("_LKKeeperSway");
    static readonly int BackId = Shader.PropertyToID("_LKKeeperBack");
    const float RunSpeed = 9f;

    const float ArmEaseSeconds = 0.2f;
    const float MaxFacingDegrees = 25f;
    const float MinFacingWeight = 0.05f;
    const float StaggerKickDegrees = 14f;

    // Eased wind lean weight (0..1); it drives the Lean layer, the cloak push and the facing.
    float leanWeight = 0f;
    float armWeight = 1f;
    float armWeightTarget = 1f;
    float armHoldRemaining;
    int gestureFrame = -100;
    bool dead;
    int armLayer = -1;
    int leanLayer = -1;
    int actionsLayer = -1;
    int upperLayer = -1;
    int staggerHash;
    int lightHash;
    int dieHash;
    int shakeOffHash;
    int deadHash;
    bool hasActions;
    float interactLength = 1.5833f;
    Transform visualRoot;
    Quaternion visualBase = Quaternion.identity;
    LanternSway sway;
    GameManager subscribedManager;
    readonly List<WaterHazard> subscribedHazards = new List<WaterHazard>();
    bool staticSubscribed;

    PlayerController player;
    Transform head;
    Transform spine;
    Transform legL;
    Transform legR;
    Transform footL;
    Transform footR;
    ParticleSystem footDust;
    Transform armL;
    Transform armR;
    Transform bob;
    Transform squashBone;
    Vector3 squashBaseScale;
    float lookTimer;
    float lookYaw;
    float lookTarget;
    float lean;
    float lastYaw;
    float phase;
    float landTimer;
    float takeoffTimer;
    bool procedural;
    DrivenBone spineDrive;
    DrivenBone headDrive;
    DrivenBone armLDrive;
    DrivenBone armRDrive;
    DrivenBone legLDrive;
    DrivenBone legRDrive;
    bool hasSpeed;
    bool hasGrounded;
    bool hasVerticalSpeed;
    bool hasJump;
    int speedHash;
    int groundedHash;
    int verticalHash;
    int jumpHash;

    public float LeanWeight => leanWeight;
    public float ArmWeight => armWeight;
    public float PoseScaleY { get; private set; } = 1f;
    public bool JumpPoseActive => takeoffTimer > 0f;
    public float LandTimer => landTimer;

    void Awake()
    {
        player = GetComponent<PlayerController>();
        if (player == null)
        {
            player = GetComponentInParent<PlayerController>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        procedural = animator == null || animator.runtimeAnimatorController == null;
        CacheBones();
        CacheParameters();
        CacheLayers();
        lastYaw = transform.eulerAngles.y;
        lookTimer = 2.5f;
        PoseScaleY = 1f;
    }

    void CacheBones()
    {
        if (animator != null && animator.isHuman)
        {
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            legL = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            legR = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            armL = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            armR = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        }

        if (head == null)
        {
            head = FindNamed(transform, "Head");
        }

        if (spine == null)
        {
            spine = FindNamed(transform, "Abdomen");
            if (spine == null)
            {
                spine = FindNamed(transform, "Torso");
            }
        }

        if (legL == null)
        {
            legL = FindFirst(transform, "Leg.L", "UpperLeg.L", "LeftUpperLeg");
        }

        if (legR == null)
        {
            legR = FindFirst(transform, "Leg.R", "UpperLeg.R", "RightUpperLeg");
        }

        if (armL == null)
        {
            armL = FindFirst(transform, "Arm.L", "UpperArm.L", "LeftUpperArm");
        }

        if (armR == null)
        {
            armR = FindFirst(transform, "Arm.R", "UpperArm.R", "RightUpperArm");
        }

        footL = FindNamed(transform, "Foot.L");
        footR = FindNamed(transform, "Foot.R");
        bob = FindNamed(transform, "Body");
        if (FindNamed(transform, "Leg.L") != null)
        {
            procedural = true;
        }

        squashBone = bob != null ? bob : spine;
        if (squashBone == transform)
        {
            squashBone = null;
        }

        if (squashBone != null)
        {
            squashBaseScale = squashBone.localScale;
        }

        spineDrive.bone = spine;
        headDrive.bone = head;
        armLDrive.bone = armL;
        armRDrive.bone = armR;
        legLDrive.bone = legL;
        legRDrive.bone = legR;
        ClearDrives();
    }

    void OnEnable()
    {
        SubscribeStatic();
        SubscribeScene();
    }

    void Start()
    {
        // GameManager and the hazards may not exist yet in OnEnable, so subscribe again here.
        SubscribeScene();
    }

    void OnDisable()
    {
        ClearDrives();
        UnsubscribeAll();
        Shader.SetGlobalVector(SwayId, new Vector4(0f, 0f, 0f, 1f));
        Shader.SetGlobalVector(BackId, Vector4.zero);
    }

    void SubscribeStatic()
    {
        if (staticSubscribed)
        {
            return;
        }

        staticSubscribed = true;
        Shade.Stole += OnStole;
        Beacon.Lit += OnBeaconLit;
    }

    void SubscribeScene()
    {
        GameManager manager = GameManager.Instance;
        if (manager != null && manager != subscribedManager)
        {
            if (subscribedManager != null)
            {
                subscribedManager.DeathStarted -= OnDeathStarted;
            }

            subscribedManager = manager;
            manager.DeathStarted += OnDeathStarted;
        }

        WaterHazard[] hazards = FindObjectsByType<WaterHazard>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < hazards.Length; i++)
        {
            if (hazards[i] != null && !subscribedHazards.Contains(hazards[i]))
            {
                subscribedHazards.Add(hazards[i]);
                hazards[i].Rescued += OnRescued;
            }
        }
    }

    void UnsubscribeAll()
    {
        if (staticSubscribed)
        {
            staticSubscribed = false;
            Shade.Stole -= OnStole;
            Beacon.Lit -= OnBeaconLit;
        }

        if (subscribedManager != null)
        {
            subscribedManager.DeathStarted -= OnDeathStarted;
        }

        subscribedManager = null;
        for (int i = 0; i < subscribedHazards.Count; i++)
        {
            if (subscribedHazards[i] != null)
            {
                subscribedHazards[i].Rescued -= OnRescued;
            }
        }

        subscribedHazards.Clear();
    }

    void OnStole(float amount)
    {
        Trigger(staggerHash);
        if (dead)
        {
            return;
        }

        PlayCueHere(SoundCues.KeeperHit);

        if (sway == null)
        {
            sway = GetComponentInChildren<LanternSway>(true);
        }

        if (sway != null)
        {
            sway.Kick(StaggerKickDegrees);
        }
    }

    void OnBeaconLit(Beacon beacon)
    {
        if (dead)
        {
            return;
        }

        Trigger(lightHash);
        PlayCueHere(SoundCues.KeeperInteract);
        armHoldRemaining = interactLength * 0.8f;
        gestureFrame = Time.frameCount;
    }

    void OnDeathStarted()
    {
        dead = true;
        if (hasActions)
        {
            animator.SetBool(deadHash, true);
        }

        Trigger(dieHash);
    }

    void OnRescued()
    {
        Trigger(shakeOffHash);
    }

    void PlayCueHere(string cue)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayCue(cue, transform.position);
        }
    }

    void Trigger(int hash)
    {
        if (hasActions && animator != null)
        {
            animator.SetTrigger(hash);
        }
    }

    void CacheLayers()
    {
        hasActions = false;
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        armLayer = animator.GetLayerIndex("LanternArm");
        leanLayer = animator.GetLayerIndex("Lean");
        actionsLayer = animator.GetLayerIndex("Actions");
        upperLayer = animator.GetLayerIndex("UpperActions");
        hasActions = actionsLayer >= 0 && upperLayer >= 0 && HasParameter("Stagger") && HasParameter("Light") && HasParameter("Die") && HasParameter("ShakeOff") && HasParameter("Dead");
        staggerHash = Animator.StringToHash("Stagger");
        lightHash = Animator.StringToHash("Light");
        dieHash = Animator.StringToHash("Die");
        shakeOffHash = Animator.StringToHash("ShakeOff");
        deadHash = Animator.StringToHash("Dead");
        visualRoot = FindNamed(transform, "CharacterArmature");
        if (visualRoot != null)
        {
            visualBase = visualRoot.localRotation;
        }

        AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null && clips[i].name.EndsWith("Interact"))
            {
                interactLength = clips[i].length;
            }
        }
    }

    void UpdateWeights()
    {
        float dt = Time.deltaTime;
        Wind wind = Wind.Instance;
        float target = 0f;
        if (wind != null && !dead)
        {
            target = KeeperLean.TargetWeight(wind.Phase, wind.Strength01, wind.Sheltered, true);
        }

        leanWeight = KeeperLean.Step(leanWeight, target, dt);

        if (armHoldRemaining > 0f)
        {
            armHoldRemaining -= dt;
        }

        // An interrupted gesture must not leave the arm stuck down: with UpperActions idle, the arm is released.
        bool actionsIdle = hasActions && !animator.IsInTransition(upperLayer) && animator.GetCurrentAnimatorStateInfo(upperLayer).IsName("Empty");
        if (actionsIdle && Time.frameCount > gestureFrame + 2)
        {
            armHoldRemaining = 0f;
        }

        armWeightTarget = dead || armHoldRemaining > 0f ? 0f : 1f;
        if (dt > 0f)
        {
            armWeight = Mathf.MoveTowards(armWeight, armWeightTarget, dt / ArmEaseSeconds);
        }

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            if (armLayer >= 0)
            {
                animator.SetLayerWeight(armLayer, armWeight);
            }

            if (leanLayer >= 0)
            {
                animator.SetLayerWeight(leanLayer, leanWeight);
            }

            // Belt and braces with the Dead transition: the reach layer never shows over the Death clip.
            if (upperLayer >= 0)
            {
                animator.SetLayerWeight(upperLayer, dead ? 0f : 1f);
            }
        }
    }

    // Turns the visual model (not the CharacterController root) up to 25 degrees toward the gust. Visual only.
    void ApplyFacing()
    {
        if (visualRoot == null)
        {
            return;
        }

        float offset = 0f;
        Wind wind = Wind.Instance;
        if (wind != null && leanWeight > MinFacingWeight)
        {
            Vector3 into = -wind.Direction;
            into.y = 0f;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (into.sqrMagnitude > 0.0001f && forward.sqrMagnitude > 0.0001f)
            {
                float delta = Vector3.SignedAngle(forward, into, Vector3.up);
                offset = Mathf.Clamp(delta, -MaxFacingDegrees, MaxFacingDegrees) * leanWeight;
            }
        }

        // The offset is a world-space yaw, so the armature's own import rotation can't flip its sign.
        Transform parent = visualRoot.parent;
        Quaternion parentRotation = parent != null ? parent.rotation : Quaternion.identity;
        visualRoot.localRotation = Quaternion.Inverse(parentRotation) * Quaternion.AngleAxis(offset, Vector3.up) * parentRotation * visualBase;
    }

    void ClearDrives()
    {
        spineDrive.tracked = false;
        headDrive.tracked = false;
        armLDrive.tracked = false;
        armRDrive.tracked = false;
        legLDrive.tracked = false;
        legRDrive.tracked = false;
    }

    void CacheParameters()
    {
        speedHash = Animator.StringToHash(speedParameter);
        groundedHash = Animator.StringToHash(groundedParameter);
        verticalHash = Animator.StringToHash(verticalSpeedParameter);
        jumpHash = Animator.StringToHash(jumpParameter);
        hasSpeed = HasParameter(speedParameter);
        hasGrounded = HasParameter(groundedParameter);
        hasVerticalSpeed = HasParameter(verticalSpeedParameter);
        hasJump = HasParameter(jumpParameter);
    }

    bool HasParameter(string parameterName)
    {
        if (animator == null || animator.runtimeAnimatorController == null || string.IsNullOrEmpty(parameterName))
        {
            return false;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    void Update()
    {
        float speed = player != null ? player.CurrentSpeed : 0f;
        bool sprint = player != null && player.IsSprinting && speed > 0.4f;
        bool grounded = player == null || player.IsGrounded;
        float vertical = player != null ? player.VerticalVelocity : 0f;

        if (player != null && player.JumpedThisFrame)
        {
            takeoffTimer = 0.16f;
            if (hasJump)
            {
                animator.SetTrigger(jumpHash);
            }
        }

        if (player != null && player.LandedThisFrame)
        {
            landTimer = landDuration;
        }

        if (takeoffTimer > 0f)
        {
            takeoffTimer -= Time.deltaTime;
        }

        UpdateWeights();

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            if (hasSpeed)
            {
                animator.SetFloat(speedHash, speed, speedDampTime, Time.deltaTime);
            }

            if (hasGrounded)
            {
                animator.SetBool(groundedHash, grounded);
            }

            if (hasVerticalSpeed)
            {
                animator.SetFloat(verticalHash, vertical);
            }
        }

        bool idle = speed < 0.25f;
        lookTimer -= Time.deltaTime;
        if (idle && lookTimer <= 0f)
        {
            lookTarget = Mathf.Abs(lookTarget) < 1f ? Random.Range(-32f, 32f) : 0f;
            lookTimer = Random.Range(2.4f, 4.6f);
        }

        if (!idle)
        {
            lookTarget = 0f;
        }

        lookYaw = Mathf.Lerp(lookYaw, lookTarget, Time.deltaTime * 2.4f);

        float yaw = transform.eulerAngles.y;
        float yawRate = Mathf.DeltaAngle(lastYaw, yaw) / Mathf.Max(Time.deltaTime, 0.0001f);
        lastYaw = yaw;
        if (Mathf.Abs(yawRate) < 15f)
        {
            yawRate = 0f;
        }

        float targetLean = Mathf.Clamp(-yawRate * 0.45f, -12f, 12f);
        if (speed < 0.3f)
        {
            targetLean = 0f;
            lean = 0f;
        }
        else
        {
            lean = Mathf.Lerp(lean, targetLean, Time.deltaTime * 6f);
        }

        lean = Mathf.Clamp(lean, -12f, 12f);

        if (procedural)
        {
            float rate = sprint ? 11f : 7.2f;
            phase += speed * rate * Time.deltaTime;
            float swing = speed < 0.2f ? 0f : Mathf.Sin(phase) * (sprint ? 34f : 24f);
            float bobAmount = speed < 0.2f ? 0f : Mathf.Abs(Mathf.Sin(phase)) * (sprint ? 0.06f : 0.035f);
            Swing(legL, swing);
            Swing(legR, -swing);
            Swing(armL, -swing * 0.75f);
            Swing(armR, swing * 0.75f);
            if (bob != null)
            {
                Vector3 local = bob.localPosition;
                local.y = 0.92f + bobAmount;
                bob.localPosition = local;
            }
        }
    }

    void LateUpdate()
    {
        float airLean = 0f;
        float tuck = 0f;
        if (player != null && !player.IsGrounded)
        {
            if (player.VerticalVelocity > 0.4f || takeoffTimer > 0f)
            {
                airLean = 10f;
                tuck = 22f;
            }
            else
            {
                airLean = 16f;
                tuck = 8f;
            }
        }

        ApplyDrive(ref spineDrive, LimitLean(Quaternion.Euler(lean * 0.15f + airLean, 0f, lean)));
        float headYaw = Mathf.Abs(lookYaw) > 0.2f ? lookYaw : 0f;
        ApplyDrive(ref headDrive, Quaternion.Euler(0f, headYaw, 0f));

        if (!procedural)
        {
            float armPitch = tuck > 0.1f ? -tuck : 0f;
            float legPitch = tuck > 0.1f ? tuck * 0.65f : 0f;
            ApplyDrive(ref armLDrive, Quaternion.Euler(armPitch, 0f, 0f));
            ApplyDrive(ref armRDrive, Quaternion.Euler(armPitch, 0f, 0f));
            ApplyDrive(ref legLDrive, Quaternion.Euler(legPitch, 0f, 0f));
            ApplyDrive(ref legRDrive, Quaternion.Euler(legPitch, 0f, 0f));
        }

        ApplySquash();
        ApplyFacing();
        WriteSway();
    }

    void WriteSway()
    {
        float planar = player != null ? player.CurrentSpeed : 0f;
        float speed01 = Mathf.Clamp01(planar / RunSpeed);
        float wind01 = Mathf.Clamp01(leanWeight);
        Vector2 direction = new Vector2(0f, 1f);
        if (Wind.Instance != null)
        {
            Vector3 world = Wind.Instance.Direction;
            direction = new Vector2(world.x, world.z);
        }

        Shader.SetGlobalVector(SwayId, new Vector4(speed01, wind01, direction.x, direction.y));
        Vector3 back = -transform.forward;
        back.y = 0f;
        back = back.sqrMagnitude > 0.0001f ? back.normalized : Vector3.zero;
        Shader.SetGlobalVector(BackId, new Vector4(back.x, back.y, back.z, 0f));
    }

    void ApplySquash()
    {
        PoseScaleY = 1f;
        if (squashBone == null)
        {
            return;
        }

        if (landTimer > 0f)
        {
            float u = Mathf.Clamp01(landTimer / Mathf.Max(0.01f, landDuration));
            PoseScaleY = Mathf.Lerp(1f, landSquash, Mathf.Sin(u * Mathf.PI));
            landTimer -= Time.deltaTime;
        }
        else if (player != null && !player.IsGrounded)
        {
            PoseScaleY = player.VerticalVelocity > 0.4f ? 1.06f : 1.04f;
        }

        Vector3 scale = squashBaseScale;
        scale.y *= PoseScaleY;
        squashBone.localScale = scale;
    }

    public void OnFootstep()
    {
        EmitFootDust();
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFootstep(transform.position);
        }
    }

    // A small puff at whichever foot is lower; the Dust system simulates in world space.
    public void EmitFootDust()
    {
        if (footDust == null)
        {
            Transform root = player != null ? player.transform : transform;
            Transform dustTransform = root.Find("Dust");
            if (dustTransform != null)
            {
                footDust = dustTransform.GetComponent<ParticleSystem>();
            }

            if (footDust == null)
            {
                return;
            }
        }

        Transform foot = footL != null ? footL : footR;
        if (footL != null && footR != null)
        {
            foot = footL.position.y <= footR.position.y ? footL : footR;
        }

        Vector3 position = foot != null ? foot.position : transform.position;
        int count = ParticleQuality.ScaleCount(Random.Range(4, 7));
        if (count <= 0)
        {
            return;
        }

        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
        emit.position = position;
        footDust.Emit(emit, count);
    }

    static void Swing(Transform bone, float pitch)
    {
        if (bone == null)
        {
            return;
        }

        bone.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    struct DrivenBone
    {
        public Transform bone;
        public bool tracked;
        public Quaternion baseRotation;
        public Quaternion written;
    }

    static void ApplyDrive(ref DrivenBone slot, Quaternion offset)
    {
        if (slot.bone == null)
        {
            return;
        }

        Quaternion current = slot.bone.localRotation;
        Quaternion basis = current;
        if (slot.tracked && Quaternion.Angle(current, slot.written) < 0.01f)
        {
            basis = slot.baseRotation;
        }

        Quaternion next = basis * offset;
        slot.bone.localRotation = next;
        slot.baseRotation = basis;
        slot.written = next;
        slot.tracked = true;
    }

    static Quaternion LimitLean(Quaternion offset)
    {
        float angle;
        Vector3 axis;
        offset.ToAngleAxis(out angle, out axis);
        if (angle > 180f)
        {
            angle -= 360f;
        }

        if (Mathf.Abs(angle) <= 15f || axis.sqrMagnitude < 0.0001f)
        {
            return offset;
        }

        return Quaternion.AngleAxis(Mathf.Sign(angle) * 15f, axis);
    }

    static Transform FindFirst(Transform root, params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Transform found = FindNamed(root, names[i]);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    static Transform FindNamed(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindNamed(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
}
