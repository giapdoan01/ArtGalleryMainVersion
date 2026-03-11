using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class PaintingInputInfo : MonoBehaviour
{
    private static PaintingInputInfo instance;
    public static PaintingInputInfo Instance => instance;

    [Header("UI Components")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Dropdown frameTypeDropdown;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TextMeshProUGUI paintingNameText;

    [Header("Frame Type Options")]
    [SerializeField] private string[] frameTypeValues = { "landscape", "portrait" };
    [SerializeField] private string[] frameTypeLabels = { "Khung ngang (Landscape)", "Khung dọc (Portrait)" };

    [Header("Spawn Settings")]
    [SerializeField] private float spawnDistanceFromPlayer = 2f;
    [SerializeField] private float spawnHeightOffset = 2f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // Current painting data
    private Painting currentPainting;
    private Texture2D currentTexture;
    private string selectedFrameType = "landscape";
    
    //  THÊM: Callback để notify PaintingItem
    private Action<Painting> onPaintingSaved;

    #region Unity Lifecycle

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        SetupDropdown();
        SetupButtons();
        Hide();
    }

    private void OnDestroy()
    {
        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(OnSaveClicked);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(OnCancelClicked);
        }

        if (frameTypeDropdown != null)
        {
            frameTypeDropdown.onValueChanged.RemoveListener(OnFrameTypeChanged);
        }
    }

    #endregion

    #region Setup

    private void SetupDropdown()
    {
        if (frameTypeDropdown == null)
        {
            Debug.LogError("[PaintingInputInfo] frameTypeDropdown is not assigned!");
            return;
        }

        frameTypeDropdown.ClearOptions();
        frameTypeDropdown.AddOptions(new System.Collections.Generic.List<string>(frameTypeLabels));
        frameTypeDropdown.value = 0;
        selectedFrameType = frameTypeValues[0];

        frameTypeDropdown.onValueChanged.RemoveListener(OnFrameTypeChanged);
        frameTypeDropdown.onValueChanged.AddListener(OnFrameTypeChanged);

        if (showDebug)
            Debug.Log("[PaintingInputInfo] Dropdown setup complete");
    }

    private void SetupButtons()
    {
        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(OnSaveClicked);
            saveButton.onClick.AddListener(OnSaveClicked);
        }
        else
        {
            Debug.LogError("[PaintingInputInfo] saveButton is not assigned!");
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(OnCancelClicked);
            cancelButton.onClick.AddListener(OnCancelClicked);
        }
        else
        {
            Debug.LogError("[PaintingInputInfo] cancelButton is not assigned!");
        }

        if (showDebug)
            Debug.Log("[PaintingInputInfo] Buttons setup complete");
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Hiển thị panel để chọn frame_type
    ///  THÊM callback để notify khi save xong
    /// </summary>
    public void Show(Painting painting, Texture2D texture, Action<Painting> onSaveCallback = null)
    {
        if (painting == null || texture == null)
        {
            Debug.LogError("[PaintingInputInfo] Invalid painting or texture!");
            return;
        }

        currentPainting = painting;
        currentTexture = texture;
        onPaintingSaved = onSaveCallback; //  Lưu callback

        // Update UI
        if (paintingNameText != null)
        {
            string displayName = painting.name;
            if (painting.paintings_lang?.vi != null && !string.IsNullOrEmpty(painting.paintings_lang.vi.name))
            {
                displayName = painting.paintings_lang.vi.name;
            }
            paintingNameText.text = displayName;
        }

        // Reset dropdown to default
        if (frameTypeDropdown != null)
        {
            frameTypeDropdown.value = 0;
            selectedFrameType = frameTypeValues[0];
        }

        // Show panel
        if (panel != null)
        {
            panel.SetActive(true);
        }

        if (showDebug)
            Debug.Log($"[PaintingInputInfo] Showing panel for: {painting.name}");
    }

    /// <summary>
    /// Ẩn panel
    /// </summary>
    public void Hide()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }

        currentPainting = null;
        currentTexture = null;
        onPaintingSaved = null; //  Clear callback

        if (showDebug)
            Debug.Log("[PaintingInputInfo] Panel hidden");
    }

    #endregion

    #region Button Callbacks

    private void OnFrameTypeChanged(int index)
    {
        if (index >= 0 && index < frameTypeValues.Length)
        {
            selectedFrameType = frameTypeValues[index];

            if (showDebug)
                Debug.Log($"[PaintingInputInfo] Frame type selected: {selectedFrameType} ({frameTypeLabels[index]})");
        }
    }

    private void OnSaveClicked()
    {
        if (currentPainting == null || currentTexture == null)
        {
            Debug.LogError("[PaintingInputInfo] No painting data to save!");
            return;
        }

        if (showDebug)
            Debug.Log($"[PaintingInputInfo] Saving painting: {currentPainting.name} with frame_type: {selectedFrameType}");

        //  Tính position và rotation dựa trên Player
        CalculateSpawnTransform(out Position position, out Rotation rotation);

        //  PATCH lên server
        PatchPaintingData(position, rotation);
    }

    private void OnCancelClicked()
    {
        if (showDebug)
            Debug.Log("[PaintingInputInfo] Cancelled");

        Hide();
    }

    #endregion

    #region Spawn Transform Calculation

    private void CalculateSpawnTransform(out Position position, out Rotation rotation)
    {
        PlayerController player = FindObjectOfType<PlayerController>();

        if (player == null)
        {
            Debug.LogError("[PaintingInputInfo] PlayerController not found! Using default position.");
            
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Vector3 cameraPos = mainCamera.transform.position;
                Vector3 cameraForward = mainCamera.transform.forward;

                Vector3 spawnPos = cameraPos + cameraForward * spawnDistanceFromPlayer;
                spawnPos.y = cameraPos.y;

                position = new Position
                {
                    x = spawnPos.x,
                    y = spawnPos.y,
                    z = spawnPos.z
                };

                Vector3 lookDirection = (cameraPos - spawnPos).normalized;
                Quaternion lookRotation = Quaternion.LookRotation(lookDirection);

                rotation = new Rotation
                {
                    x = lookRotation.eulerAngles.x,
                    y = lookRotation.eulerAngles.y,
                    z = lookRotation.eulerAngles.z
                };

                if (showDebug)
                    Debug.Log($"[PaintingInputInfo] Using Camera position: {spawnPos}, rotation: {lookRotation.eulerAngles}");

                return;
            }
            else
            {
                Debug.LogError("[PaintingInputInfo] Camera not found! Using origin.");
                position = new Position { x = 0, y = 2, z = 0 };
                rotation = new Rotation { x = 0, y = 0, z = 0 };
                return;
            }
        }

        Transform playerTransform = player.transform;

        Vector3 spawnPosition = playerTransform.position + playerTransform.forward * spawnDistanceFromPlayer;
        spawnPosition.y += spawnHeightOffset;

        position = new Position
        {
            x = spawnPosition.x,
            y = spawnPosition.y,
            z = spawnPosition.z
        };

        Vector3 directionToPlayer = (playerTransform.position - spawnPosition).normalized;
        Quaternion spawnRotation = Quaternion.LookRotation(directionToPlayer);

        rotation = new Rotation
        {
            x = 0,
            y = spawnRotation.eulerAngles.y,
            z = 0
        };

        if (showDebug)
        {
            Debug.Log($"[PaintingInputInfo] Player position: {playerTransform.position}");
            Debug.Log($"[PaintingInputInfo] Spawn position: {spawnPosition}");
            Debug.Log($"[PaintingInputInfo] Spawn rotation: {spawnRotation.eulerAngles}");
        }
    }

    #endregion

    #region API Patch

    private void PatchPaintingData(Position position, Rotation rotation)
    {
        if (APIManager.Instance == null)
        {
            Debug.LogError("[PaintingInputInfo] APIManager not found!");
            return;
        }

        if (showDebug)
        {
            Debug.Log($"[PaintingInputInfo] Patching painting {currentPainting.name}:");
            Debug.Log($"  frame_type: {selectedFrameType}");
            Debug.Log($"  is_used: 1");
            Debug.Log($"  position: ({position.x}, {position.y}, {position.z})");
            Debug.Log($"  rotation: ({rotation.x}, {rotation.y}, {rotation.z})");
        }

        //  Update local data trước
        currentPainting.frame_type = selectedFrameType;
        currentPainting.is_used = 1;
        currentPainting.position = position;
        currentPainting.rotate = rotation;

        //  PATCH lên server
        APIManager.Instance.PatchPaintingWithFrameType(
            currentPainting.id,
            1,
            selectedFrameType,
            position,
            rotation,
            () => {
                //  Success callback
                if (showDebug)
                    Debug.Log($"[PaintingInputInfo]  Successfully saved painting: {currentPainting.name}");

                //  QUAN TRỌNG: Gọi callback để update PaintingItem
                onPaintingSaved?.Invoke(currentPainting);

                //  Hide panel
                Hide();

                //  Reload paintings để spawn painting mới
                if (PaintingPrefabManager.Instance != null)
                {
                    PaintingPrefabManager.Instance.ReloadAllPaintings();
                }
            },
            (error) => {
                //  Error callback
                Debug.LogError($"[PaintingInputInfo] ❌ Failed to save painting: {error}");
            }
        );
    }

    #endregion
}
