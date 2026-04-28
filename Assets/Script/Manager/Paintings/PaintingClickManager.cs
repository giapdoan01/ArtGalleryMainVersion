using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class PaintingClickManager : MonoBehaviour
{
    public static PaintingClickManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private LayerMask paintingLayer;
    [SerializeField] private float maxClickDistance = 100f;
    [SerializeField] private float dragThreshold    = 8f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true; // ← bật để xem log

    private List<PaintingPrefab> registeredPaintings = new List<PaintingPrefab>();

    private bool           isPressing      = false;
    private bool           isDragging      = false;
    private Vector2        pressStartPos;
    private PaintingPrefab pressedPainting;

    // ════════════════════════════════════════════════
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        HandleMouseDown();
        HandleMouseDrag();
        HandleMouseUp();
    }

    // ── Bước 1 ───────────────────────────────────────
    private void HandleMouseDown()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        if (IsPointerOverUI())
        {
            if (showDebug) Debug.Log("[ClickManager] MouseDown blocked by UI");
            return;
        }

        isPressing      = true;
        isDragging      = false;
        pressStartPos   = Input.mousePosition;
        pressedPainting = RaycastPainting();

        if (showDebug)
            Debug.Log($"[ClickManager] MouseDown → pressedPainting = {(pressedPainting != null ? pressedPainting.name : "NULL")}");
    }

    // ── Bước 2 ───────────────────────────────────────
    private void HandleMouseDrag()
    {
        if (!isPressing) return;

        float dist = Vector2.Distance(Input.mousePosition, pressStartPos);
        if (dist > dragThreshold && !isDragging)
        {
            isDragging = true;
            if (showDebug) Debug.Log($"[ClickManager] Drag detected ({dist:F1}px) → will skip click");
        }
    }

    // ── Bước 3 ───────────────────────────────────────
    private void HandleMouseUp()
    {
        if (!Input.GetMouseButtonUp(0)) return;

        if (showDebug)
            Debug.Log($"[ClickManager] MouseUp → isPressing={isPressing}, isDragging={isDragging}, pressedPainting={(pressedPainting != null ? pressedPainting.name : "NULL")}");

        // Lưu lại trước khi reset
        bool           wasPressing = isPressing;
        bool           wasDragging = isDragging;
        PaintingPrefab wasPainting = pressedPainting;

        // Reset ngay
        isPressing      = false;
        isDragging      = false;
        pressedPainting = null;

        // Guard
        if (!wasPressing)
        {
            if (showDebug) Debug.Log("[ClickManager] Skip: was not pressing");
            return;
        }
        if (wasDragging)
        {
            if (showDebug) Debug.Log("[ClickManager] Skip: was dragging");
            return;
        }
        if (wasPainting == null)
        {
            if (showDebug) Debug.Log("[ClickManager] Skip: no painting was pressed");
            return;
        }
        if (IsPointerOverUI())
        {
            if (showDebug) Debug.Log("[ClickManager] Skip: pointer over UI on MouseUp");
            return;
        }

        //  Không raycast lại — tin tưởng painting đã được ghi nhận lúc MouseDown
        // (tránh trường hợp chuột lệch nhẹ khi nhả làm miss raycast)
        if (showDebug) Debug.Log($"[ClickManager]  Fire click → {wasPainting.name}");
        wasPainting.OnInfoColliderClicked();
    }

    // ── Raycast ──────────────────────────────────────
    private PaintingPrefab RaycastPainting()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            if (showDebug) Debug.LogWarning("[ClickManager] Camera.main is NULL!");
            return null;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        if (showDebug) Debug.Log($"[ClickManager] Raycast with layer mask: {paintingLayer.value}");

        if (!Physics.Raycast(ray, out RaycastHit hit, maxClickDistance, paintingLayer))
        {
            if (showDebug) Debug.Log("[ClickManager] Raycast miss — no collider hit on paintingLayer");

            // ── Fallback: thử raycast không filter layer để xem có hit gì không ──
            if (showDebug && Physics.Raycast(ray, out RaycastHit anyHit, maxClickDistance))
                Debug.Log($"[ClickManager] (Fallback) Hit object: '{anyHit.collider.gameObject.name}' on layer {anyHit.collider.gameObject.layer}");

            return null;
        }

        if (showDebug) Debug.Log($"[ClickManager] Raycast hit: '{hit.collider.gameObject.name}' on layer {hit.collider.gameObject.layer}");

        PaintingPrefab painting = hit.collider.GetComponentInParent<PaintingPrefab>();

        if (painting == null)
        {
            if (showDebug) Debug.Log("[ClickManager] Hit collider has no PaintingPrefab in parent");
            return null;
        }

        if (!registeredPaintings.Contains(painting))
        {
            if (showDebug) Debug.Log($"[ClickManager] Painting '{painting.name}' not registered!");
            return null;
        }

        return painting;
    }

    // ── UI check ─────────────────────────────────────
    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        if (Input.touchCount > 0)
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return EventSystem.current.IsPointerOverGameObject();
    }

    // ── Register / Unregister ─────────────────────────
    public void RegisterPainting(PaintingPrefab painting)
    {
        if (!registeredPaintings.Contains(painting))
        {
            registeredPaintings.Add(painting);
            if (showDebug) Debug.Log($"[ClickManager] Registered: {painting.name} (total: {registeredPaintings.Count})");
        }
    }

    public void UnregisterPainting(PaintingPrefab painting)
    {
        registeredPaintings.Remove(painting);
        if (showDebug) Debug.Log($"[ClickManager] Unregistered: {painting.name}");
    }
}