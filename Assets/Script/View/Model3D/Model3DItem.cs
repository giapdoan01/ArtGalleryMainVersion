using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class Model3DItem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] public Image thumbnailImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI categoryText;
    [SerializeField] private GameObject isUsedFrame;
    [SerializeField] private Button selectButton;
    [SerializeField] private Button teleportButton;
    [SerializeField] private Image frameHighlight;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnDistanceFromPlayer = 3f;
    [SerializeField] private float spawnHeightOffset = 0f;
    
    [Header("Rotation Settings")]
    [SerializeField] private bool useModelOriginalRotation = true; //  Dùng rotation gốc từ GLB
    [SerializeField] private bool facePlayer = false; //  Có quay Y về phía player không

    [Header("Highlight Settings")]
    [SerializeField] private Color highlightColor = new Color(1f, 0.5f, 0f, 1f);

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    public Model3D model3DData;
    private Texture2D modelTexture;
    private Vector3 originalModelRotation; //  Rotation gốc từ GLB
    private bool hasLoadedRotation = false; //  Flag check đã load rotation chưa
    private Model3DPrefab model3DPrefabInstance;
    private Model3DInfo model3DInfo;
    private bool isSubscribedToEvent = false;

    private void OnEnable()
    {
        if (!isSubscribedToEvent)
        {
            Model3DPrefabManager.OnModel3DPrefabSpawned += OnModel3DPrefabSpawned;
            isSubscribedToEvent = true;
        }

        AdminModeManager.OnAdminModeChanged += ApplyAdminMode;
        ApplyAdminMode(AdminModeManager.Instance != null && AdminModeManager.Instance.IsAdmin);

        Model3DInfo.OnModel3DInfoShown  += OnModel3DInfoShown;
        Model3DInfo.OnModel3DInfoHidden += OnModel3DInfoHidden;
    }

    private void OnDisable()
    {
        if (isSubscribedToEvent)
        {
            Model3DPrefabManager.OnModel3DPrefabSpawned -= OnModel3DPrefabSpawned;
            isSubscribedToEvent = false;
        }

        AdminModeManager.OnAdminModeChanged -= ApplyAdminMode;

        Model3DInfo.OnModel3DInfoShown  -= OnModel3DInfoShown;
        Model3DInfo.OnModel3DInfoHidden -= OnModel3DInfoHidden;
    }

    private void OnModel3DInfoShown(int modelId)
    {
        if (frameHighlight == null) return;
        bool isSelected = model3DData != null && model3DData.id == modelId;
        frameHighlight.color = isSelected ? highlightColor : Color.white;
    }

    private void OnModel3DInfoHidden()
    {
        if (frameHighlight == null) return;
        frameHighlight.color = Color.white;
    }

    private void ApplyAdminMode(bool isAdmin)
    {
        if (isUsedFrame != null)
            isUsedFrame.SetActive(isAdmin && model3DData != null && model3DData.is_used == 1);

        if (selectButton != null)
            selectButton.gameObject.SetActive(isAdmin);
    }

    public void Setup(Model3D model3D, Texture2D pathTexture)
    {
        model3DData = model3D;
        modelTexture = pathTexture;

        // Gán name (ưu tiên tiếng Việt)
        if (nameText != null)
        {
            string displayName = model3D.name;
            if (model3D.model3ds_lang?.vi != null && !string.IsNullOrEmpty(model3D.model3ds_lang.vi.name))
            {
                displayName = model3D.model3ds_lang.vi.name;
            }
            nameText.text = displayName;

            if (showDebug)
                Debug.Log($"[Model3DItem] Name set: {displayName}");
        }

        // Gán category ID
        if (categoryText != null)
        {
            categoryText.text = $"Category: {model3D.category_id}";

            if (showDebug)
                Debug.Log($"[Model3DItem] Category set: {model3D.category_id}");
        }

        // Hiển thị isUsedFrame nếu is_used = 1
        if (isUsedFrame != null)
        {
            bool isAdmin = AdminModeManager.Instance != null && AdminModeManager.Instance.IsAdmin;
            isUsedFrame.SetActive(isAdmin && model3D.is_used == 1);
        }

        // Disable button nếu is_used = 1
        UpdateButtonState();

        // Chỉ set thumbnail nếu có pathTexture
        if (thumbnailImage != null && pathTexture != null)
        {
            Sprite sprite = Sprite.Create(
                pathTexture,
                new Rect(0, 0, pathTexture.width, pathTexture.height),
                new Vector2(0.5f, 0.5f)
            );
            thumbnailImage.sprite = sprite;

            if (showDebug)
                Debug.Log($"[Model3DItem] Thumbnail set from pathTexture");
        }

        // Setup button
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelectModel3D);
        }

        if (teleportButton != null)
        {
            teleportButton.onClick.RemoveAllListeners();
            teleportButton.onClick.AddListener(OnTeleportButtonClicked);
        }

        // Link prefab instance nếu đã spawn
        if (model3D.is_used == 1 && Model3DPrefabManager.Instance != null &&
            Model3DPrefabManager.Instance.IsModel3DSpawned(model3D.id))
        {
            FindModel3DPrefabInScene();
        }

        //  LẤY ROTATION GỐC TỪ GLB (nếu model đã được spawn trước đó)
        LoadOriginalRotationFromManager();

        if (showDebug)
            Debug.Log($"[Model3DItem] Setup complete: {model3D.name} (ID: {model3D.id}, is_used: {model3D.is_used})");
    }

    //  LẤY ROTATION GỐC TỪ Model3DPrefabManager
    private void LoadOriginalRotationFromManager()
    {
        if (Model3DPrefabManager.Instance == null)
        {
            Debug.LogWarning("[Model3DItem] Model3DPrefabManager not found! Using (0,0,0)");
            originalModelRotation = Vector3.zero;
            hasLoadedRotation = false;
            return;
        }

        //  Lấy rotation gốc từ manager
        originalModelRotation = Model3DPrefabManager.Instance.GetOriginalRotation(model3DData.id);
        hasLoadedRotation = true;

        if (showDebug)
            Debug.Log($"[Model3DItem] Original rotation for {model3DData.name}: {originalModelRotation}");
    }

    private void OnSelectModel3D()
    {
        if (model3DData.is_used == 1)
        {
            if (showDebug)
                Debug.Log($"[Model3DItem] Cannot select - already used: {model3DData.name}");
            return;
        }

        if (showDebug)
            Debug.Log($"[Model3DItem] Selected: {model3DData.name}");

        SpawnModel3D();
    }

    private void SpawnModel3D()
    {
        if (model3DData == null)
        {
            Debug.LogError("[Model3DItem] Invalid model3D data!");
            return;
        }

        if (showDebug)
            Debug.Log($"[Model3DItem] Spawning model3D: {model3DData.name}");

        //  Reload rotation trước khi spawn (để chắc chắn có rotation mới nhất)
        if (!hasLoadedRotation)
        {
            LoadOriginalRotationFromManager();
        }

        CalculateSpawnTransform(out Position position, out Rotation rotation);
        PatchModel3DData(position, rotation);
    }

    //  CALCULATE SPAWN TRANSFORM
    private void CalculateSpawnTransform(out Position position, out Rotation rotation)
    {
        PlayerController player = FindObjectOfType<PlayerController>();

        Vector3 spawnPos;
        Vector3 referenceForward;
        Vector3 referencePosition;

        // ===== TÍNH POSITION =====
        if (player == null)
        {
            if (showDebug)
                Debug.LogWarning("[Model3DItem] PlayerController not found! Using Camera.");

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                referencePosition = mainCamera.transform.position;
                referenceForward = mainCamera.transform.forward;
            }
            else
            {
                Debug.LogError("[Model3DItem] Camera not found! Using origin.");
                referencePosition = Vector3.zero;
                referenceForward = Vector3.forward;
            }
        }
        else
        {
            referencePosition = player.transform.position;
            referenceForward = player.transform.forward;
        }

        // Tính spawn position
        spawnPos = referencePosition + referenceForward * spawnDistanceFromPlayer;
        spawnPos.y = referencePosition.y + spawnHeightOffset;

        position = new Position { x = spawnPos.x, y = spawnPos.y, z = spawnPos.z };

        // ===== TÍNH ROTATION =====
        Vector3 finalRotation;

        if (useModelOriginalRotation)
        {
            //  DÙNG ROTATION GỐC TỪ GLB
            finalRotation = originalModelRotation;

            //  NẾU CẦN QUAY VỀ PHÍA PLAYER, CHỈ XOAY TRỤC Y
            if (facePlayer)
            {
                Vector3 directionToPlayer = (referencePosition - spawnPos).normalized;
                float yRotation = Quaternion.LookRotation(directionToPlayer).eulerAngles.y;
                
                // Giữ nguyên X và Z, chỉ thay đổi Y
                finalRotation.y = yRotation;
            }

            if (showDebug)
            {
                Debug.Log($"[Model3DItem] Using original GLB rotation: {finalRotation}");
                if (facePlayer)
                    Debug.Log($"[Model3DItem] Adjusted Y rotation to face player: {finalRotation.y}");
            }
        }
        else
        {
            //  TÍNH TOÁN ROTATION ĐỂ QUAY VỀ PHÍA PLAYER
            Vector3 directionToPlayer = (referencePosition - spawnPos).normalized;
            Quaternion spawnRotation = Quaternion.LookRotation(directionToPlayer);
            finalRotation = spawnRotation.eulerAngles;

            if (showDebug)
                Debug.Log($"[Model3DItem] Calculated rotation to face player: {finalRotation}");
        }

        rotation = new Rotation
        {
            x = finalRotation.x,
            y = finalRotation.y,
            z = finalRotation.z
        };

        if (showDebug)
        {
            Debug.Log($"[Model3DItem] Spawn Transform:");
            Debug.Log($"  Position: ({position.x:F2}, {position.y:F2}, {position.z:F2})");
            Debug.Log($"  Rotation: ({rotation.x:F2}, {rotation.y:F2}, {rotation.z:F2})");
        }
    }

    private void PatchModel3DData(Position position, Rotation rotation)
    {
        if (APIManager.Instance == null)
        {
            Debug.LogError("[Model3DItem] APIManager not found!");
            return;
        }

        if (showDebug)
        {
            Debug.Log($"[Model3DItem] Patching model3D {model3DData.name}:");
            Debug.Log($"  is_used: 1");
            Debug.Log($"  position: ({position.x}, {position.y}, {position.z})");
            Debug.Log($"  rotation: ({rotation.x}, {rotation.y}, {rotation.z})");
        }

        model3DData.is_used = 1;
        model3DData.position = position;
        model3DData.rotate = rotation;

        APIManager.Instance.PatchModel3DWithUsed(
            model3DData.id,
            1,
            position,
            rotation,
            model3DData.size ?? new Size { x = 1, y = 1, z = 1 },
            () =>
            {
                if (showDebug)
                    Debug.Log($"[Model3DItem] Successfully saved model3D: {model3DData.name}");

                OnModel3DSaved(model3DData);

                if (Model3DPrefabManager.Instance != null)
                {
                    Model3DPrefabManager.Instance.ReloadAllModels();
                }
            },
            (error) =>
            {
                Debug.LogError($"[Model3DItem] Failed to save model3D: {error}");
            }
        );
    }

    public void OnModel3DSaved(Model3D updatedModel3D)
    {
        if (showDebug)
            Debug.Log($"[Model3DItem] Model3D saved callback: {updatedModel3D.name}");

        model3DData.is_used = updatedModel3D.is_used;
        model3DData.position = updatedModel3D.position;
        model3DData.rotate = updatedModel3D.rotate;
        model3DData.size = updatedModel3D.size;

        UpdateButtonState();

        if (showDebug)
            Debug.Log($"[Model3DItem] UI updated: Button disabled, isUsedFrame shown");
    }

    private void OnModel3DPrefabSpawned(int modelId, Model3DPrefab prefab)
    {
        if (model3DData != null && model3DData.id == modelId)
        {
            model3DPrefabInstance = prefab;
            UpdateTeleportButtonState();

            if (showDebug)
                Debug.Log($"[Model3DItem] Prefab linked via event: {model3DData.name}");
        }
    }

    private void FindModel3DPrefabInScene()
    {
        if (model3DData == null) return;

        if (Model3DPrefabManager.Instance != null)
        {
            model3DPrefabInstance = Model3DPrefabManager.Instance.FindPrefabByID(model3DData.id);
            if (model3DPrefabInstance != null)
            {
                UpdateTeleportButtonState();
                return;
            }
        }

        // Fallback: tìm trong scene
        foreach (Model3DPrefab p in FindObjectsOfType<Model3DPrefab>())
        {
            if (p.GetData() != null && p.GetData().id == model3DData.id)
            {
                model3DPrefabInstance = p;
                break;
            }
        }

        UpdateTeleportButtonState();
    }

    private void OnTeleportButtonClicked()
    {
        if (model3DData == null) return;

        if (model3DData.is_used != 1)
        {
            Debug.LogWarning($"[Model3DItem] Model not in scene: {model3DData.name}");
            return;
        }

        if (model3DPrefabInstance == null)
            FindModel3DPrefabInScene();

        if (model3DPrefabInstance == null)
        {
            Debug.LogError($"[Model3DItem] Prefab not found: {model3DData.name} (ID: {model3DData.id})");
            return;
        }

        if (model3DPrefabInstance.GetTeleportPoint() == null)
        {
            Debug.LogWarning($"[Model3DItem] TeleportPoint not assigned: {model3DData.name}");
            return;
        }

        model3DPrefabInstance.TeleportPlayerToModel();

        if (showDebug)
            Debug.Log($"[Model3DItem] TeleportPlayerToModel: {model3DData.name}");

        if (model3DInfo != null)
            StartCoroutine(ShowModel3DInfoDelayed());
    }

    private IEnumerator ShowModel3DInfoDelayed()
    {
        yield return new WaitForSeconds(1f);
        model3DInfo.ShowInfoWithPrefab(model3DData, modelTexture, model3DPrefabInstance);

        if (showDebug)
            Debug.Log($"[Model3DItem] Model3DInfo shown for: {model3DData.name}");
    }

    private void UpdateTeleportButtonState()
    {
        if (teleportButton == null) return;

        bool isUsed      = model3DData != null && model3DData.is_used == 1;
        bool hasPrefab   = model3DPrefabInstance != null;
        bool hasTeleport = hasPrefab && model3DPrefabInstance.GetTeleportPoint() != null;

        teleportButton.gameObject.SetActive(isUsed);
        teleportButton.interactable = isUsed && hasPrefab && hasTeleport;
    }

    private void UpdateButtonState()
    {
        if (selectButton != null)
        {
            selectButton.interactable = (model3DData.is_used == 0);

            if (showDebug)
                Debug.Log($"[Model3DItem] Button interactable: {selectButton.interactable}");
        }

        if (isUsedFrame != null)
        {
            bool isAdmin = AdminModeManager.Instance != null && AdminModeManager.Instance.IsAdmin;
            isUsedFrame.SetActive(isAdmin && model3DData.is_used == 1);

            if (showDebug)
                Debug.Log($"[Model3DItem] isUsedFrame active: {isUsedFrame.activeSelf}");
        }

        UpdateTeleportButtonState();
    }

    public void OnModel3DRemoved(int model3DId)
    {
        if (showDebug)
            Debug.Log($"[Model3DItem] OnModel3DRemoved called with ID: {model3DId}");

        if (model3DData == null)
        {
            if (showDebug)
                Debug.LogError($"[Model3DItem] model3DData is NULL!");
            return;
        }

        if (showDebug)
            Debug.Log($"[Model3DItem] Current data: {model3DData.name} (ID: {model3DData.id}, is_used: {model3DData.is_used})");

        if (model3DData.id == model3DId)
        {
            if (showDebug)
                Debug.Log($"[Model3DItem] ENABLING item: {model3DData.name} (ID: {model3DId})");

            int oldIsUsed = model3DData.is_used;
            model3DData.is_used = 0;
            model3DPrefabInstance = null;

            if (showDebug)
                Debug.Log($"[Model3DItem] is_used changed: {oldIsUsed} → {model3DData.is_used}");

            UpdateButtonState();

            if (showDebug)
                Debug.Log($"[Model3DItem] Button enabled: {selectButton?.interactable}");
        }
        else
        {
            if (showDebug)
                Debug.LogWarning($"[Model3DItem] ID MISMATCH! Expected: {model3DId}, Current: {model3DData.id}");
        }
    }

    public void SetModel3DInfo(Model3DInfo info) => model3DInfo = info;

    public Model3D GetData() => model3DData;
    public Texture2D GetTexture() => modelTexture;

    private void OnDestroy()
    {
        if (selectButton != null)
            selectButton.onClick.RemoveAllListeners();

        if (teleportButton != null)
            teleportButton.onClick.RemoveAllListeners();

        if (isSubscribedToEvent)
            Model3DPrefabManager.OnModel3DPrefabSpawned -= OnModel3DPrefabSpawned;
    }
}
