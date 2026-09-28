using UnityEngine;
using UnityEngine.InputSystem;

namespace LanternKeeper
{
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] float moveSpeed = 6f;
    [SerializeField] float gravity = -20f;
    [SerializeField] float turnSpeed = 12f;

    CharacterController controller;
    float verticalVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || controller == null)
        {
            return;
        }

        float inputX = 0f;
        float inputZ = 0f;
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

        Vector3 velocity = worldMove * moveSpeed;
        velocity.y = verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        Vector3 look = worldMove;
        if (look.sqrMagnitude > 0.01f)
        {
            Quaternion facing = Quaternion.LookRotation(look, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, facing, turnSpeed * Time.deltaTime);
        }
    }
}
}
