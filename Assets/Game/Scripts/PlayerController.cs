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
    [SerializeField] float safeWaterMargin = 0.45f;
    [SerializeField] float safeSlope = 0.85f;

    CharacterController controller;
    Lantern lantern;
    ParticleSystem dust;
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
    public static bool ExternalSprint;
    public static bool ExternalJump;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        lantern = GetComponentInChildren<Lantern>();
        Transform dustTransform = transform.Find("Dust");
        if (dustTransform != null)
        {
            dust = dustTransform.GetComponent<ParticleSystem>();
        }

        controller.slopeLimit = 50f;
        controller.stepOffset = 0.45f;
        lastPosition = transform.position;
        LastSafePosition = transform.position;
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
            ExternalJump = false;
            IsSprinting = false;
            HorizontalSpeed = 0f;
            CurrentSpeed = 0f;
            planarVelocity = Vector3.zero;
            if (lantern != null)
            {
                lantern.SetDrainModifier(this, 1f);
            }

            UpdateDust(false);
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
        Vector3 velocity = planarVelocity;
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

        UpdateDust(planarVelocity.sqrMagnitude > 0.04f && controller.isGrounded);
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

        if (transform.position.y <= WaterHazard.SurfaceY + safeWaterMargin)
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

        Camera view = Camera.main;
        if (view == null)
        {
            return input;
        }

        float yaw;
        CameraFollow follow = view.GetComponent<CameraFollow>();
        if (CameraFollow.UseExternalYaw)
        {
            yaw = CameraFollow.ExternalYaw;
        }
        else if (follow != null)
        {
            yaw = follow.Yaw;
        }
        else
        {
            Vector3 forward = view.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                return input;
            }

            yaw = Quaternion.LookRotation(forward.normalized, Vector3.up).eulerAngles.y;
        }

        Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
        return facing * input;
    }

    void BurstLandingDust()
    {
        if (dust != null)
        {
            dust.Emit(14);
        }
    }

    void UpdateDust(bool moving)
    {
        if (dust == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = dust.emission;
        emission.rateOverTime = moving ? 16f : 0f;
    }

    public void OnFootstep()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFootstep(transform.position);
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
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayFootstep(transform.position);
        }
    }
}
}
