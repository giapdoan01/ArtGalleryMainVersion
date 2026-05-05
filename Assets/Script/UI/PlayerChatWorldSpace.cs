using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Gắn lên prefab player. Hiển thị bubble chat trên đầu avatar kiểu VLTK:
/// - Hiện ngay khi nhận chat, tồn tại displayDuration giây, sau đó fade out.
/// - Nếu chat mới đến trước khi fade xong → cập nhật text + reset timer (không chớp nháy).
/// - Tự đối mặt với camera mỗi frame (billboard).
/// </summary>
public class PlayerChatWorldSpace : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup  canvasGroup;
    [SerializeField] private Image        background;
    [SerializeField] private TextMeshProUGUI chatText;

    [Header("Timing")]
    [SerializeField] private float displayDuration = 4f;
    [SerializeField] private float fadeOutDuration  = 0.8f;

    // Owner identity — set bởi PlayerView khi Initialize
    private string _ownerUsername;
    private bool   _isLocalPlayer;

    private Transform  _cameraTransform;
    private Coroutine  _displayCoroutine;

    // ════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════

    private void Awake()
    {
        HideImmediate();
    }

    private void OnEnable()
    {
        if (ChatNetworkHandler.Instance != null)
            ChatNetworkHandler.Instance.OnChatMessageReceived += OnChatReceived;
    }

    private void OnDisable()
    {
        if (ChatNetworkHandler.Instance != null)
            ChatNetworkHandler.Instance.OnChatMessageReceived -= OnChatReceived;
    }

    private void Update()
    {
        BillboardToCamera();
    }

    // ════════════════════════════════════════════════
    // INIT
    // ════════════════════════════════════════════════

    /// <summary>Gọi từ PlayerView.Initialize sau khi có username và isLocal.</summary>
    public void Initialize(string username, bool isLocal)
    {
        _ownerUsername = username;
        _isLocalPlayer = isLocal;

        if (isLocal)
        {
            // Local player: lấy camera ngay
            _cameraTransform = Camera.main?.transform;
        }
    }

    // ════════════════════════════════════════════════
    // CHAT EVENT
    // ════════════════════════════════════════════════

    private void OnChatReceived(string username, string message, long timestamp)
    {
        // Chỉ phản ứng với message của chính owner avatar này
        if (username != _ownerUsername) return;

        ShowChat(message);
    }

    /// <summary>Gọi trực tiếp từ bên ngoài nếu cần (vd: local player gửi chat).</summary>
    public void ShowChat(string message)
    {
        if (chatText != null)
            chatText.text = message;

        // Reset coroutine — nếu đang fade thì huỷ và bắt đầu lại timer
        if (_displayCoroutine != null)
            StopCoroutine(_displayCoroutine);

        _displayCoroutine = StartCoroutine(DisplayRoutine());
    }

    // ════════════════════════════════════════════════
    // DISPLAY COROUTINE
    // ════════════════════════════════════════════════

    private IEnumerator DisplayRoutine()
    {
        // Hiện ngay, alpha = 1
        canvasGroup.alpha = 1f;
        if (background != null) background.enabled = true;
        if (chatText   != null) chatText.enabled   = true;

        // Đợi displayDuration
        yield return new WaitForSeconds(displayDuration);

        // Fade out
        float elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeOutDuration);
            yield return null;
        }

        HideImmediate();
        _displayCoroutine = null;
    }

    // ════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════

    private void HideImmediate()
    {
        canvasGroup.alpha = 0f;
        if (background != null) background.enabled = false;
        if (chatText   != null) chatText.enabled   = false;
    }

    private void BillboardToCamera()
    {
        if (canvasGroup.alpha <= 0f) return;

        if (_cameraTransform == null)
            _cameraTransform = Camera.main?.transform;

        if (_cameraTransform == null) return;

        transform.LookAt(_cameraTransform);
        transform.Rotate(0f, 180f, 0f);
    }
}
