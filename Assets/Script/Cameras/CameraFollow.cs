using UnityEngine;
using UnityEngine.EventSystems;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Settings")]
    [SerializeField] private float distance    = 5f;
    [SerializeField] private float height      = 2f;
    [SerializeField] private float smoothSpeed = 10f;

    [Header("Mouse Control")]
    [SerializeField] private float mouseSensitivity = 3f;

    [Header("Arrow Key Rotation")]
    [SerializeField] private float arrowRotateSpeed = 90f; // độ/giây

    [Header("Camera Mode")]
    [SerializeField] private bool  firstPersonMode         = false;
    [SerializeField] private float firstPersonHeightOffset = 1.6f;

    [Header("Drag Detection")]
    [SerializeField] private float dragThreshold = 8f; // pixel — vượt quá = drag

    private float currentYaw   = 0f;
    private float currentPitch = 0f;

    // ── Drag state ───────────────────────────────────
    private bool    isPressing        = false; // đang giữ chuột trái
    private bool    isDragging        = false; // đã vượt ngưỡng → đang xoay camera
    private Vector2 pressStartPos;
    private Vector2 lastMousePosition;
    public bool IsDragging => isDragging;
    public bool IsCleanClick { get; private set; } = false;

    // ═══════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════

    private void Start()
    {
        currentYaw   = 0f;
        currentPitch = 0f;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        IsCleanClick = false; // reset mỗi frame

        HandleInput();
        UpdateCamera();
    }

    // ═══════════════════════════════════════════════
    // INPUT — phân biệt click vs drag
    // ═══════════════════════════════════════════════

    private void HandleInput()
    {
        // ── MouseDown: bắt đầu theo dõi ──────────────
        if (Input.GetMouseButtonDown(0))
        {
            if (!IsPointerOverUI())
            {
                isPressing    = true;
                isDragging    = false;
                pressStartPos = Input.mousePosition;
                lastMousePosition = Input.mousePosition;
            }
        }

        // ── Đang giữ: kiểm tra có vượt ngưỡng drag không ──
        if (isPressing && Input.GetMouseButton(0))
        {
            float dist = Vector2.Distance(Input.mousePosition, pressStartPos);

            if (dist > dragThreshold)
                isDragging = true; // đánh dấu drag

            // Xoay camera CHỈ khi đã xác nhận drag
            if (isDragging)
            {
                Vector2 delta     = (Vector2)Input.mousePosition - lastMousePosition;
                lastMousePosition = Input.mousePosition;

                currentYaw   += delta.x * mouseSensitivity * Time.deltaTime * 10f;
                currentPitch -= delta.y * mouseSensitivity * Time.deltaTime * 10f;

                currentPitch = Mathf.Clamp(
                    currentPitch,
                    firstPersonMode ? -80f : -20f,
                    firstPersonMode ?  80f :  60f
                );
            }
        }

        // ── MouseUp: phân loại kết quả ────────────────
        if (Input.GetMouseButtonUp(0))
        {
            if (isPressing && !isDragging)
                IsCleanClick = true; // click thuần → báo cho PlayerController

            isPressing = false;
            isDragging = false;
        }

        // ── Arrow keys: xoay camera ───────────────────
        float arrowH = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        float arrowV = (Input.GetKey(KeyCode.UpArrow)    ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);

        if (Mathf.Abs(arrowH) > 0f)
            currentYaw += arrowH * arrowRotateSpeed * Time.deltaTime;

        if (Mathf.Abs(arrowV) > 0f)
        {
            currentPitch -= arrowV * arrowRotateSpeed * Time.deltaTime;
            currentPitch = Mathf.Clamp(
                currentPitch,
                firstPersonMode ? -80f : -20f,
                firstPersonMode ?  80f :  60f
            );
        }
    }

    private bool IsPointerOverUI()
        => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    // ═══════════════════════════════════════════════
    // PUBLIC API
    // ═══════════════════════════════════════════════

    public void SetTarget(Transform newTarget, bool resetRotation = true)
    {
        target = newTarget;
        if (target == null) return;
        if (resetRotation) { currentYaw = 0f; currentPitch = 0f; }
    }

    public void ToggleCameraMode()
    {
        firstPersonMode = !firstPersonMode;
        currentPitch    = 0f;
    }

    public void SetCameraMode(bool isFirstPerson)
    {
        if (firstPersonMode != isFirstPerson)
        {
            firstPersonMode = isFirstPerson;
            currentPitch    = 0f;
        }
    }

    public bool  IsFirstPersonMode()        => firstPersonMode;
    public void  SetCameraYaw(float yaw)    => currentYaw = yaw;
    public void  SetCameraPitch(float pitch) => currentPitch = Mathf.Clamp(pitch,
                                                    firstPersonMode ? -80f : -20f,
                                                    firstPersonMode ?  80f :  60f);
    public float GetCurrentYaw()            => currentYaw;
    public float GetCurrentPitch()          => currentPitch;

    // ═══════════════════════════════════════════════
    // CAMERA UPDATE
    // ═══════════════════════════════════════════════

    private void UpdateCamera()
    {
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);

        if (firstPersonMode)
        {
            transform.position = target.position + Vector3.up * firstPersonHeightOffset;
            transform.rotation = Quaternion.Slerp(
                transform.rotation, rotation, smoothSpeed * Time.deltaTime);
        }
        else
        {
            Vector3 targetPosition  = target.position + Vector3.up * height;
            Vector3 direction       = rotation * Vector3.back;
            Vector3 desiredPosition = targetPosition + direction * distance;

            transform.position = Vector3.Lerp(
                transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            transform.LookAt(targetPosition);
        }
    }
}