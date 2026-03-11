using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PaintingRemoveConfirmPopup : MonoBehaviour
{
    public static PaintingRemoveConfirmPopup Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI messageText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button closeButton;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    private System.Action onConfirmCallback;
    private Painting currentPainting;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        SetupButtons();
        Hide();
    }

    private void SetupButtons()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveAllListeners();
            cancelButton.onClick.AddListener(OnCancelClicked);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnCancelClicked);
        }
    }

    public void Show(Painting painting, System.Action onConfirm)
    {
        if (painting == null)
        {
            Debug.LogError("[PaintingRemoveConfirmPopup] Painting is null!");
            return;
        }

        currentPainting = painting;
        onConfirmCallback = onConfirm;

        //  Hiển thị thông tin painting
        if (titleText != null)
        {
            titleText.text = "Remove Painting Confirmation";
        }

        if (messageText != null)
        {
            messageText.text = $"Are you sure you want to remove:\n\n<b>{painting.name}</b>?\n\nThis action cannot be undone.";
        }

        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
        }

        if (showDebug)
            Debug.Log($"[PaintingRemoveConfirmPopup]  Popup shown for: {painting.name}");
    }

    public void Hide()
    {
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }

        currentPainting = null;
        onConfirmCallback = null;

        if (showDebug)
            Debug.Log("[PaintingRemoveConfirmPopup] Popup hidden");
    }

    private void OnConfirmClicked()
    {
        if (showDebug)
            Debug.Log("[PaintingRemoveConfirmPopup]  Confirm clicked");

        onConfirmCallback?.Invoke();
        Hide();
    }

    private void OnCancelClicked()
    {
        if (showDebug)
            Debug.Log("[PaintingRemoveConfirmPopup]  Cancel clicked");

        Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
