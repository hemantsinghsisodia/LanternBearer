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
    [SerializeField] float turnSpeed = 12f;
    [SerializeField] float stepDistance = 2.3f;

    CharacterController controller;
    Lantern lantern;
    ParticleSystem dust;
    Vector3 lastPosition;
    float verticalVelocity;
    float walked;

    public bool IsSprinting { get; private set; }
    public float HorizontalSpeed { get; private set; }
    public Vector3 LastSafePosition { get; private set; }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        lantern = GetComponentInChildren<Lantern>();
        Transform dustTransform = transform.Find("Dust");
        if (dustTransform != null)
        {
            dust = dustTransform.GetComponent<ParticleSystem>();
        }

        lastPosition = transform.position;
        LastSafePosition = transform.position;
    }

    void Update()
    {
        if (controller == null)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        bool roundOver = GameManager.Instance != null && GameManager.Instance.IsRoundOver;
        float inputX = 0f;
        float inputZ = 0f;
        bool sprintHeld = false;

        if (keyboard != null && !roundOver)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                inputX -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                inputX += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                inputZ -= 1f;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                inputZ += 1f;
            }

            sprintHeld = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        }

        IsSprinting = sprintHeld;
        if (lantern != null)
        {
            lantern.SetDrainModifier(this, sprintHeld ? sprintDrainMultiplier : 1f);
        }

        Vector3 input = new Vector3(inputX, 0f, inputZ);
        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        Vector3 worldMove = input;
        Camera view = Camera.main;
        if (view != null)
        {
            Vector3 forward = view.transform.forward;
            forward.y = 0f;
            Vector3 right = view.transform.right;
            right.y = 0f;
            if (forward.sqrMagnitude > 0.001f && right.sqrMagnitude > 0.001f)
            {
                forward.Normalize();
                right.Normalize();
                worldMove = forward * input.z + right * input.x;
            }
        }

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;
        float speed = sprintHeld ? moveSpeed * sprintMultiplier : moveSpeed;
        Vector3 velocity = worldMove * speed;
        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        Vector3 look = worldMove;
        if (look.sqrMagnitude > 0.01f)
        {
            Quaternion facing = Quaternion.LookRotation(look, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, facing, turnSpeed * Time.deltaTime);
        }

        Vector3 flatDelta = transform.position - lastPosition;
        flatDelta.y = 0f;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        HorizontalSpeed = flatDelta.magnitude / dt;
        lastPosition = transform.position;

        if (controller.isGrounded && transform.position.y > WaterHazard.SurfaceY + 0.35f)
        {
            LastSafePosition = transform.position;
        }

        UpdateDust(worldMove.sqrMagnitude > 0.01f && controller.isGrounded);
        UpdateFootsteps();
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

    void UpdateFootsteps()
    {
        if (!controller.isGrounded || HorizontalSpeed < 0.8f)
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
