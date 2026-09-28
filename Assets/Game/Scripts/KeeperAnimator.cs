using UnityEngine;

namespace LanternKeeper
{
public class KeeperAnimator : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] string speedParameter = "Speed";

    PlayerController player;
    Transform head;
    Transform spine;
    Transform legL;
    Transform legR;
    Transform armL;
    Transform armR;
    Transform bob;
    float lookTimer;
    float lookYaw;
    float lookTarget;
    float lean;
    float lastYaw;
    float phase;
    bool procedural;

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
        lastYaw = transform.eulerAngles.y;
        lookTimer = 2.5f;
    }

    void CacheBones()
    {
        if (animator != null && animator.isHuman)
        {
            head = animator.GetBoneTransform(HumanBodyBones.Head);
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
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

        legL = FindNamed(transform, "Leg.L");
        legR = FindNamed(transform, "Leg.R");
        armL = FindNamed(transform, "Arm.L");
        armR = FindNamed(transform, "Arm.R");
        bob = FindNamed(transform, "Body");
        if (legL != null)
        {
            procedural = true;
        }
    }

    void Update()
    {
        float speed = player != null ? player.CurrentSpeed : 0f;
        bool sprint = player != null && player.IsSprinting && speed > 0.4f;
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetFloat(speedParameter, speed);
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
        float targetLean = Mathf.Clamp(-yawRate * 0.45f, -12f, 12f);
        if (speed < 0.3f)
        {
            targetLean = 0f;
        }

        lean = Mathf.Lerp(lean, targetLean, Time.deltaTime * 6f);

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
        if (spine != null)
        {
            spine.localRotation = spine.localRotation * Quaternion.Euler(lean * 0.15f, 0f, lean);
        }

        if (head != null && Mathf.Abs(lookYaw) > 0.2f)
        {
            head.localRotation = head.localRotation * Quaternion.Euler(0f, lookYaw, 0f);
        }
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
