using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Gắn vào GameObject chứa RawImage Minimap.
/// Click lên minimap → tính world position → gọi PlayerController di chuyển + reset camera về player.
/// Kéo (drag) lên minimap → pan camera minimap để xem vùng khác.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class MinimapClickMove : MonoBehaviour,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("References")]
    [SerializeField] private Camera     minimapCamera;
    [SerializeField] private RawImage   minimapRawImage;

    [Header("Click-to-Move")]
    [SerializeField] private LayerMask  groundLayer;
    [SerializeField] private bool       enableClickToMove = true;

    [Header("Drag-to-Pan")]
    [SerializeField] private bool       enableDragToPan  = true;

    [Header("Click Effect")]
    [SerializeField] private GameObject mouseClickPrefab;
    [SerializeField] private float      clickEffectYOffset  = 0.35f;
    [SerializeField] private float      clickEffectLifetime = 1f;

    [Header("Cursor Preview on Minimap")]
    [SerializeField] private GameObject cursorPreviewPrefab;
    [SerializeField] private float      cursorPreviewYOffset = 0.3f;
    [SerializeField] private bool       showCursorPreview    = true;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // ── Runtime ──────────────────────────────────
    private PlayerController localPlayer;
    private GameObject       cursorPreviewInstance;
    private bool             isHoveringMinimap = false;
    private RectTransform    rawImageRect;

    // ── Drag-to-Pan ───────────────────────────────
    private bool    isPanned        = false;  // camera đang ở trạng thái pan (không follow player)
    private bool    blockNextClick  = false;  // sau drag → chặn OnPointerClick kế tiếp
    private Vector2 dragLastPos;              // vị trí chuột frame trước trong drag

    // ═══════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════

    private void Awake()
    {
        if (minimapRawImage == null)
            minimapRawImage = GetComponent<RawImage>();

        rawImageRect = minimapRawImage.GetComponent<RectTransform>();

        if (minimapCamera == null)
            Debug.LogError("[MinimapClickMove] minimapCamera not assigned!");

        if (groundLayer == 0)
            Debug.LogWarning("[MinimapClickMove] groundLayer not set!");
    }

    private void Start()
    {
        if (cursorPreviewPrefab != null && showCursorPreview)
        {
            cursorPreviewInstance = Instantiate(cursorPreviewPrefab);
            cursorPreviewInstance.name = "MinimapCursorPreview";
            cursorPreviewInstance.SetActive(false);
        }
    }

    private void Update()
    {
        // Khi không bị pan → camera tự follow player
        if (!isPanned)
            FollowPlayer();

        if (!isHoveringMinimap) return;
        UpdateCursorPreview();
    }

    private void OnDestroy()
    {
        if (cursorPreviewInstance != null)
            Destroy(cursorPreviewInstance);
    }

    // ═══════════════════════════════════════════════
    // CAMERA FOLLOW PLAYER
    // ═══════════════════════════════════════════════

    private void FollowPlayer()
    {
        if (minimapCamera == null) return;

        PlayerController player = GetLocalPlayer();
        if (player == null) return;

        Vector3 camPos = minimapCamera.transform.position;
        minimapCamera.transform.position = new Vector3(
            player.transform.position.x,
            camPos.y,
            player.transform.position.z
        );
    }

    private void ResetCameraToPlayer()
    {
        isPanned = false;
        // FollowPlayer() sẽ snap camera về player ở frame tiếp theo
    }

    // ═══════════════════════════════════════════════
    // FIND LOCAL PLAYER (lazy)
    // ═══════════════════════════════════════════════

    private PlayerController GetLocalPlayer()
    {
        if (localPlayer != null) return localPlayer;

        foreach (var pc in FindObjectsOfType<PlayerController>())
        {
            if (pc.IsLocalPlayer)
            {
                localPlayer = pc;
                break;
            }
        }
        return localPlayer;
    }

    // ═══════════════════════════════════════════════
    // POINTER EVENTS
    // ═══════════════════════════════════════════════

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHoveringMinimap = true;
        if (showDebug) Debug.Log("[MinimapClickMove] Hover Enter");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHoveringMinimap = false;
        if (cursorPreviewInstance != null)
            cursorPreviewInstance.SetActive(false);
        if (showDebug) Debug.Log("[MinimapClickMove] Hover Exit");
    }

    /// <summary>
    /// Click thuần (không drag) → di chuyển player + reset camera về follow player.
    /// Unity EventSystem tự động không gọi OnPointerClick nếu IDragHandler đã nhận drag.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // Drag vừa kết thúc → bỏ qua click này
        if (blockNextClick) { blockNextClick = false; return; }

        if (!enableClickToMove) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (!TryGetWorldPosition(eventData.position, out Vector3 worldPos)) return;

        PlayerController player = GetLocalPlayer();
        if (player == null) { Debug.LogWarning("[MinimapClickMove] LocalPlayer not found!"); return; }

        player.SetMoveTarget(worldPos);
        SpawnClickEffect(worldPos);

        // Reset camera về follow player
        ResetCameraToPlayer();

        if (showDebug) Debug.Log($"[MinimapClickMove] Move to: {worldPos}");
    }

    // ═══════════════════════════════════════════════
    // DRAG-TO-PAN
    // ═══════════════════════════════════════════════

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!enableDragToPan) return;

        dragLastPos = eventData.position;
        isPanned    = true;

        if (showDebug) Debug.Log("[MinimapClickMove] Drag Begin");
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!enableDragToPan || minimapCamera == null) return;

        Vector2 screenDelta = eventData.position - dragLastPos;
        dragLastPos = eventData.position;

        if (screenDelta == Vector2.zero) return;

        // Tính world delta từ screen delta
        // Minimap camera là orthographic top-down:
        //   worldHeight = orthographicSize * 2  (đơn vị world trên trục Z)
        //   worldWidth  = worldHeight * aspect  (đơn vị world trên trục X)
        Vector2 rawImageSize = new Vector2(rawImageRect.rect.width, rawImageRect.rect.height);
        if (rawImageSize.x <= 0f || rawImageSize.y <= 0f) return;

        float uvDeltaX = screenDelta.x / rawImageSize.x;
        float uvDeltaY = screenDelta.y / rawImageSize.y;

        float worldHeight = minimapCamera.orthographicSize * 2f;
        float worldWidth  = worldHeight * minimapCamera.aspect;

        float panRight = uvDeltaX * worldWidth;
        float panUp    = uvDeltaY * worldHeight;

        // Dùng trục của camera (projected xuống XZ) để đồng bộ hướng kéo khi camera bị xoay Y
        Vector3 camRight   = minimapCamera.transform.right;
        Vector3 camUp      = minimapCamera.transform.up;
        camRight.y = 0f;  camRight = camRight.sqrMagnitude > 0.001f ? camRight.normalized : Vector3.right;
        camUp.y    = 0f;  camUp    = camUp.sqrMagnitude    > 0.001f ? camUp.normalized    : Vector3.forward;

        // Kéo chuột sang phải → camera dịch trái (kéo map ngược chiều)
        minimapCamera.transform.position -= camRight * panRight + camUp * panUp;

        if (showDebug)
            Debug.Log($"[MinimapClickMove] Pan: ({panRight:F2}, {panUp:F2})");
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Chặn OnPointerClick sẽ fire ngay sau sự kiện này
        blockNextClick = true;

        if (showDebug) Debug.Log("[MinimapClickMove] Drag End");
    }

    // ═══════════════════════════════════════════════
    // CURSOR PREVIEW
    // ═══════════════════════════════════════════════

    private void UpdateCursorPreview()
    {
        if (cursorPreviewInstance == null || !showCursorPreview) return;

        if (TryGetWorldPosition(Input.mousePosition, out Vector3 worldPos))
        {
            cursorPreviewInstance.SetActive(true);
            cursorPreviewInstance.transform.position =
                new Vector3(worldPos.x, cursorPreviewYOffset, worldPos.z);
        }
        else
        {
            cursorPreviewInstance.SetActive(false);
        }
    }

    // ═══════════════════════════════════════════════
    // CORE: Screen → UV → Minimap Ray → World
    // ═══════════════════════════════════════════════

    /// <summary>
    /// Chuyển screen position → UV trên RawImage → Ray từ MinimapCamera → Raycast Ground.
    /// </summary>
    private bool TryGetWorldPosition(Vector2 screenPos, out Vector3 worldPos)
    {
        worldPos = Vector3.zero;

        if (minimapCamera == null || rawImageRect == null) return false;

        // ── BƯỚC 1: Screen → Local point trên RawImage ──
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rawImageRect, screenPos, null, out Vector2 localPoint))
            return false;

        // ── BƯỚC 2: Local point → UV (0..1) ──
        Rect rect = rawImageRect.rect;
        float u = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x);
        float v = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y);

        // ── BƯỚC 3: Áp dụng UV rect của RawImage (nếu có crop/offset) ──
        Rect uvRect = minimapRawImage.uvRect;
        float finalU = uvRect.x + u * uvRect.width;
        float finalV = uvRect.y + v * uvRect.height;

        if (showDebug) Debug.Log($"[MinimapClickMove] UV = ({finalU:F3}, {finalV:F3})");

        // ── BƯỚC 4: UV → Ray từ MinimapCamera ──
        Ray ray = minimapCamera.ViewportPointToRay(new Vector3(finalU, finalV, 0f));

        // ── BƯỚC 5: Raycast xuống Ground ──
        if (!Physics.Raycast(ray, out RaycastHit hit, 2000f, groundLayer)) return false;

        worldPos   = hit.point;
        worldPos.y = 0f; // flatten về ground level của player
        return true;
    }

    // ═══════════════════════════════════════════════
    // CLICK EFFECT
    // ═══════════════════════════════════════════════

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
}
