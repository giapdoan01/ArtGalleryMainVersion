using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PreviewModel3D : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject previewModel3DPanel;
    [SerializeField] private Button     closeButton;

    [Header("Render Texture Output")]
    [SerializeField] private RawImage     renderTextureDisplay;
    [SerializeField] private RenderTexture renderTexture;

    [Header("Camera")]
    [SerializeField] private Model3DCamera model3DCamera;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // ════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════

    private void Awake()
    {
        if (previewModel3DPanel != null)
            previewModel3DPanel.SetActive(false);

        // ✅ Đảm bảo RawImage block raycast — không cho event xuyên xuống closeButton
        if (renderTextureDisplay != null)
        {
            renderTextureDisplay.raycastTarget = true;

            // ✅ Thêm GraphicRaycaster blocker nếu chưa có EventTrigger
            // Dùng EventTrigger để bắt click trên RawImage mà KHÔNG close panel
            EventTrigger trigger = renderTextureDisplay.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = renderTextureDisplay.gameObject.AddComponent<EventTrigger>();

            // Block PointerClick — không làm gì cả, chỉ để chặn event bubble xuống
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener((_) => { /* Chặn click — không close */ });
            trigger.triggers.Add(entry);
        }
    }

    private void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (renderTextureDisplay != null && renderTexture != null)
            renderTextureDisplay.texture = renderTexture;
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Hide);
    }

    // ════════════════════════════════════════════════
    // PUBLIC API
    // ════════════════════════════════════════════════

    public void Show(Transform targetModel)
    {
        if (previewModel3DPanel != null)
            previewModel3DPanel.SetActive(true);

        if (model3DCamera != null)
            model3DCamera.SetTarget(targetModel);

        if (showDebug)
            Debug.Log($"[PreviewModel3D] Showing preview for: {targetModel?.name ?? "null"}");
    }

    public void Hide()
    {
        if (previewModel3DPanel != null)
            previewModel3DPanel.SetActive(false);

        // ✅ Restore layer của target về Default khi đóng
        if (model3DCamera != null)
            model3DCamera.ClearTarget();

        if (showDebug)
            Debug.Log("[PreviewModel3D] Panel hidden");
    }
}