using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Model3DRemoveConfirmPopup : MonoBehaviour
{
    public static Model3DRemoveConfirmPopup Instance { get; private set; }

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
    private Model3D currentModel3D;

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

    public void Show(Model3D model3D, System.Action onConfirm)
    {
        if (model3D == null)
        {
            Debug.LogError("[Model3DRemoveConfirmPopup] Model3D is null!");
            return;
        }

        currentModel3D = model3D;
        onConfirmCallback = onConfirm;

        //  Hiển thị thông tin model3D
        if (titleText != null)
        {
            titleText.text = "Remove Model Confirmation";
        }

        if (messageText != null)
        {
            messageText.text = $"Are you sure you want to remove:\n\n<b>{model3D.name}</b>?\n\nThis action cannot be undone.";
        }

        if (popupPanel != null)
        {
            popupPanel.SetActive(true);
        }

        if (showDebug)
            Debug.Log($"[Model3DRemoveConfirmPopup]  Popup shown for: {model3D.name}");
    }

    public void Hide()
    {
        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }

        currentModel3D = null;
        onConfirmCallback = null;

        if (showDebug)
            Debug.Log("[Model3DRemoveConfirmPopup] Popup hidden");
    }

    private void OnConfirmClicked()
    {
        if (showDebug)
            Debug.Log("[Model3DRemoveConfirmPopup]  Confirm clicked");

        onConfirmCallback?.Invoke();
        Hide();
    }

    private void OnCancelClicked()
    {
        if (showDebug)
            Debug.Log("[Model3DRemoveConfirmPopup] Cancel clicked");

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
