using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PreviewModel3D : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject previewModel3DPanel;
    [SerializeField] private Button     closeButton;
    [SerializeField] private Button     closeButton2;

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
        // Không gọi previewModel3DPanel.SetActive(false) ở đây — nếu PreviewModel3D
        // nằm trên chính panel, Show() gọi SetActive(true) sẽ trigger Awake và tự ẩn lại.
        // Hãy đảm bảo previewModel3DPanel được set inactive trong Inspector/Prefab.

        if (closeButton  != null) closeButton.onClick.AddListener(Hide);
        if (closeButton2 != null) closeButton2.onClick.AddListener(Hide);

        if (renderTextureDisplay != null && renderTexture != null)
            renderTextureDisplay.texture = renderTexture;

        // ✅ Đảm bảo RawImage block raycast — không cho event xuyên xuống closeButton
        if (renderTextureDisplay != null)
        {
            renderTextureDisplay.raycastTarget = true;

            EventTrigger trigger = renderTextureDisplay.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = renderTextureDisplay.gameObject.AddComponent<EventTrigger>();

            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener((_) => { /* Chặn click — không close */ });
            trigger.triggers.Add(entry);
        }
    }

    private void OnDestroy()
    {
        if (closeButton  != null) closeButton.onClick.RemoveListener(Hide);
        if (closeButton2 != null) closeButton2.onClick.RemoveListener(Hide);
    }

    // ════════════════════════════════════════════════
    // PUBLIC API
    // ════════════════════════════════════════════════

    private Model3DRotate currentRotate;

    public void Show(Transform targetModel)
    {
        if (previewModel3DPanel != null)
            previewModel3DPanel.SetActive(true);

        if (model3DCamera != null)
            model3DCamera.SetTarget(targetModel);

        currentRotate = targetModel != null ? targetModel.GetComponentInChildren<Model3DRotate>() : null;
        if (currentRotate != null) currentRotate.Stop();

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

        if (currentRotate != null)
        {
            currentRotate.Resume();
            currentRotate = null;
        }

        if (showDebug)
            Debug.Log("[PreviewModel3D] Panel hidden");
    }
}