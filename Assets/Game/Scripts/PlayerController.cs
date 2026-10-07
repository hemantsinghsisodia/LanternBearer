using UnityEngine;
using UnityEngine.InputSystem;

namespace LanternKeeper
{
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float sprintMultiplier = 1.7f;
    [SerializeField] float sprintDrainMultiplier = 1.6f;
    [SerializeField] float gravity = -20f;
    [SerializeField] float jumpHeight = 2.8f;
    [Tooltip("Seconds to reach full speed on the ground.")]
    [SerializeField] float acceleration = 0.12f;
    [Tooltip("Seconds to stop on the ground.")]
    [SerializeField] float deceleration = 0.10f;
    [Tooltip("Air acceleration and braking as a fraction of the ground response. Lower is slower.")]
    [SerializeField] float airControl = 0.6f;
    [SerializeField] float rotationSpeed = 720f;
    [SerializeField] float coyoteTime = 0.12f;
    [SerializeField] float jumpBufferTime = 0.15f;
    [SerializeField] float stepDistance = 2.3f;
    [SerializeField] float safeCheckInterval = 0.25f;
    [SerializeField] float safeEdgeRadius = 1.5f;
    [SerializeField] float safeWaterMargin = WaterHazard.DefaultSafeWaterMargin;
    [SerializeField] float safeSlope = 0.85f;

    public const float GrassBendRadius = 1.4f;

    CharacterController controller;
    Lantern lantern;
    Vector3 grassSample;
    bool grassSampled;
    static readonly int PlayerPosId = Shader.PropertyToID("_LKPlayerPos");
    static readonly int PlayerRadiusId = Shader.PropertyToID("_LKPlayerRadius");
    ParticleSystem dust;
    KeeperAnimator keeperAnimator;
    Vector3 planarVelocity;
    Vector3 lastPosition;
    float verticalVelocity;
    float walked;
    float coyoteTimer;
    float jumpBufferTimer;
    float brakeFrom;
    float nextSafeCheck;
    bool jumpLocked;
    bool wasOnGround;
    bool wasSlowing;

    public bool IsSprinting { get; private set; }
    public float HorizontalSpeed { get; private set; }
    public float CurrentSpeed { get; private set; }
    public float PlanarSpeed => planarVelocity.magnitude;
    public float VerticalVelocity => verticalVelocity;
    public bool IsGrounded { get; private set; }
    public bool JumpedThisFrame { get; private set; }
    public bool LandedThisFrame { get; private set; }
    public Vector3 LastSafePosition { get; private set; }
    public bool UseDistanceFootsteps = true;

    public static Vector2 ExternalMove;
    public static Vector3 ExternalPush;
    public static bool ExternalSprint;
    public static bool ExternalJump;

    [SerializeField] CameraFollow cameraFollow;
    bool viewResolved;
    static bool loggedFallback;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        lantern = GetComponentInChildren<Lantern>();
        keeperAnimator = GetComponentInChildren<KeeperAnimator>();
        Transform dustTransform = transform.Find("Dust");
        if (dustTransform != null)
        {
            dust = dustTransform.GetComponent<ParticleSystem>();
            ParticleQuality.ApplyTo(dust);
        }

        controller.slopeLimit = 50f;
        controller.stepOffset = 0.45f;
        lastPosition = transform.position;
        LastSafePosition = transform.position;
    }

    void Start()
    {
        ResolveView();
    }

    void ResolveView()
    {
        if (cameraFollow != null || viewResolved)
        {
            return;
        }

        viewResolved = true;
        Camera view = Camera.main;
        if (view != null)
        {
            cameraFollow = view.GetComponent<CameraFollow>();
        }

        if (cameraFollow == null)
        {
            cameraFollow = FindAnyObjectByType<CameraFollow>();
        }

        if (!loggedFallback)
        {
            loggedFallback = true;
            Debug.LogWarning("PlayerController camera follow was not wired. Resolved once.", this);
        }
    }

    void Update()
    {
        if (controller == null)
        {
            return;
        }

        JumpedThisFrame = false;
        LandedThisFrame = false;

        GameManager manager = GameManager.Instance;
        if (manager != null && (manager.IsPaused || manager.ControlsLocked || manager.IsDying || manager.IsRoundOver))
        {
            ExternalMove = Vector2.zero;
            ExternalPush = Vector3.zero;
            ExternalJump = false;
            IsSprinting = false;
            HorizontalSpeed = 0f;
            CurrentSpeed = 0f;
            planarVelocity = Vector3.zero;
            if (lantern != null)
            {
                lantern.SetDrainModifier(this, 1f);
            }

            UpdateDust();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        bool roundOver = GameManager.Instance != null && GameManager.Instance.IsRoundOver;
        float inputX = 0f;
        float inputZ = 0f;
        bool sprintHeld = false;
        bool jumpPressed = false;

        if (keyboard != null && !roundOver)
        {
            if (keyboard.aKey.isPressed)
            {
                inputX -= 1f;
            }

            if (keyboard.dKey.isPressed)
            {
                inputX += 1f;
            }

            if (keyboard.sKey.isPressed)
            {
                inputZ -= 1f;
            }

            if (keyboard.wKey.isPressed)
            {
                inputZ += 1f;
            }

            sprintHeld = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            jumpPressed = keyboard.spaceKey.wasPressedThisFrame;
        }

        if (ExternalMove.sqrMagnitude > 0.0001f)
        {
            inputX = ExternalMove.x;
            inputZ = ExternalMove.y;
            sprintHeld = ExternalSprint;
        }

        if (ExternalJump && !roundOver)
        {
            jumpPressed = true;
            ExternalJump = false;
        }
        else if (ExternalJump)
        {
            ExternalJump = false;
        }

        IsSprinting = sprintHeld && !roundOver;
        if (lantern != null)
        {
            lantern.SetDrainModifier(this, IsSprinting ? sprintDrainMultiplier : 1f);
        }

        Vector3 input = new Vector3(inputX, 0f, inputZ);
        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        Vector3 worldMove = CameraRelative(input);
        float dt = Time.deltaTime;
        bool onGround = controller.isGrounded && verticalVelocity <= 0.05f;
        IsGrounded = onGround;
        if (onGround)
        {
            if (!wasOnGround && verticalVelocity < -3.5f)
            {
                LandedThisFrame = true;
                BurstLandingDust();
            }

            jumpLocked = false;
            coyoteTimer = coyoteTime;
            verticalVelocity = -2f;
        }
        else
        {
            coyoteTimer = Mathf.Max(0f, coyoteTimer - dt);
        }

        wasOnGround = onGround;

        if (jumpPressed)
        {
            jumpBufferTimer = jumpBufferTime;
        }
        else
        {
            jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - dt);
        }

        if (!jumpLocked && jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpLocked = true;
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
            JumpedThisFrame = true;
            LandedThisFrame = false;
            IsGrounded = false;
        }

        float targetSpeed = IsSprinting ? moveSpeed * sprintMultiplier : moveSpeed;
        Vector3 targetVelocity = worldMove * targetSpeed;
        bool slowing = targetVelocity.sqrMagnitude + 0.01f < planarVelocity.sqrMagnitude
            || (planarVelocity.sqrMagnitude > 0.01f && Vector3.Dot(planarVelocity, targetVelocity) < 0f);
        if (slowing && !wasSlowing)
        {
            brakeFrom = planarVelocity.magnitude;
        }

        wasSlowing = slowing;
        float response = slowing ? deceleration : acceleration;
        float span = slowing ? Mathf.Max(brakeFrom, 0.01f) : Mathf.Max(targetSpeed, 0.01f);
        float rate = span / Mathf.Max(response, 0.01f);
        if (!onGround)
        {
            rate *= Mathf.Clamp(airControl, 0.05f, 1f);
        }

        planarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, rate * dt);

        verticalVelocity += gravity * dt;
        Vector3 velocity = planarVelocity + ExternalPush;
        velocity.y = verticalVelocity;
        controller.Move(velocity * dt);

        Vector3 look = planarVelocity.sqrMagnitude > 0.04f ? planarVelocity : worldMove;
        look.y = 0f;
        if (look.sqrMagnitude > 0.01f)
        {
            Quaternion facing = Quaternion.LookRotation(look.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, facing, rotationSpeed * dt);
        }

        Vector3 flatDelta = transform.position - lastPosition;
        flatDelta.y = 0f;
        float stepDt = Mathf.Max(dt, 0.0001f);
        HorizontalSpeed = flatDelta.magnitude / stepDt;
        CurrentSpeed = planarVelocity.magnitude;
        lastPosition = transform.position;

        TryRecordSafe();

        UpdateDust();
        UpdateFootsteps();
    }

    void TryRecordSafe()
    {
        if (Time.time < nextSafeCheck)
        {
            return;
        }

        nextSafeCheck = Time.time + safeCheckInterval;
        if (!controller.isGrounded)
        {
            return;
        }

        if (transform.position.y <= WaterHazard.SafeFloorY + safeWaterMargin)
        {
            return;
        }

        Vector3 normal;
        if (!GroundNormal(out normal) || normal.y <= safeSlope)
        {
            return;
        }

        if (!ClearOfEdge())
        {
            return;
        }

        LastSafePosition = transform.position;
    }

    bool GroundNormal(out Vector3 normal)
    {
        normal = Vector3.up;
        Vector3 origin = transform.position + Vector3.up * 0.35f;
        RaycastHit hit;
        if (Physics.Raycast(origin, Vector3.down, out hit, 2.2f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null && !hit.collider.transform.IsChildOf(transform))
            {
                normal = hit.normal;
                return true;
            }
        }

        float height;
        return TerrainQuery.TrySample(transform.position, out height, out normal);
    }

    bool ClearOfEdge()
    {
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 2f / 8f;
            Vector3 sample = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * safeEdgeRadius;
            float height;
            Vector3 normal;
            if (!TerrainQuery.TrySample(sample, out height, out normal))
            {
                return false;
            }

            if (height < WaterHazard.SurfaceY + safeWaterMargin)
            {
                return false;
            }
        }

        return true;
    }

    Vector3 CameraRelative(Vector3 input)
    {
        if (input.sqrMagnitude < 0.0001f)
        {
            return Vector3.zero;
        }

        float yaw;
        if (CameraFollow.UseExternalYaw)
        {
            yaw = CameraFollow.ExternalYaw;
        }
        else if (cameraFollow != null)
        {
            yaw = cameraFollow.Yaw;
        }
        else
        {
            return input;
        }

        Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
        return facing * input;
    }

    void BurstLandingDust()
    {
        if (dust != null)
        {
            int count = ParticleQuality.ScaleCount(14);
            if (count > 0)
            {
                dust.Emit(count);
            }
        }
    }

    void UpdateDust()
    {
        if (dust == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = dust.emission;
        emission.rateOverTime = 0f; // footsteps emit puffs at the feet instead (KeeperAnimator.EmitFootDust)
    }

    public void OnFootstep()
    {
        EmitFootDust();
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFootstep(transform.position);
        }
    }

    void EmitFootDust()
    {
        if (keeperAnimator != null)
        {
            keeperAnimator.EmitFootDust();
        }
    }

    void UpdateFootsteps()
    {
        if (!UseDistanceFootsteps || !controller.isGrounded || HorizontalSpeed < 0.8f)
        {
            return;
        }

        walked += HorizontalSpeed * Time.deltaTime;
        if (walked < stepDistance)
        {
            return;
        }

        walked = 0f;
        EmitFootDust();
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFootstep(transform.position);
        }
    }

    void LateUpdate()
    {
        Vector3 position = transform.position;
        if (grassSampled && (position - grassSample).sqrMagnitude <= 0.0004f)
        {
            return;
        }

        grassSampled = true;
        grassSample = position;
        Shader.SetGlobalVector(PlayerPosId, new Vector4(position.x, position.y, position.z, 0f));
        Shader.SetGlobalFloat(PlayerRadiusId, GrassBendRadius);
    }
}
}
