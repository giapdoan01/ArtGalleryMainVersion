using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MaxPainting : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject maxPanel;
    [SerializeField] private Image      paintingImage;
    [SerializeField] private Button     closeButton;

    [Header("Image Size Limits")]
    [SerializeField] private float maxWidth  = 800f;
    [SerializeField] private float maxHeight = 800f;

    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed    = 100f;
    [SerializeField] private float maxZoomHeight = 1080f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // ────────────────────────────────────────────────
    private float minZoomHeight;   // = finalH lúc FitImage (set khi Show)
    private float currentHeight;   // height hiện tại của image

    // ════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════

    private void Awake()
    {
        if (maxPanel != null)
            maxPanel.SetActive(false);
    }

    private void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseButtonClicked);
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);
    }

    private void Update()
    {
        if (maxPanel == null || !maxPanel.activeSelf) return;
        if (paintingImage == null) return;

        HandleZoom();
    }

    // ════════════════════════════════════════════════
    // PUBLIC API
    // ════════════════════════════════════════════════

    public void Show(Texture2D texture)
    {
        if (texture == null)
        {
            Debug.LogWarning("[MaxPainting] Texture is null!");
            return;
        }

        if (maxPanel != null)
            maxPanel.SetActive(true);

        ApplyTexture(texture);

        if (showDebug)
            Debug.Log($"[MaxPainting] Showing texture: {texture.width}x{texture.height}");
    }

    public void Hide()
    {
        if (maxPanel != null)
            maxPanel.SetActive(false);

        if (showDebug)
            Debug.Log("[MaxPainting] Panel hidden");
    }

    // ════════════════════════════════════════════════
    // CLOSE BUTTON — chặn khi có UI phía trước
    // ════════════════════════════════════════════════

    private void OnCloseButtonClicked()
    {
        // Kiểm tra xem có UI nào khác đang phủ lên closeButton không
        if (IsBlockedByOtherUI())
        {
            if (showDebug)
                Debug.Log("[MaxPainting] Close button blocked by UI on top");
            return;
        }

        Hide();
    }

    /// <summary>
    /// Raycast UI tại vị trí chuột — nếu element đầu tiên hit KHÔNG phải
    /// closeButton (hoặc con của nó) thì coi như bị chặn.
    /// </summary>
    private bool IsBlockedByOtherUI()
    {
        if (EventSystem.current == null) return false;

        var results = new System.Collections.Generic.List<RaycastResult>();
        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        EventSystem.current.RaycastAll(pointerData, results);

        if (results.Count == 0) return false;

        // Element trên cùng (index 0) phải là closeButton hoặc con của nó
        GameObject topObject = results[0].gameObject;
        return !IsChildOf(topObject, closeButton.gameObject);
    }

    /// <summary>Kiểm tra obj có phải là parent hoặc chính nó không</summary>
    private bool IsChildOf(GameObject obj, GameObject parent)
    {
        Transform t = obj.transform;
        while (t != null)
        {
            if (t.gameObject == parent) return true;
            t = t.parent;
        }
        return false;
    }

    // ════════════════════════════════════════════════
    // ZOOM
    // ════════════════════════════════════════════════

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) < 0.001f) return;

        RectTransform imageRect = paintingImage.GetComponent<RectTransform>();
        if (imageRect == null) return;

        // Lấy aspect ratio từ size hiện tại để giữ tỉ lệ
        float aspect = imageRect.sizeDelta.x / imageRect.sizeDelta.y;

        currentHeight = Mathf.Clamp(
            currentHeight + scroll * zoomSpeed,
            minZoomHeight,
            maxZoomHeight
        );

        float newWidth = currentHeight * aspect;
        imageRect.sizeDelta = new Vector2(newWidth, currentHeight);

        if (showDebug)
            Debug.Log($"[MaxPainting] Zoom → {newWidth:F0}x{currentHeight:F0}");
    }

    // ════════════════════════════════════════════════
    // PRIVATE
    // ════════════════════════════════════════════════

    private void ApplyTexture(Texture2D texture)
    {
        if (paintingImage == null) return;

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f)
        );
        paintingImage.sprite = sprite;

        FitImageWithMaxSize(texture.width, texture.height);
    }

    private void FitImageWithMaxSize(float texWidth, float texHeight)
    {
        if (paintingImage == null) return;

        RectTransform imageRect = paintingImage.GetComponent<RectTransform>();
        if (imageRect == null) return;

        float texAspect = texWidth / texHeight;

        float finalW = maxWidth;
        float finalH = maxWidth / texAspect;

        if (finalH > maxHeight)
        {
            finalH = maxHeight;
            finalW = maxHeight * texAspect;
        }

        imageRect.sizeDelta = new Vector2(finalW, finalH);

        // ✅ Ghi nhớ min zoom = kích thước vừa fit xong
        minZoomHeight = finalH;
        currentHeight = finalH;

        if (showDebug)
            Debug.Log($"[MaxPainting] Image sized: {finalW:F0}x{finalH:F0} " +
                      $"(tex: {texWidth}x{texHeight}, aspect: {texAspect:F2}) " +
                      $"| zoom [{minZoomHeight:F0} ~ {maxZoomHeight:F0}]");
    }
}