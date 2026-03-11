using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class Model3DClickManager : MonoBehaviour
{
    public static Model3DClickManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private LayerMask model3DLayer;
    [SerializeField] private float     maxRayDistance = 100f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // ────────────────────────────────────────────────
    private readonly List<Model3DPrefab> registeredModels = new List<Model3DPrefab>();
    private bool          collidersEnabled  = true;
    private Model3DPrefab currentHovered    = null;   // ← track hover hiện tại

    // ════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (!collidersEnabled) return;

        // ── Hover mỗi frame ───────────────────────────
        HandleHover();

        // ── Click ─────────────────────────────────────
        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUI())
            {
                if (showDebug)
                    Debug.Log("[Model3DClickManager] Click blocked by UI");
                return;
            }

            HandleClick();
        }
    }

    // ════════════════════════════════════════════════
    // HOVER
    // ════════════════════════════════════════════════

    private void HandleHover()
    {
        // Không hover khi đang trỏ vào UI
        if (IsPointerOverUI())
        {
            ClearHover();
            return;
        }

        Camera cam = Camera.main;
        if (cam == null) return;

        Ray        ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxRayDistance, model3DLayer))
        {
            Model3DPrefab hovered = hit.collider.GetComponentInParent<Model3DPrefab>();

            if (hovered != null && registeredModels.Contains(hovered))
            {
                // Đổi target hover
                if (currentHovered != hovered)
                {
                    ClearHover();                  // exit cái cũ
                    currentHovered = hovered;
                    currentHovered.OnHoverEnter(); // enter cái mới

                    if (showDebug)
                        Debug.Log($"[Model3DClickManager] HoverEnter → {hovered.name}");
                }
                return; // ← vẫn đang hover, không clear
            }
        }

        // Raycast miss hoặc hit object không phải Model3D
        ClearHover();
    }

    private void ClearHover()
    {
        if (currentHovered == null) return;

        if (showDebug)
            Debug.Log($"[Model3DClickManager] HoverExit → {currentHovered.name}");

        currentHovered.OnHoverExit();
        currentHovered = null;
    }

    // ════════════════════════════════════════════════
    // CLICK
    // ════════════════════════════════════════════════

    private void HandleClick()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Ray        ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, maxRayDistance, model3DLayer))
        {
            if (showDebug)
                Debug.Log($"[Model3DClickManager] Hit: {hit.collider.gameObject.name}");

            Model3DPrefab model3D = hit.collider.GetComponentInParent<Model3DPrefab>();

            if (model3D != null && registeredModels.Contains(model3D))
            {
                model3D.OnInfoColliderClicked();

                if (showDebug)
                    Debug.Log($"[Model3DClickManager] Clicked: {model3D.GetData()?.name}");
            }
        }
    }

    // ════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;

        if (Input.touchCount > 0)
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);

        return EventSystem.current.IsPointerOverGameObject();
    }

    // ════════════════════════════════════════════════
    // PUBLIC API
    // ════════════════════════════════════════════════

    /// <summary>Bật/tắt toàn bộ click + hover</summary>
    public void SetCollidersEnabled(bool enabled)
    {
        collidersEnabled = enabled;

        // Nếu disable thì clear hover ngay
        if (!enabled) ClearHover();

        if (showDebug)
            Debug.Log($"[Model3DClickManager] collidersEnabled = {enabled}");
    }

    public void RegisterModel3D(Model3DPrefab model3D)
    {
        if (model3D == null || registeredModels.Contains(model3D)) return;

        registeredModels.Add(model3D);

        if (showDebug)
            Debug.Log($"[Model3DClickManager] Registered: {model3D.GetData()?.name}");
    }

    public void UnregisterModel3D(Model3DPrefab model3D)
    {
        if (model3D == null) return;

        // Nếu đang hover cái bị unregister thì clear luôn
        if (currentHovered == model3D)
        {
            currentHovered.OnHoverExit();
            currentHovered = null;
        }

        registeredModels.Remove(model3D);

        if (showDebug)
            Debug.Log($"[Model3DClickManager] Unregistered: {model3D.GetData()?.name}");
    }
}