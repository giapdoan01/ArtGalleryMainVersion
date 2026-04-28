using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class CombinedCursorManager : MonoBehaviour
{
    public static CombinedCursorManager Instance { get; private set; }

    [Header("Cursor Sprite")]
    [SerializeField] private Sprite cursorClickSprite;

    [Header("Cursor Settings")]
    [SerializeField] private float cursorScale = 1f;
    [SerializeField] private Vector2 cursorOffset = Vector2.zero;

    [Header("Transition Settings")]
    [SerializeField] private float fadeSpeed = 10f;
    [SerializeField] private float scaleInSpeed = 8f;

    [Header("Raycast Settings")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float maxRaycastDistance = 100f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;
    [SerializeField] private bool drawRaycast = false;

    // UI Components
    private Canvas cursorCanvas;
    private Image cursorImage;
    private RectTransform cursorRect;
    private CanvasGroup cursorCanvasGroup;

    // State tracking
    private bool isOverClickable = false;
    private CursorState currentState = CursorState.Default;
    private GameObject lastHoveredObject;

    // Animation state
    private float targetAlpha = 0f;
    private float currentAlpha = 0f;
    private float targetScale = 0.8f;
    private float currentScale = 0.8f;

    // Clickable tags
    private readonly string[] CLICKABLE_TAGS = { "Painting", "Model3D" };

    private Camera mainCamera;

    private enum CursorState
    {
        Default,   
        Ground,    
        Clickable   
    }

    #region Unity Lifecycle

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
            return;
        }

        mainCamera = Camera.main;
        if (targetCamera == null)
            targetCamera = mainCamera;

        CreateCursorCanvas();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (showDebug)
            Debug.Log("[CombinedCursorManager] Initialized");
    }

    private void LateUpdate()
    {
        UpdateCursorPosition();
        CheckForClickableObjects();
        UpdateCursorAnimation();

        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame)
                OnMousePressed();
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
                OnMouseReleased();
        }
    }

    private void OnDestroy() => Cursor.visible = true;
    private void OnApplicationQuit() => Cursor.visible = true;

    #endregion

    #region Cursor Setup

    private void CreateCursorCanvas()
    {
        GameObject canvasObj = new GameObject("CursorCanvas");
        canvasObj.transform.SetParent(transform);

        cursorCanvas = canvasObj.AddComponent<Canvas>();
        cursorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        cursorCanvas.sortingOrder = 32767;

        cursorCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
        cursorCanvasGroup.alpha = 0f;
        cursorCanvasGroup.interactable = false;
        cursorCanvasGroup.blocksRaycasts = false;

        GraphicRaycaster raycaster = canvasObj.AddComponent<GraphicRaycaster>();
        raycaster.enabled = false;

        GameObject imageObj = new GameObject("CursorImage");
        imageObj.transform.SetParent(canvasObj.transform, false);

        cursorImage = imageObj.AddComponent<Image>();
        cursorImage.sprite = cursorClickSprite;
        cursorImage.raycastTarget = false;

        cursorRect = imageObj.GetComponent<RectTransform>();
        cursorRect.anchorMin = Vector2.zero;
        cursorRect.anchorMax = Vector2.zero;
        cursorRect.pivot = new Vector2(0.5f, 0.5f);

        UpdateCursorSize();
        cursorCanvas.transform.SetAsLastSibling();
    }

    private void UpdateCursorSize()
    {
        if (cursorRect == null) return;

        cursorRect.sizeDelta = cursorClickSprite != null
            ? new Vector2(cursorClickSprite.rect.width, cursorClickSprite.rect.height) * cursorScale
            : new Vector2(32, 32) * cursorScale;
    }

    #endregion

    #region Cursor Position & Animation

    private void UpdateCursorPosition()
    {
        if (cursorRect == null || Mouse.current == null) return;

        cursorRect.anchoredPosition = Mouse.current.position.ReadValue() + cursorOffset;
    }

    private void UpdateCursorAnimation()
    {
        if (cursorCanvasGroup == null || cursorRect == null) return;

        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);
        cursorCanvasGroup.alpha = currentAlpha;

        currentScale = Mathf.Lerp(currentScale, targetScale, Time.deltaTime * scaleInSpeed);
        cursorRect.localScale = Vector3.one * currentScale;
    }

    private void OnMousePressed()
    {
        if (isOverClickable) targetScale = 0.85f;
    }

    private void OnMouseReleased()
    {
        if (isOverClickable) targetScale = 1f;
    }

    #endregion

    #region Clickable Object Detection

    private void CheckForClickableObjects()
    {
        isOverClickable = false;
        GameObject hitObject = null;

        bool isBlockedByUI = IsPointerBlockedByUI();

        if (isBlockedByUI)
        {
            if (IsPointerOverUIButton(out hitObject))
            {
                isOverClickable = true;

                if (showDebug && hitObject != lastHoveredObject)
                    Debug.Log($"[CombinedCursorManager] Hover Button: {hitObject.name}");

                lastHoveredObject = hitObject;
            }
            else
            {
                if (lastHoveredObject != null)
                {
                    if (showDebug) Debug.Log("[CombinedCursorManager] Blocked by UI");
                    lastHoveredObject = null;
                }
            }
        }
        else
        {
            if (IsPointerOver3DClickableObject(out hitObject))
            {
                isOverClickable = true;

                if (showDebug && hitObject != lastHoveredObject)
                    Debug.Log($"[CombinedCursorManager] Hover 3D: {hitObject.name}");

                lastHoveredObject = hitObject;
            }
            else
            {
                if (lastHoveredObject != null)
                {
                    if (showDebug) Debug.Log("[CombinedCursorManager] Exit hover");
                    lastHoveredObject = null;
                }
            }
        }

        //  ĐÂY LÀ NƠI DUY NHẤT QUYẾT ĐỊNH Cursor.visible
        ApplyCursorState();
    }

    /// <summary>
    ///  Hàm duy nhất quyết định Cursor.visible và custom cursor
    /// Chạy trong LateUpdate → luôn là quyết định CUỐI CÙNG trong frame
    /// </summary>
    private void ApplyCursorState()
    {
        if (isOverClickable)
        {
            // Priority 1: Hover Painting/Model3D/Button → Custom cursor
            if (currentState != CursorState.Clickable)
            {
                currentState = CursorState.Clickable;
                targetAlpha = 1f;
                targetScale = 1f;
                Cursor.visible = false;

                if (showDebug)
                    Debug.Log("[CombinedCursorManager] → CLICKABLE (Cursor.visible = false)");
            }
        }
        else if (PlayerController.IsShowingGroundCursor)
        {
            // Priority 2: Đang trên Ground → hiện cả system cursor lẫn cursorPreview
            if (currentState != CursorState.Ground)
            {
                currentState = CursorState.Ground;
                targetAlpha = 0f;
                targetScale = 0.8f;
                Cursor.visible = true;

                if (showDebug)
                    Debug.Log("[CombinedCursorManager] → GROUND (Cursor.visible = true)");
            }
        }
        else
        {
            // Priority 3: Không có gì → System cursor
            if (currentState != CursorState.Default)
            {
                currentState = CursorState.Default;
                targetAlpha = 0f;
                targetScale = 0.8f;
                Cursor.visible = true;

                if (showDebug)
                    Debug.Log("[CombinedCursorManager] → DEFAULT (Cursor.visible = true)");
            }
        }
    }

    private bool IsPointerBlockedByUI()
    {
        if (EventSystem.current == null || Mouse.current == null) return false;
        if (!EventSystem.current.IsPointerOverGameObject()) return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            if (result.gameObject.transform.IsChildOf(cursorCanvas.transform))
                continue;

            return true;
        }

        return false;
    }

    private bool IsPointerOverUIButton(out GameObject hitObject)
    {
        hitObject = null;

        if (EventSystem.current == null || Mouse.current == null) return false;
        if (!EventSystem.current.IsPointerOverGameObject()) return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Mouse.current.position.ReadValue()
        };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (var result in results)
        {
            if (result.gameObject.transform.IsChildOf(cursorCanvas.transform))
                continue;

            Button button = result.gameObject.GetComponent<Button>();
            if (button != null && button.interactable)
            {
                hitObject = result.gameObject;
                return true;
            }

            Button parentButton = result.gameObject.GetComponentInParent<Button>();
            if (parentButton != null && parentButton.interactable)
            {
                hitObject = parentButton.gameObject;
                return true;
            }
        }

        return false;
    }

    private bool IsPointerOver3DClickableObject(out GameObject hitObject)
    {
        hitObject = null;

        if (targetCamera == null || Mouse.current == null) return false;

        Ray ray = targetCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (drawRaycast)
            Debug.DrawRay(ray.origin, ray.direction * maxRaycastDistance, Color.yellow);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance))
        {
            if (IsClickableTag(hit.collider.gameObject.tag))
            {
                hitObject = hit.collider.gameObject;
                return true;
            }

            Transform parent = hit.collider.transform.parent;
            while (parent != null)
            {
                if (IsClickableTag(parent.tag))
                {
                    hitObject = parent.gameObject;
                    return true;
                }
                parent = parent.parent;
            }
        }

        return false;
    }

    private bool IsClickableTag(string tag)
    {
        foreach (string t in CLICKABLE_TAGS)
            if (tag == t) return true;
        return false;
    }

    #endregion

    #region Public API

    //  IsManagingCursor = đang hiện custom cursor (Clickable state)
    public bool IsManagingCursor => currentState == CursorState.Clickable;

    public bool IsPointerOverAnyUIElement()
    {
        if (EventSystem.current == null) return false;
        return EventSystem.current.IsPointerOverGameObject();
    }

    public void ForceSetDefaultCursor()
    {
        currentState = CursorState.Default;
        targetAlpha = 0f;
        Cursor.visible = true;
    }

    public GameObject GetHoveredObject() => lastHoveredObject;
    public bool IsOverClickable() => isOverClickable;
    public string GetCurrentState() => currentState.ToString();

    public void SetCursorSprite(Sprite clickSprite)
    {
        cursorClickSprite = clickSprite;
        if (cursorImage != null)
        {
            cursorImage.sprite = clickSprite;
            UpdateCursorSize();
        }
    }

    public void SetCursorScale(float scale) { cursorScale = scale; UpdateCursorSize(); }
    public void SetCursorOffset(Vector2 offset) => cursorOffset = offset;
    public void SetFadeSpeed(float speed) => fadeSpeed = speed;
    public void SetScaleSpeed(float speed) => scaleInSpeed = speed;

    public void SetCursorSystemEnabled(bool enabled)
    {
        if (enabled) ForceSetDefaultCursor();
        else { Cursor.visible = false; targetAlpha = 0f; }
    }

    #endregion
}
