using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Model3DTransformEditPopup : MonoBehaviour
{
    private static Model3DTransformEditPopup _instance;
    public static Model3DTransformEditPopup Instance => _instance;

    [Header("UI References")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private TMP_InputField modelIdInput;
    [SerializeField] private TMP_InputField modelNameInput;
    [SerializeField] private TMP_InputField posXInput;
    [SerializeField] private TMP_InputField posYInput;
    [SerializeField] private TMP_InputField posZInput;
    [SerializeField] private TMP_InputField rotXInput;
    [SerializeField] private TMP_InputField rotYInput;
    [SerializeField] private TMP_InputField rotZInput;
    [SerializeField] private TMP_InputField scaleXInput;
    [SerializeField] private TMP_InputField scaleYInput;
    [SerializeField] private TMP_InputField scaleZInput;
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
    private int currentModelId;
    private Model3D currentModelData;
    private Model3DPrefab targetModelPrefab;

    // Transform tracking
    private Vector3 originalPosition;
    private Vector3 originalRotation;
    private Vector3 originalScale;
    private Vector3 newPosition;
    private Vector3 newRotation;
    private Vector3 newScale;
    private bool hasChanges = false;

    // Input handling
    private float nextUpdateTime;
    private bool isPopulating = false;
    public Action<String> onGizmoModeChanged;

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
            Debug.Log("[Model3DTransformEditPopup] Initialized");

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
        // Position
        if (posXInput != null)
            posXInput.onValueChanged.AddListener(val => OnPositionChanged());
        if (posYInput != null)
            posYInput.onValueChanged.AddListener(val => OnPositionChanged());
        if (posZInput != null)
            posZInput.onValueChanged.AddListener(val => OnPositionChanged());

        // Rotation
        if (rotXInput != null)
            rotXInput.onValueChanged.AddListener(val => OnRotationChanged());
        if (rotYInput != null)
            rotYInput.onValueChanged.AddListener(val => OnRotationChanged());
        if (rotZInput != null)
            rotZInput.onValueChanged.AddListener(val => OnRotationChanged());

        // Scale
        if (scaleXInput != null)
            scaleXInput.onValueChanged.AddListener(val => OnScaleChanged());
        if (scaleYInput != null)
            scaleYInput.onValueChanged.AddListener(val => OnScaleChanged());
        if (scaleZInput != null)
            scaleZInput.onValueChanged.AddListener(val => OnScaleChanged());
    }
    public void OnUpdateGizmoMode(string mode)
    {
        if (gizmoModeText == null) return;
        if (mode == "Move")
            gizmoModeText.text = "Chế độ chỉnh sửa: Di chuyển";
        else if (mode == "Rotate")
            gizmoModeText.text = "Chế độ chỉnh sửa: Xoay";
        else if (mode == "Scale")
            gizmoModeText.text = "Chế độ chỉnh sửa: Tỉ lệ";
    }

    #endregion

    #region Show/Hide

    public void Show(int modelId, Model3D modelData, Model3DPrefab modelPrefab)
    {
        if (modelData == null || modelPrefab == null)
        {
            Debug.LogError("[Model3DTransformEditPopup] Invalid parameters!");
            return;
        }

        currentModelId = modelId;
        currentModelData = modelData;
        targetModelPrefab = modelPrefab;

        // Store original transform
        originalPosition = modelPrefab.transform.position;
        originalRotation = modelPrefab.transform.eulerAngles;
        
        GameObject glb = modelPrefab.GetLoadedGLB();
        originalScale = glb != null ? glb.transform.localScale : Vector3.one;

        // Reset tracking
        newPosition = originalPosition;
        newRotation = originalRotation;
        newScale = originalScale;
        hasChanges = false;

        // Populate UI
        PopulateInputFields();

        // Show panel
        if (popupPanel != null)
            popupPanel.SetActive(true);

        UpdateStatus("Chỉnh sửa transform... (Drag gizmo hoặc nhập số)", Color.white);

        if (showDebug)
            Debug.Log($"[Model3DTransformEditPopup] Showing for model: {modelData.name} (ID: {modelId})");

        modelPrefab.onDisplayButton.Invoke(true);
    }

    public void Hide()
    {
        if (popupPanel != null)
            popupPanel.SetActive(false);

        // Deactivate gizmo
        if (targetModelPrefab != null)
        {
            RuntimeTransformGizmo gizmo = targetModelPrefab.GetGizmo();
            if (gizmo != null && gizmo.IsActive)
            {
                gizmo.Deactivate();
            }
        }

        // Clear references
        targetModelPrefab = null;
        currentModelData = null;

        if (showDebug)
            Debug.Log("[Model3DTransformEditPopup] Hidden");
        
        
    }

    #endregion

    #region Populate UI

    private void PopulateInputFields()
    {
        isPopulating = true;

        // Model info (read-only)
        if (modelIdInput != null)
        {
            modelIdInput.text = currentModelId.ToString();
            modelIdInput.interactable = false;
        }

        if (modelNameInput != null)
        {
            modelNameInput.text = currentModelData.name;
            modelNameInput.interactable = false;
        }

        // Position
        if (posXInput != null) posXInput.text = originalPosition.x.ToString("F3");
        if (posYInput != null) posYInput.text = originalPosition.y.ToString("F3");
        if (posZInput != null) posZInput.text = originalPosition.z.ToString("F3");

        // Rotation
        if (rotXInput != null) rotXInput.text = originalRotation.x.ToString("F2");
        if (rotYInput != null) rotYInput.text = originalRotation.y.ToString("F2");
        if (rotZInput != null) rotZInput.text = originalRotation.z.ToString("F2");

        // Scale
        if (scaleXInput != null) scaleXInput.text = originalScale.x.ToString("F3");
        if (scaleYInput != null) scaleYInput.text = originalScale.y.ToString("F3");
        if (scaleZInput != null) scaleZInput.text = originalScale.z.ToString("F3");

        isPopulating = false;

        if (showDebug)
            Debug.Log("[Model3DTransformEditPopup] Input fields populated");
    }

    #endregion

    #region Input Callbacks

    private void OnPositionChanged()
    {
        if (isPopulating || !updateInRealtime || targetModelPrefab == null)
            return;

        if (Time.time < nextUpdateTime)
            return;

        nextUpdateTime = Time.time + updateDelay;

        if (float.TryParse(posXInput.text, out float x) &&
            float.TryParse(posYInput.text, out float y) &&
            float.TryParse(posZInput.text, out float z))
        {
            newPosition = new Vector3(x, y, z);
            targetModelPrefab.transform.position = newPosition;
            hasChanges = true;

            if (showDebug)
                Debug.Log($"[Model3DTransformEditPopup] Position changed: {newPosition}");
        }
    }

    private void OnRotationChanged()
    {
        if (isPopulating || !updateInRealtime || targetModelPrefab == null)
            return;

        if (Time.time < nextUpdateTime)
            return;

        nextUpdateTime = Time.time + updateDelay;

        if (float.TryParse(rotXInput.text, out float x) &&
            float.TryParse(rotYInput.text, out float y) &&
            float.TryParse(rotZInput.text, out float z))
        {
            newRotation = new Vector3(x, y, z);
            targetModelPrefab.transform.eulerAngles = newRotation;
            hasChanges = true;

            if (showDebug)
                Debug.Log($"[Model3DTransformEditPopup] Rotation changed: {newRotation}");
        }
    }

    private void OnScaleChanged()
    {
        if (isPopulating || !updateInRealtime || targetModelPrefab == null)
            return;

        if (Time.time < nextUpdateTime)
            return;

        nextUpdateTime = Time.time + updateDelay;

        GameObject glb = targetModelPrefab.GetLoadedGLB();
        if (glb == null) return;

        if (float.TryParse(scaleXInput.text, out float x) &&
            float.TryParse(scaleYInput.text, out float y) &&
            float.TryParse(scaleZInput.text, out float z))
        {
            newScale = new Vector3(x, y, z);
            glb.transform.localScale = newScale;
            hasChanges = true;

            if (showDebug)
                Debug.Log($"[Model3DTransformEditPopup] Scale changed: {newScale}");
        }
    }

    #endregion

    #region Gizmo Integration

    public void UpdateFromGizmo(Vector3 position, Vector3 rotation)
    {
        if (isPopulating)
            return;

        isPopulating = true;

        // Update position inputs
        if (posXInput != null) posXInput.text = position.x.ToString("F3");
        if (posYInput != null) posYInput.text = position.y.ToString("F3");
        if (posZInput != null) posZInput.text = position.z.ToString("F3");

        // Update rotation inputs
        if (rotXInput != null) rotXInput.text = rotation.x.ToString("F2");
        if (rotYInput != null) rotYInput.text = rotation.y.ToString("F2");
        if (rotZInput != null) rotZInput.text = rotation.z.ToString("F2");

        isPopulating = false;

        newPosition = position;
        newRotation = rotation;
        hasChanges = true;

        if (showDebug)
            Debug.Log($"[Model3DTransformEditPopup] Updated from gizmo: pos={position}, rot={rotation}");
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
            Debug.Log($"[Model3DTransformEditPopup] Saving transform for model {currentModelId}");

        UpdateStatus("Đang lưu...", Color.yellow);

        // Update position object
        if (currentModelData.position == null)
        {
            currentModelData.position = new Position();
        }
        currentModelData.position.x = newPosition.x;
        currentModelData.position.y = newPosition.y;
        currentModelData.position.z = newPosition.z;

        // Update rotation object
        if (currentModelData.rotate == null)
        {
            currentModelData.rotate = new Rotation();
        }
        currentModelData.rotate.x = newRotation.x;
        currentModelData.rotate.y = newRotation.y;
        currentModelData.rotate.z = newRotation.z;

        // Update size object
        if (currentModelData.size == null)
        {
            currentModelData.size = new Size();
        }
        currentModelData.size.x = newScale.x;
        currentModelData.size.y = newScale.y;
        currentModelData.size.z = newScale.z;

        // Update prefab data
        if (targetModelPrefab != null)
        {
            targetModelPrefab.UpdateDataFromTransform();
        }

        // TODO: Call API to update
        APIManager.Instance.UpdateModel3DTransform(
            currentModelId,
            currentModelData.position,
            currentModelData.rotate,
            currentModelData.size,
            OnSaveSuccess,
            OnSaveError
        );
    }

    private void OnSaveSuccess()
    {
        UpdateStatus("Đã lưu thành công!", Color.green);

        if (showDebug)
            Debug.Log("[Model3DTransformEditPopup] Transform saved successfully");

        originalPosition = newPosition;
        originalRotation = newRotation;
        originalScale = newScale;
        hasChanges = false;

        Invoke(nameof(Hide), 1f);
        targetModelPrefab.onDisplayButton.Invoke(false);
    }

    private void OnSaveError(string error)
    {
        UpdateStatus($"Lỗi: {error}", Color.red);
        Debug.LogError($"[Model3DTransformEditPopup] Save failed: {error}");
    }

    private void OnCancelClicked()
    {
        if (hasChanges)
        {
            if (targetModelPrefab != null)
            {
                targetModelPrefab.transform.position = originalPosition;
                targetModelPrefab.transform.eulerAngles = originalRotation;
                
                GameObject glb = targetModelPrefab.GetLoadedGLB();
                if (glb != null)
                {
                    glb.transform.localScale = originalScale;
                }
            }

            if (showDebug)
                Debug.Log("[Model3DTransformEditPopup] Changes cancelled, restored original transform");
        }
        targetModelPrefab.onDisplayButton.Invoke(false);

        Hide();
    }

    private void OnResetClicked()
    {
        if (targetModelPrefab != null)
        {
            targetModelPrefab.transform.position = originalPosition;
            targetModelPrefab.transform.eulerAngles = originalRotation;
            
            GameObject glb = targetModelPrefab.GetLoadedGLB();
            if (glb != null)
            {
                glb.transform.localScale = originalScale;
            }
        }

        PopulateInputFields();
        hasChanges = false;

        UpdateStatus("Đã reset về giá trị ban đầu", Color.white);

        if (showDebug)
            Debug.Log("[Model3DTransformEditPopup] Reset to original transform");
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
