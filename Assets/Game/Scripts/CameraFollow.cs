using UnityEngine;
using UnityEngine.InputSystem;

namespace LanternKeeper
{
public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float pivotHeight = 1.6f;
    [SerializeField] float distance = 6f;
    [SerializeField] float minPitch = -10f;
    [SerializeField] float maxPitch = 55f;
    [SerializeField] float mouseSensitivity = 0.1f;
    [SerializeField] float keyboardYawSpeed = 120f;
    [SerializeField] float stickYawSpeed = 140f;
    [SerializeField] float positionSmoothTime = 0.08f;
    [SerializeField] float recoverSmoothTime = 0.18f;
    [SerializeField] float collisionRadius = 0.25f;
    [SerializeField] float minDistance = 0.45f;

    float yaw;
    float pitch = 28f;
    float currentDistance = 6f;
    float distanceVelocity;
    Vector3 cameraVelocity;
    bool snapped;
    int collisionMask;

    readonly RaycastHit[] hits = new RaycastHit[32];

    public static float ExternalYaw;
    public static bool UseExternalYaw;

    public float Yaw
    {
        get { return yaw; }
        set { yaw = value; }
    }

    public float Pitch => pitch;
    public float CurrentDistance => currentDistance;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        snapped = false;
    }

    public void SnapBehind()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }

        if (target == null)
        {
            return;
        }

        yaw = target.eulerAngles.y;
        pitch = 22f;
        currentDistance = distance;
        distanceVelocity = 0f;
        cameraVelocity = Vector3.zero;
        snapped = true;
        Vector3 pivot = target.position + Vector3.up * pivotHeight;
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 direction = orbit * Vector3.back;
        float allowed = KeepAboveGround(pivot, direction, ResolveDistance(pivot, direction, distance));
        currentDistance = allowed;
        transform.position = pivot + direction * currentDistance;
        Vector3 look = pivot - transform.position;
        if (look.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }
    }

    void Awake()
    {
        collisionMask = ~0;
        int water = LayerMask.NameToLayer("Water");
        int ignore = LayerMask.NameToLayer("Ignore Raycast");
        int ui = LayerMask.NameToLayer("UI");
        if (water >= 0)
        {
            collisionMask &= ~(1 << water);
        }

        if (ignore >= 0)
        {
            collisionMask &= ~(1 << ignore);
        }

        if (ui >= 0)
        {
            collisionMask &= ~(1 << ui);
        }
    }

    void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }

        if (target != null)
        {
            yaw = target.eulerAngles.y;
        }

        currentDistance = distance;
    }

    void LateUpdate()
    {
        bool menu = MenuOpen();
        ApplyCursor(menu);
        if (target == null || menu)
        {
            return;
        }

        float dt = Time.deltaTime;
        ReadLook(dt);

        Vector3 pivot = target.position + Vector3.up * pivotHeight;
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 direction = orbit * Vector3.back;
        float allowed = KeepAboveGround(pivot, direction, ResolveDistance(pivot, direction, distance));
        if (!snapped || allowed < currentDistance)
        {
            currentDistance = allowed;
            distanceVelocity = 0f;
            snapped = true;
        }
        else
        {
            currentDistance = Mathf.SmoothDamp(currentDistance, allowed, ref distanceVelocity, recoverSmoothTime, Mathf.Infinity, dt);
        }

        Vector3 goal = pivot + direction * currentDistance;
        float floor = GroundClearance(goal);
        if (goal.y < floor)
        {
            goal.y = floor;
        }

        float heldDistance = Vector3.Distance(pivot, transform.position);
        if (currentDistance + 0.02f < heldDistance)
        {
            transform.position = goal;
            cameraVelocity = Vector3.zero;
        }
        else
        {
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref cameraVelocity, positionSmoothTime, Mathf.Infinity, dt);
            Vector3 corrected = PullOutOfCollision(pivot, transform.position);
            if (Vector3.Distance(pivot, corrected) + 0.02f < Vector3.Distance(pivot, transform.position))
            {
                transform.position = corrected;
                cameraVelocity = Vector3.zero;
            }
        }

        Vector3 look = pivot - transform.position;
        if (look.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }
    }

    void ReadLook(float dt)
    {
        if (UseExternalYaw)
        {
            yaw = ExternalYaw;
        }

        bool paused = GameManager.Instance != null && GameManager.Instance.IsPaused;
        if (paused)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.isPressed)
            {
                yaw -= keyboardYawSpeed * dt;
            }

            if (keyboard.rightArrowKey.isPressed)
            {
                yaw += keyboardYawSpeed * dt;
            }
        }

        Gamepad pad = Gamepad.current;
        if (pad != null)
        {
            Vector2 stick = pad.rightStick.ReadValue();
            yaw += stick.x * stickYawSpeed * dt;
            pitch = Mathf.Clamp(pitch - stick.y * stickYawSpeed * dt, minPitch, maxPitch);
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * mouseSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * mouseSensitivity, minPitch, maxPitch);
        }

        if (UseExternalYaw)
        {
            yaw = ExternalYaw;
        }
    }

    float ResolveDistance(Vector3 pivot, Vector3 direction, float wanted)
    {
        float allowed = wanted;
        int count = Physics.SphereCastNonAlloc(pivot, collisionRadius, direction, hits, wanted, collisionMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.distance <= 0.02f || IsPlayer(hit.collider))
            {
                continue;
            }

            if (hit.distance < allowed)
            {
                allowed = hit.distance;
            }
        }

        return Mathf.Max(minDistance, allowed - 0.04f);
    }

    float KeepAboveGround(Vector3 pivot, Vector3 direction, float span)
    {
        Vector3 point = PlaceAboveGround(pivot, direction, span);
        return Vector3.Distance(pivot, point);
    }

    Vector3 PlaceAboveGround(Vector3 pivot, Vector3 direction, float span)
    {
        Vector3 point = pivot + direction * span;
        float floor = GroundClearance(point);
        if (point.y >= floor)
        {
            return point;
        }

        float lo = minDistance;
        float hi = span;
        for (int i = 0; i < 8; i++)
        {
            float mid = (lo + hi) * 0.5f;
            Vector3 sample = pivot + direction * mid;
            if (sample.y < GroundClearance(sample))
            {
                hi = mid;
            }
            else
            {
                lo = mid;
            }
        }

        point = pivot + direction * lo;
        floor = GroundClearance(point);
        if (point.y < floor)
        {
            point.y = floor;
        }

        return point;
    }

    Vector3 PullOutOfCollision(Vector3 pivot, Vector3 position)
    {
        Vector3 offset = position - pivot;
        float span = offset.magnitude;
        if (span < 0.001f)
        {
            return position;
        }

        Vector3 direction = offset / span;
        float allowed = ResolveDistance(pivot, direction, span);
        Vector3 pulled = PlaceAboveGround(pivot, direction, Mathf.Min(span, allowed));
        return pulled;
    }

    float GroundClearance(Vector3 world)
    {
        float floor = float.NegativeInfinity;
        Terrain[] terrains = Terrain.activeTerrains;
        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (world.x < origin.x || world.z < origin.z || world.x > origin.x + size.x || world.z > origin.z + size.z)
            {
                continue;
            }

            float height = terrain.SampleHeight(world) + origin.y + collisionRadius + 0.08f;
            if (height > floor)
            {
                floor = height;
            }
        }

        return floor;
    }

    bool IsPlayer(Collider collider)
    {
        if (collider == null || target == null)
        {
            return false;
        }

        Transform hitTransform = collider.transform;
        return hitTransform == target || hitTransform.IsChildOf(target);
    }

    bool MenuOpen()
    {
        GameManager manager = GameManager.Instance;
        if (manager == null)
        {
            return false;
        }

        return manager.IsPaused || (manager.IsRoundOver && !manager.DawnPlaying);
    }

    void ApplyCursor(bool menu)
    {
        if (!Application.isPlaying)
        {
            ReleaseCursor();
            return;
        }

        if (menu || !Application.isFocused)
        {
            ReleaseCursor();
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()
    {
        ReleaseCursor();
    }

    void OnDestroy()
    {
        ReleaseCursor();
    }

    static void ReleaseCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
}
