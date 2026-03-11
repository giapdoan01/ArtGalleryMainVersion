using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// PaintingTransformEditPopup - Popup để chỉnh sửa transform của Painting
/// Đồng bộ với RuntimeTransformGizmo
/// </summary>
public class PaintingTransformEditPopup : MonoBehaviour
{
    private static PaintingTransformEditPopup _instance;
    public static PaintingTransformEditPopup Instance => _instance;

    [Header("UI References")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private TMP_InputField paintingIdInput;
    [SerializeField] private TMP_InputField paintingNameInput;
    [SerializeField] private TMP_InputField posXInput;
    [SerializeField] private TMP_InputField posYInput;
    [SerializeField] private TMP_InputField posZInput;
    [SerializeField] private TMP_InputField rotXInput;
    [SerializeField] private TMP_InputField rotYInput;
    [SerializeField] private TMP_InputField rotZInput;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button resetButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI gizmoModeText;

    [Header("Transform Preview Settings")]
    [SerializeField] private bool updateInRealtime = true;
    [SerializeField] private float updateDelay = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // Data storage
    private int currentPaintingId;
    private Painting currentPaintingData;
    private PaintingPrefab targetPaintingPrefab;

    // Transform tracking
    private Vector3 originalPosition;
    private Vector3 originalRotation;
    private Vector3 newPosition;
    private Vector3 newRotation;
    private bool hasChanges = false;

    // Input handling
    private float nextUpdateTime;
    private bool isPopulating = false;
    public Action<string> onGizmoModeChanged;

    #region Unity Lifecycle

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        SetupButtons();
        SetupInputFields();
        Hide();

        if (showDebug)
            Debug.Log("[PaintingTransformEditPopup] Initialized");
        onGizmoModeChanged += OnUpdateGizmoMode;
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();
        onGizmoModeChanged -= OnUpdateGizmoMode;
    }

    #endregion

    #region Setup

    private void SetupButtons()
    {
        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);

        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetClicked);
    }

    private void RemoveButtonListeners()
    {
        if (saveButton != null)
            saveButton.onClick.RemoveListener(OnSaveClicked);

        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelClicked);

        if (resetButton != null)
            resetButton.onClick.RemoveListener(OnResetClicked);
    }

    private void SetupInputFields()
    {
        if (posXInput != null)
            posXInput.onValueChanged.AddListener(val => OnPositionChanged());

        if (posYInput != null)
            posYInput.onValueChanged.AddListener(val => OnPositionChanged());

        if (posZInput != null)
            posZInput.onValueChanged.AddListener(val => OnPositionChanged());

        if (rotXInput != null)
            rotXInput.onValueChanged.AddListener(val => OnRotationChanged());

        if (rotYInput != null)
            rotYInput.onValueChanged.AddListener(val => OnRotationChanged());

        if (rotZInput != null)
            rotZInput.onValueChanged.AddListener(val => OnRotationChanged());
    }
    public void OnUpdateGizmoMode(string mode)
    {
        if (mode == "Move")
        {
            gizmoModeText.text = "Chế độ chỉnh sửa: Di chuyển";
        }
        else if (mode == "Rotate")
        {
            gizmoModeText.text = "Chế độ chỉnh sửa: Xoay";
        }
        else if (mode == "Scale")
        {
            gizmoModeText.text = "Chế độ chỉnh sửa: Tỉ lệ";
        }
    }

    #endregion

    #region Show/Hide

    public void Show(int paintingId, Painting paintingData, PaintingPrefab paintingPrefab)
    {
        if (paintingData == null || paintingPrefab == null)
        {
            Debug.LogError("[PaintingTransformEditPopup] Invalid parameters!");
            return;
        }

        currentPaintingId = paintingId;
        currentPaintingData = paintingData;
        targetPaintingPrefab = paintingPrefab;

        // Store original transform
        originalPosition = paintingPrefab.transform.position;
        originalRotation = paintingPrefab.transform.eulerAngles;

        // Reset tracking
        newPosition = originalPosition;
        newRotation = originalRotation;
        hasChanges = false;

        // Populate UI
        PopulateInputFields();

        // Show panel
        if (popupPanel != null)
            popupPanel.SetActive(true);

        UpdateStatus("Chỉnh sửa transform... (Drag gizmo hoặc nhập số)", Color.white);

        if (showDebug)
            Debug.Log($"[PaintingTransformEditPopup] Showing for painting: {paintingData.name} (ID: {paintingId})");
        paintingPrefab.onDisplayButton.Invoke(true);
    }

    public void Hide()
    {
        if (popupPanel != null)
            popupPanel.SetActive(false);

        // Deactivate gizmo
        if (targetPaintingPrefab != null)
        {
            RuntimeTransformGizmo gizmo = targetPaintingPrefab.GetGizmo();
            if (gizmo != null && gizmo.IsActive)
            {
                gizmo.Deactivate();
            }
        }

        // Clear references
        targetPaintingPrefab = null;
        currentPaintingData = null;

        if (showDebug)
            Debug.Log("[PaintingTransformEditPopup] Hidden");
    }

    #endregion

    #region Populate UI

    private void PopulateInputFields()
    {
        isPopulating = true;

        // Painting info (read-only)
        if (paintingIdInput != null)
        {
            paintingIdInput.text = currentPaintingId.ToString();
            paintingIdInput.interactable = false;
        }

        if (paintingNameInput != null)
        {
            paintingNameInput.text = currentPaintingData.name;
            paintingNameInput.interactable = false;
        }

        // Position
        if (posXInput != null)
            posXInput.text = originalPosition.x.ToString("F3");

        if (posYInput != null)
            posYInput.text = originalPosition.y.ToString("F3");

        if (posZInput != null)
            posZInput.text = originalPosition.z.ToString("F3");

        // Rotation
        if (rotXInput != null)
            rotXInput.text = originalRotation.x.ToString("F2");

        if (rotYInput != null)
            rotYInput.text = originalRotation.y.ToString("F2");

        if (rotZInput != null)
            rotZInput.text = originalRotation.z.ToString("F2");

        isPopulating = false;

        if (showDebug)
            Debug.Log("[PaintingTransformEditPopup] Input fields populated");
    }

    #endregion

    #region Input Callbacks

    private void OnPositionChanged()
    {
        if (isPopulating || !updateInRealtime || targetPaintingPrefab == null)
            return;

        if (Time.time < nextUpdateTime)
            return;

        nextUpdateTime = Time.time + updateDelay;

        if (float.TryParse(posXInput.text, out float x) &&
            float.TryParse(posYInput.text, out float y) &&
            float.TryParse(posZInput.text, out float z))
        {
            newPosition = new Vector3(x, y, z);
            targetPaintingPrefab.transform.position = newPosition;
            hasChanges = true;

            if (showDebug)
                Debug.Log($"[PaintingTransformEditPopup] Position changed from input: {newPosition}");
        }
    }

    private void OnRotationChanged()
    {
        if (isPopulating || !updateInRealtime || targetPaintingPrefab == null)
            return;

        if (Time.time < nextUpdateTime)
            return;

        nextUpdateTime = Time.time + updateDelay;

        if (float.TryParse(rotXInput.text, out float x) &&
            float.TryParse(rotYInput.text, out float y) &&
            float.TryParse(rotZInput.text, out float z))
        {
            newRotation = new Vector3(x, y, z);
            targetPaintingPrefab.transform.eulerAngles = newRotation;
            hasChanges = true;

            if (showDebug)
                Debug.Log($"[PaintingTransformEditPopup] Rotation changed from input: {newRotation}");
        }
    }

    #endregion

    #region Gizmo Integration

    public void UpdateFromGizmo(Vector3 position, Vector3 rotation)
    {
        if (isPopulating)
            return;

        isPopulating = true;

        if (posXInput != null) posXInput.text = position.x.ToString("F3");
        if (posYInput != null) posYInput.text = position.y.ToString("F3");
        if (posZInput != null) posZInput.text = position.z.ToString("F3");

        if (rotXInput != null) rotXInput.text = rotation.x.ToString("F2");
        if (rotYInput != null) rotYInput.text = rotation.y.ToString("F2");
        if (rotZInput != null) rotZInput.text = rotation.z.ToString("F2");

        isPopulating = false;

        newPosition = position;
        newRotation = rotation;
        hasChanges = true;
    }

    #endregion

    #region Button Callbacks

    private void OnSaveClicked()
    {
        if (!hasChanges)
        {
            UpdateStatus("Không có thay đổi!", Color.yellow);
            return;
        }

        if (showDebug)
            Debug.Log($"[PaintingTransformEditPopup] Saving transform for painting {currentPaintingId}");

        UpdateStatus("Đang lưu...", Color.yellow);

        //  Update position object
        if (currentPaintingData.position == null)
        {
            currentPaintingData.position = new Position();
        }
        currentPaintingData.position.x = newPosition.x;
        currentPaintingData.position.y = newPosition.y;
        currentPaintingData.position.z = newPosition.z;

        //  Update rotation object
        if (currentPaintingData.rotate == null)
        {
            currentPaintingData.rotate = new Rotation();
        }
        currentPaintingData.rotate.x = newRotation.x;
        currentPaintingData.rotate.y = newRotation.y;
        currentPaintingData.rotate.z = newRotation.z;

        //  Update prefab data
        if (targetPaintingPrefab != null)
        {
            targetPaintingPrefab.UpdateDataFromTransform();
        }

        //  TODO: Call API to update
        APIManager.Instance.UpdatePaintingTransform(
            currentPaintingId,
            currentPaintingData.position,
            currentPaintingData.rotate,
            OnSaveSuccess,
            OnSaveError
            );
        OnSaveSuccess();
        targetPaintingPrefab.onDisplayButton.Invoke(false);
    }

    private void OnSaveSuccess()
    {
        UpdateStatus("Đã lưu thành công!", Color.green);

        if (showDebug)
            Debug.Log("[PaintingTransformEditPopup] Transform saved successfully");

        originalPosition = newPosition;
        originalRotation = newRotation;
        hasChanges = false;

        Invoke(nameof(Hide), 1f);
    }

    private void OnSaveError(string error)
    {
        UpdateStatus($"Lỗi: {error}", Color.red);
        Debug.LogError($"[PaintingTransformEditPopup] Save failed: {error}");
    }

    private void OnCancelClicked()
    {
        if (hasChanges)
        {
            if (targetPaintingPrefab != null)
            {
                targetPaintingPrefab.transform.position = originalPosition;
                targetPaintingPrefab.transform.eulerAngles = originalRotation;
            }

            if (showDebug)
                Debug.Log("[PaintingTransformEditPopup] Changes cancelled, restored original transform");
        }
        targetPaintingPrefab.onDisplayButton.Invoke(false);
        Hide();
    }

    private void OnResetClicked()
    {
        if (targetPaintingPrefab != null)
        {
            targetPaintingPrefab.transform.position = originalPosition;
            targetPaintingPrefab.transform.eulerAngles = originalRotation;
        }

        PopulateInputFields();
        hasChanges = false;

        UpdateStatus("Đã reset về giá trị ban đầu", Color.white);

        if (showDebug)
            Debug.Log("[PaintingTransformEditPopup] Reset to original transform");
    }

    #endregion

    #region Utility

    private void UpdateStatus(string message, Color color)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.color = color;
        }
    }

    #endregion
}
