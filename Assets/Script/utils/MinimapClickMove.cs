using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Gắn vào GameObject chứa RawImage Minimap.
/// Click lên minimap → tính world position → gọi PlayerController di chuyển.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class MinimapClickMove : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("References")]
    [SerializeField] private Camera     minimapCamera;
    [SerializeField] private RawImage   minimapRawImage;

    [Header("Click-to-Move")]
    [SerializeField] private LayerMask  groundLayer;
    [SerializeField] private bool       enableClickToMove = true;

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
        if (!isHoveringMinimap) return;
        UpdateCursorPreview();
    }

    private void OnDestroy()
    {
        if (cursorPreviewInstance != null)
            Destroy(cursorPreviewInstance);
    }

    // ═══════════════════════════════════════════════
    // FIND LOCAL PLAYER (lazy)
    // ═══════════════════════════════════════════════

    private PlayerController GetLocalPlayer()
    {
        if (localPlayer != null) return localPlayer;

        // Tìm PlayerController có IsLocalPlayer = true
        foreach (var pc in FindObjectsOfType<PlayerController>())
        {
            // Dùng reflection-free: PlayerController expose IsLocalPlayer qua public property
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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!enableClickToMove) return;
        if (eventData.button != PointerEventData.InputButton.Left) return;

        if (!TryGetWorldPosition(eventData.position, out Vector3 worldPos)) return;

        // Gọi PlayerController di chuyển
        PlayerController player = GetLocalPlayer();
        if (player == null) { Debug.LogWarning("[MinimapClickMove] LocalPlayer not found!"); return; }

        player.SetMoveTarget(worldPos);
        SpawnClickEffect(worldPos);

        if (showDebug) Debug.Log($"[MinimapClickMove] Move to: {worldPos}");
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