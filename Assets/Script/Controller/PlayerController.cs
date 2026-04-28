using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerView))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float rotationSpeed = 10f;
    [SerializeField] private float stoppingDistance = 0.5f;

    [Header("Network Settings")]
    [SerializeField] private float sendInterval = 0.1f;
    [SerializeField] private float lerpSpeed = 10f;

    [Header("Click-to-Move Settings")]
    [SerializeField] private bool enableClickToMove = true;
    [SerializeField] private LayerMask groundLayer;

    [Header("Click Effect Settings")]
    [SerializeField] private GameObject mouseClickPrefab;
    [SerializeField] private float clickEffectYOffset = 0.35f;
    [SerializeField] private float clickEffectLifetime = 1f;

    [Header("Cursor Preview Settings")]
    [SerializeField] private GameObject cursorPreviewPrefab;
    [SerializeField] private float cursorPreviewYOffset = 0.3f;
    [SerializeField] private bool showCursorPreview = true;
    [SerializeField] private bool autoHideSystemCursor = true;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // ─── Components ────────────────────────────────
    private CharacterController characterController;
    private PlayerView playerView;
    private PlayerModel playerModel;
    private Camera mainCamera;
    private string playerSessionId;
    private Vector3 previousPosition;

    private CameraFollow cameraFollow;

    // ─── Cursor Preview ────────────────────────────
    private GameObject cursorPreviewInstance;
    private bool isCursorOverGround;

    /// <summary>Static flag — chỉ báo state, KHÔNG tự set Cursor.visible</summary>
    public static bool IsShowingGroundCursor { get; private set; } = false;

    // ═══════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerView = GetComponent<PlayerView>();
        mainCamera = Camera.main;

        if (mainCamera == null) Debug.LogError("[PlayerController] Camera.main not found!");
        if (groundLayer == 0) Debug.LogWarning("[PlayerController] Ground Layer not set!");
    }

    private void Update()
    {
        if (playerModel == null) return;

        if (playerModel.IsLocalPlayer)
        {
            HandleLocalPlayer();
            UpdateCursorPreview();
        }
        else
        {
            HandleRemotePlayer();
        }
    }

    private void OnDestroy()
    {
        if (playerModel != null)
            playerModel.OnSpeedChanged -= playerView.UpdateAnimationSpeed;

        if (cursorPreviewInstance != null)
            Destroy(cursorPreviewInstance);

        IsShowingGroundCursor = false;
    }

    private void OnDisable() => SetGroundCursorState(false);

    // ═══════════════════════════════════════════════
    // INITIALIZE
    // ═══════════════════════════════════════════════

    public void Initialize(string sessionId, Player state, bool isLocal)
    {
        playerSessionId = sessionId;

        //  FIX: state.avatarIndex là float (Colyseus "number")
        //         Cast (int) khi truyền vào PlayerModel nếu cần dùng làm index
        playerModel = new PlayerModel(sessionId, state, isLocal, moveSpeed, rotationSpeed)
        {
            SendInterval = sendInterval,
            LerpSpeed = lerpSpeed,
            StoppingDistance = stoppingDistance
        };

        playerView.Initialize(state.username, isLocal);

        transform.position = new Vector3(state.x, state.y, state.z);
        transform.rotation = Quaternion.Euler(0, state.rotationY, 0);
        previousPosition = transform.position;

        playerModel.OnSpeedChanged += playerView.UpdateAnimationSpeed;

        if (isLocal) InitializeCursorPreview();

        if (showDebug)
            Debug.Log($"[PlayerController] Initialized: {state.username}" +
                      $" | avatarIndex={(int)state.avatarIndex}" +  //  cast (int) khi log
                      $" | isLocal={isLocal}");
    }

    // ═══════════════════════════════════════════════
    // CURSOR PREVIEW
    // ═══════════════════════════════════════════════

    private void InitializeCursorPreview()
    {
        if (cursorPreviewPrefab == null || !showCursorPreview) return;
        cursorPreviewInstance = Instantiate(cursorPreviewPrefab);
        cursorPreviewInstance.name = "CursorPreview";
        cursorPreviewInstance.SetActive(false);
    }

    /// <summary>
    /// Quản lý cursorPreviewInstance (3D object).
    /// KHÔNG bao giờ set Cursor.visible — để CombinedCursorManager xử lý.
    /// </summary>
    private void UpdateCursorPreview()
    {
        if (cursorPreviewInstance == null || !showCursorPreview) return;

        if (mainCamera == null) { mainCamera = Camera.main; if (mainCamera == null) return; }

        // Priority 1: Đang trên UI
        if (IsPointerOverAnyUI()) { SetGroundCursorState(false); return; }

        // Priority 2: CombinedCursorManager đang quản lý
        if (CombinedCursorManager.Instance != null && CombinedCursorManager.Instance.IsManagingCursor)
        { SetGroundCursorState(false); return; }

        // Priority 3: Raycast xuống Ground
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            bool isGround = ((1 << hit.collider.gameObject.layer) & groundLayer) != 0;

            if (isGround)
            {
                SetGroundCursorState(true);
                cursorPreviewInstance.transform.position =
                    new Vector3(hit.point.x, cursorPreviewYOffset, hit.point.z);
            }
            else SetGroundCursorState(false);
        }
        else SetGroundCursorState(false);
    }

    private void SetGroundCursorState(bool onGround)
    {
        if (isCursorOverGround == onGround) return;

        isCursorOverGround = onGround;
        IsShowingGroundCursor = onGround;

        if (cursorPreviewInstance != null)
            cursorPreviewInstance.SetActive(onGround);

        if (showDebug) Debug.Log($"[PlayerController] IsShowingGroundCursor = {onGround}");
    }

    private bool IsPointerOverAnyUI()
        => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    // ═══════════════════════════════════════════════
    // INPUT
    // ═══════════════════════════════════════════════

    private void HandleInput()
    {
        if (!enableClickToMove) return;

        // Lấy CameraFollow nếu chưa có
        if (cameraFollow == null)
        {
            cameraFollow = mainCamera?.GetComponent<CameraFollow>();
            if (cameraFollow == null) cameraFollow = FindObjectOfType<CameraFollow>();
        }

        //  Chỉ fire move khi CameraFollow xác nhận đây là click thuần (không drag)
        bool isCleanClick = cameraFollow != null
            ? cameraFollow.IsCleanClick
            : Input.GetMouseButtonUp(0); // fallback nếu không có CameraFollow

        if (isCleanClick)
            TrySetMoveTarget();

        // WASD cancel click-to-move
        if ((Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.A) ||
             Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.D)) &&
            playerModel.IsMovingToTarget)
            playerModel.StopMovingToTarget();
    }

    private void TrySetMoveTarget()
    {
        if (IsPointerOverAnyUI()) return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1000f)) return;

        bool isGround = ((1 << hit.collider.gameObject.layer) & groundLayer) != 0;
        if (!isGround) return;

        Vector3 targetPos = hit.point;
        targetPos.y = transform.position.y;

        playerModel.SetTargetPosition(targetPos);
        SpawnClickEffect(hit.point);

        if (showDebug) Debug.Log($"[PlayerController] Move target: {targetPos}");
    }

    private void SpawnClickEffect(Vector3 pos)
    {
        if (mouseClickPrefab == null) return;
        GameObject fx = Instantiate(
            mouseClickPrefab,
            new Vector3(pos.x, clickEffectYOffset, pos.z),
            mouseClickPrefab.transform.rotation
        );
        Destroy(fx, clickEffectLifetime);
    }

    // ═══════════════════════════════════════════════
    // LOCAL PLAYER
    // ═══════════════════════════════════════════════

    private void HandleLocalPlayer()
    {
        if (characterController == null || !characterController.enabled) return;
        if (mainCamera == null) { mainCamera = Camera.main; if (mainCamera == null) return; }

        HandleInput();
        HandleMovement();
        ApplyGravity();
        SendPositionToServer();
    }

    private void HandleMovement()
    {
        // Chỉ đọc WASD, không dùng arrow keys
        float h = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
        float v = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
        bool hasInput = Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f;

        if (hasInput) MoveWithWASD(h, v);
        else if (playerModel.IsMovingToTarget) MoveToTarget();
        else SetIdle();
    }

    private void MoveWithWASD(float h, float v)
    {
        Vector3 forward = mainCamera.transform.forward; forward.y = 0; forward.Normalize();
        Vector3 right = mainCamera.transform.right; right.y = 0; right.Normalize();

        Vector3 dir = forward * v + right * h;

        if (dir.magnitude > 0.1f)
        {
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.LookRotation(dir),
                playerModel.RotationSpeed * Time.deltaTime
            );
            characterController.Move(dir * playerModel.MoveSpeed * Time.deltaTime);
            playerModel.SetMovementSpeed(1.0f);
            SendAnimation("walk");
        }
    }

    private void MoveToTarget()
    {
        Vector3 dir = playerModel.TargetPosition - transform.position;
        dir.y = 0;

        if (playerModel.HasReachedTarget(transform.position))
        {
            playerModel.StopMovingToTarget();
            SetIdle();
            return;
        }

        Vector3 moveDir = dir.normalized;
        if (moveDir.magnitude > 0.1f)
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.LookRotation(moveDir),
                playerModel.RotationSpeed * Time.deltaTime
            );

        float dist = Mathf.Min(playerModel.MoveSpeed * Time.deltaTime, dir.magnitude);
        characterController.Move(moveDir * dist);
        playerModel.SetMovementSpeed(1.0f);
        SendAnimation("walk");
    }

    private void SetIdle()
    {
        playerModel.SetMovementSpeed(0f);
        SendAnimation("idle");
    }

    private void ApplyGravity()
        => characterController.Move(Vector3.down * 9.81f * Time.deltaTime);

    private void SendPositionToServer()
    {
        if (Time.time - playerModel.LastSendTime < playerModel.SendInterval) return;

        playerModel.UpdateLocalPosition(transform.position, transform.eulerAngles.y);

        if (NetworkManager.Instance != null)
            NetworkManager.Instance.SendPosition(transform.position, transform.eulerAngles.y);

        playerModel.LastSendTime = Time.time;
    }

    private void SendAnimation(string animName)
    {
        if (NetworkManager.Instance != null &&
            Time.time - playerModel.LastSendTime > playerModel.SendInterval * 0.5f)
            NetworkManager.Instance.SendAnimation(animName);
    }

    // ═══════════════════════════════════════════════
    // REMOTE PLAYER
    // ═══════════════════════════════════════════════

    private void HandleRemotePlayer()
    {
        if (NetworkManager.Instance == null) return;

        previousPosition = transform.position;

        transform.position = NetworkManager.Instance.GetInterpolatedPosition(
            playerSessionId, transform.position);

        float newRotY = NetworkManager.Instance.GetInterpolatedRotation(
            playerSessionId, transform.eulerAngles.y);
        transform.rotation = Quaternion.Euler(0, newRotY, 0);

        float moved = new Vector2(
            transform.position.x - previousPosition.x,
            transform.position.z - previousPosition.z
        ).magnitude;

        float smoothSpeed = Mathf.Lerp(
            playerModel.MovementSpeed,
            moved > 0.001f ? 1f : 0f,
            Time.deltaTime * 8f
        );
        playerModel.SetMovementSpeed(smoothSpeed);
    }

    // ═══════════════════════════════════════════════
    // PUBLIC API
    // ═══════════════════════════════════════════════

    public void UpdateNetworkPosition(float x, float y, float z, float rotY)
        => playerModel?.UpdateFromNetwork(x, y, z, rotY);

    public void SetCursorPreviewEnabled(bool enabled)
    {
        showCursorPreview = enabled;
        if (!enabled) SetGroundCursorState(false);
    }

    public void SetAutoHideSystemCursor(bool enabled) => autoHideSystemCursor = enabled;
    public void ForceSystemCursorVisible(bool visible) => Cursor.visible = visible;
    /// <summary>Để MinimapClickMove có thể check IsLocalPlayer</summary>
    public bool IsLocalPlayer => playerModel != null && playerModel.IsLocalPlayer;

    /// <summary>Để MinimapClickMove gọi di chuyển giống click trên MainCamera</summary>
    public void SetMoveTarget(Vector3 worldPos)
    {
        if (playerModel == null || !playerModel.IsLocalPlayer) return;

        Vector3 targetPos = worldPos;
        targetPos.y = transform.position.y; // giữ đúng độ cao player

        playerModel.SetTargetPosition(targetPos);

        if (showDebug) Debug.Log($"[PlayerController] SetMoveTarget (external): {targetPos}");
    }

    // ═══════════════════════════════════════════════
    // GIZMOS
    // ═══════════════════════════════════════════════

    private void OnDrawGizmos()
    {
        if (playerModel == null || !playerModel.IsMovingToTarget) return;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, playerModel.TargetPosition);
        Gizmos.DrawWireSphere(playerModel.TargetPosition, 0.5f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(playerModel.TargetPosition, playerModel.StoppingDistance);
    }
}