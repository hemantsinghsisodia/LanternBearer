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

    PlayerController player;
    Transform head;
    Transform spine;
    Transform legL;
    Transform legR;
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

    void OnDisable()
    {
        ClearDrives();
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
                animator.SetTrigger(jumpParameter);
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

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            if (hasSpeed)
            {
                animator.SetFloat(speedParameter, speed, speedDampTime, Time.deltaTime);
            }

            if (hasGrounded)
            {
                animator.SetBool(groundedParameter, grounded);
            }

            if (hasVerticalSpeed)
            {
                animator.SetFloat(verticalSpeedParameter, vertical);
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
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFootstep(transform.position);
        }
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
