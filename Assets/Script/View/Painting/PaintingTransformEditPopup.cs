using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PaintingTransformEditPopup : MonoBehaviour
{
    private static PaintingTransformEditPopup _instance;
    public static PaintingTransformEditPopup Instance => _instance;

    [Header("UI References")]
    [SerializeField] private GameObject       popupPanel;
    [SerializeField] private TMP_InputField   paintingIdInput;
    [SerializeField] private TMP_InputField   paintingNameInput;
    [SerializeField] private TMP_InputField   posXInput;
    [SerializeField] private TMP_InputField   posYInput;
    [SerializeField] private TMP_InputField   posZInput;
    [SerializeField] private TMP_InputField   rotXInput;
    [SerializeField] private TMP_InputField   rotYInput;
    [SerializeField] private TMP_InputField   rotZInput;
    [SerializeField] private Button           saveButton;
    [SerializeField] private Button           cancelButton;
    [SerializeField] private Button           resetButton;
    [SerializeField] private TextMeshProUGUI  statusText;
    [SerializeField] private TextMeshProUGUI  gizmoModeText;

    [Header("Transform Preview Settings")]
    [SerializeField] private bool  updateInRealtime = true;
    [SerializeField] private float updateDelay      = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // ── Data ─────────────────────────────────────────
    private int            currentPaintingId;
    private Painting       currentPaintingData;
    private PaintingPrefab targetPaintingPrefab;

    // ── Transform tracking ───────────────────────────
    private Vector3 originalPosition;
    private Vector3 originalRotation;
    private Vector3 newPosition;
    private Vector3 newRotation;
    private bool    hasChanges  = false;

    // ── Input handling ───────────────────────────────
    private float nextUpdateTime;
    private bool  isPopulating = false;

    //  Custom delegate — tránh Action<string> gây invoke_iiii mismatch trên WebGL IL2CPP
    public delegate void GizmoModeChangedHandler(string mode);
    public GizmoModeChangedHandler onGizmoModeChanged;

    // ════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════

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

        //  Subscribe bằng named method — không dùng lambda
        onGizmoModeChanged += OnUpdateGizmoMode;

        if (showDebug)
            Debug.Log("[PaintingTransformEditPopup] Initialized");
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();
        RemoveInputListeners();
        onGizmoModeChanged -= OnUpdateGizmoMode;
    }

    // ════════════════════════════════════════════════
    // SETUP
    // ════════════════════════════════════════════════

    private void SetupButtons()
    {
        if (saveButton   != null) saveButton.onClick.AddListener(OnSaveClicked);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
        if (resetButton  != null) resetButton.onClick.AddListener(OnResetClicked);
    }

    private void RemoveButtonListeners()
    {
        if (saveButton   != null) saveButton.onClick.RemoveListener(OnSaveClicked);
        if (cancelButton != null) cancelButton.onClick.RemoveListener(OnCancelClicked);
        if (resetButton  != null) resetButton.onClick.RemoveListener(OnResetClicked);
    }

    private void SetupInputFields()
    {
        //  Named method thay lambda — tránh anonymous closure gây signature mismatch trên WebGL
        if (posXInput != null) posXInput.onValueChanged.AddListener(OnPosXChanged);
        if (posYInput != null) posYInput.onValueChanged.AddListener(OnPosYChanged);
        if (posZInput != null) posZInput.onValueChanged.AddListener(OnPosZChanged);
        if (rotXInput != null) rotXInput.onValueChanged.AddListener(OnRotXChanged);
        if (rotYInput != null) rotYInput.onValueChanged.AddListener(OnRotYChanged);
        if (rotZInput != null) rotZInput.onValueChanged.AddListener(OnRotZChanged);
    }

    private void RemoveInputListeners()
    {
        if (posXInput != null) posXInput.onValueChanged.RemoveListener(OnPosXChanged);
        if (posYInput != null) posYInput.onValueChanged.RemoveListener(OnPosYChanged);
        if (posZInput != null) posZInput.onValueChanged.RemoveListener(OnPosZChanged);
        if (rotXInput != null) rotXInput.onValueChanged.RemoveListener(OnRotXChanged);
        if (rotYInput != null) rotYInput.onValueChanged.RemoveListener(OnRotYChanged);
        if (rotZInput != null) rotZInput.onValueChanged.RemoveListener(OnRotZChanged);
    }

    // ── Input field named callbacks ──────────────────
    private void OnPosXChanged(string _) => OnPositionChanged();
    private void OnPosYChanged(string _) => OnPositionChanged();
    private void OnPosZChanged(string _) => OnPositionChanged();
    private void OnRotXChanged(string _) => OnRotationChanged();
    private void OnRotYChanged(string _) => OnRotationChanged();
    private void OnRotZChanged(string _) => OnRotationChanged();

    // ════════════════════════════════════════════════
    // GIZMO MODE
    // ════════════════════════════════════════════════

    public void OnUpdateGizmoMode(string mode)
    {
        if (gizmoModeText == null) return;

        switch (mode)
        {
            case "Move":   gizmoModeText.text = "Chế độ chỉnh sửa: Di chuyển"; break;
            case "Rotate": gizmoModeText.text = "Chế độ chỉnh sửa: Xoay";      break;
            case "Scale":  gizmoModeText.text = "Chế độ chỉnh sửa: Tỉ lệ";     break;
            default:       gizmoModeText.text = $"Chế độ: {mode}";              break;
        }
    }

    // ════════════════════════════════════════════════
    // SHOW / HIDE
    // ════════════════════════════════════════════════

    public void Show(int paintingId, Painting paintingData, PaintingPrefab paintingPrefab)
    {
        if (paintingData == null || paintingPrefab == null)
        {
            Debug.LogError("[PaintingTransformEditPopup] Invalid parameters!");
            return;
        }

        currentPaintingId   = paintingId;
        currentPaintingData = paintingData;
        targetPaintingPrefab = paintingPrefab;

        originalPosition = paintingPrefab.transform.position;
        originalRotation = paintingPrefab.transform.eulerAngles;

        newPosition = originalPosition;
        newRotation = originalRotation;
        hasChanges  = false;

        PopulateInputFields();

        if (popupPanel != null)
            popupPanel.SetActive(true);

        UpdateStatus("Chỉnh sửa transform... (Drag gizmo hoặc nhập số)", Color.white);

        //  Invoke custom delegate — an toàn trên WebGL
        paintingPrefab.onDisplayButton.Invoke(true);

        if (showDebug)
            Debug.Log($"[PaintingTransformEditPopup] Showing for: {paintingData.name} (ID: {paintingId})");
    }

    public void Hide()
    {
        if (popupPanel != null)
            popupPanel.SetActive(false);

        if (targetPaintingPrefab != null)
        {
            RuntimeTransformGizmo gizmo = targetPaintingPrefab.GetGizmo();
            if (gizmo != null && gizmo.IsActive)
                gizmo.Deactivate();
        }

        targetPaintingPrefab = null;
        currentPaintingData  = null;

        if (showDebug)
            Debug.Log("[PaintingTransformEditPopup] Hidden");
    }

    // ════════════════════════════════════════════════
    // POPULATE UI
    // ════════════════════════════════════════════════

    private void PopulateInputFields()
    {
        isPopulating = true;

        if (paintingIdInput != null)
        {
            paintingIdInput.text        = currentPaintingId.ToString();
            paintingIdInput.interactable = false;
        }

        if (paintingNameInput != null)
        {
            paintingNameInput.text        = currentPaintingData.name;
            paintingNameInput.interactable = false;
        }

        if (posXInput != null) posXInput.text = originalPosition.x.ToString("F3");
        if (posYInput != null) posYInput.text = originalPosition.y.ToString("F3");
        if (posZInput != null) posZInput.text = originalPosition.z.ToString("F3");

        if (rotXInput != null) rotXInput.text = originalRotation.x.ToString("F2");
        if (rotYInput != null) rotYInput.text = originalRotation.y.ToString("F2");
        if (rotZInput != null) rotZInput.text = originalRotation.z.ToString("F2");

        isPopulating = false;

        if (showDebug)
            Debug.Log("[PaintingTransformEditPopup] Input fields populated");
    }

    // ════════════════════════════════════════════════
    // INPUT CALLBACKS
    // ════════════════════════════════════════════════

    private void OnPositionChanged()
    {
        if (isPopulating || !updateInRealtime || targetPaintingPrefab == null) return;
        if (Time.time < nextUpdateTime) return;

        nextUpdateTime = Time.time + updateDelay;

        if (float.TryParse(posXInput.text, out float x) &&
            float.TryParse(posYInput.text, out float y) &&
            float.TryParse(posZInput.text, out float z))
        {
            newPosition = new Vector3(x, y, z);
            targetPaintingPrefab.transform.position = newPosition;
            hasChanges = true;

            if (showDebug)
                Debug.Log($"[PaintingTransformEditPopup] Position from input: {newPosition}");
        }
    }

    private void OnRotationChanged()
    {
        if (isPopulating || !updateInRealtime || targetPaintingPrefab == null) return;
        if (Time.time < nextUpdateTime) return;

        nextUpdateTime = Time.time + updateDelay;

        if (float.TryParse(rotXInput.text, out float x) &&
            float.TryParse(rotYInput.text, out float y) &&
            float.TryParse(rotZInput.text, out float z))
        {
            newRotation = new Vector3(x, y, z);
            targetPaintingPrefab.transform.eulerAngles = newRotation;
            hasChanges = true;

            if (showDebug)
                Debug.Log($"[PaintingTransformEditPopup] Rotation from input: {newRotation}");
        }
    }

    // ════════════════════════════════════════════════
    // GIZMO INTEGRATION
    // ════════════════════════════════════════════════

    public void UpdateFromGizmo(Vector3 position, Vector3 rotation)
    {
        if (isPopulating) return;

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
        hasChanges  = true;
    }

    // ════════════════════════════════════════════════
    // BUTTON CALLBACKS
    // ════════════════════════════════════════════════

    private void OnSaveClicked()
    {
        if (!hasChanges)
        {
            UpdateStatus("Không có thay đổi!", Color.yellow);
            return;
        }

        UpdateStatus("Đang lưu...", Color.yellow);

        if (currentPaintingData.position == null)
            currentPaintingData.position = new Position();

        currentPaintingData.position.x = newPosition.x;
        currentPaintingData.position.y = newPosition.y;
        currentPaintingData.position.z = newPosition.z;

        if (currentPaintingData.rotate == null)
            currentPaintingData.rotate = new Rotation();

        currentPaintingData.rotate.x = newRotation.x;
        currentPaintingData.rotate.y = newRotation.y;
        currentPaintingData.rotate.z = newRotation.z;

        targetPaintingPrefab?.UpdateDataFromTransform();

        APIManager.Instance.UpdatePaintingTransform(
            currentPaintingId,
            currentPaintingData.position,
            currentPaintingData.rotate,
            OnSaveSuccess,
            OnSaveError
        );

        if (showDebug)
            Debug.Log($"[PaintingTransformEditPopup] Saving transform for ID: {currentPaintingId}");
    }

    private void OnSaveSuccess()
    {
        UpdateStatus("Đã lưu thành công!", Color.green);

        originalPosition = newPosition;
        originalRotation = newRotation;
        hasChanges       = false;

        //  Invoke custom delegate — an toàn trên WebGL
        targetPaintingPrefab?.onDisplayButton.Invoke(false);

        Invoke(nameof(Hide), 1f);

        if (showDebug)
            Debug.Log("[PaintingTransformEditPopup] Saved successfully");
    }

    private void OnSaveError(string error)
    {
        UpdateStatus($"Lỗi: {error}", Color.red);
        Debug.LogError($"[PaintingTransformEditPopup] Save failed: {error}");
    }

    private void OnCancelClicked()
    {
        if (hasChanges && targetPaintingPrefab != null)
        {
            targetPaintingPrefab.transform.position    = originalPosition;
            targetPaintingPrefab.transform.eulerAngles = originalRotation;

            if (showDebug)
                Debug.Log("[PaintingTransformEditPopup] Cancelled — restored original transform");
        }

        //  Invoke custom delegate — an toàn trên WebGL
        targetPaintingPrefab?.onDisplayButton.Invoke(false);
        Hide();
    }

    private void OnResetClicked()
    {
        if (targetPaintingPrefab != null)
        {
            targetPaintingPrefab.transform.position    = originalPosition;
            targetPaintingPrefab.transform.eulerAngles = originalRotation;
        }

        PopulateInputFields();
        hasChanges = false;

        UpdateStatus("Đã reset về giá trị ban đầu", Color.white);

        if (showDebug)
            Debug.Log("[PaintingTransformEditPopup] Reset to original");
    }

    // ════════════════════════════════════════════════
    // UTILITY
    // ════════════════════════════════════════════════

    private void UpdateStatus(string message, Color color)
    {
        if (statusText == null) return;
        statusText.text  = message;
        statusText.color = color;
    }
}
