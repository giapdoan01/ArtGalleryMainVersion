using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PaintingItem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image thumbnailImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI frameIdText;
    [SerializeField] private GameObject isUsedFrame;
    [SerializeField] private Button selectButton;
    [SerializeField] private Button teleportButton;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    public Painting paintingData;
    private Texture2D paintingTexture;
    private PaintingPrefab paintingPrefabInstance;

    private bool isSubscribedToEvent = false;

    private void OnEnable()
    {
        if (!isSubscribedToEvent)
        {
            PaintingPrefabManager.OnPaintingPrefabSpawned += OnPaintingPrefabSpawned;
            isSubscribedToEvent = true;

            if (showDebug)
                Debug.Log($"[PaintingItem] Subscribed to OnPaintingPrefabSpawned event");
        }
    }

    private void OnDisable()
    {
        if (isSubscribedToEvent)
        {
            PaintingPrefabManager.OnPaintingPrefabSpawned -= OnPaintingPrefabSpawned;
            isSubscribedToEvent = false;
        }
    }

    public void Setup(Painting painting, Texture2D pathTexture)
    {
        paintingData = painting;
        paintingTexture = pathTexture;

        if (nameText != null)
        {
            string displayName = painting.name;
            if (painting.paintings_lang?.vi != null && !string.IsNullOrEmpty(painting.paintings_lang.vi.name))
            {
                displayName = painting.paintings_lang.vi.name;
            }
            nameText.text = displayName;
        }

        if (frameIdText != null)
        {
            frameIdText.text = $" {painting.frame ?? "None"}";
        }

        if (isUsedFrame != null)
        {
            isUsedFrame.SetActive(painting.is_used == 1);
        }

        UpdateButtonState();

        if (thumbnailImage != null)
        {
            if (pathTexture != null)
            {
                Sprite sprite = Sprite.Create(
                    pathTexture,
                    new Rect(0, 0, pathTexture.width, pathTexture.height),
                    new Vector2(0.5f, 0.5f)
                );
                thumbnailImage.sprite = sprite;

                if (showDebug)
                    Debug.Log($"[PaintingItem] Path texture displayed: {painting.name}");
            }
            else if (!string.IsNullOrEmpty(painting.thumbnail_url))
            {
                StartCoroutine(LoadThumbnail(painting.thumbnail_url));
            }
        }

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnSelectPainting);
        }

        if (teleportButton != null)
        {
            teleportButton.onClick.RemoveAllListeners();
            teleportButton.onClick.AddListener(OnTeleportButtonClicked);

            if (showDebug)
                Debug.Log($"[PaintingItem] Teleport button setup for: {painting.name} (ID: {painting.id})");
        }

        if (painting.is_used == 1)
        {
            if (PaintingPrefabManager.Instance != null && 
                PaintingPrefabManager.Instance.IsPrefabSpawned(painting.id))
            {
                FindPaintingPrefabInScene();
            }
            else
            {
                if (showDebug)
                    Debug.Log($"[PaintingItem] Waiting for prefab spawn event: {painting.name} (ID: {painting.id})");
            }
        }

        if (showDebug)
            Debug.Log($"[PaintingItem] Setup: {painting.name} (ID: {painting.id}, is_used: {painting.is_used})");
    }

    private void OnPaintingPrefabSpawned(int paintingId, PaintingPrefab prefab)
    {
        if (paintingData != null && paintingData.id == paintingId)
        {
            if (showDebug)
                Debug.Log($"[PaintingItem]  Received spawn event for: {paintingData.name} (ID: {paintingId})");

            paintingPrefabInstance = prefab;

            UpdateTeleportButtonState();

            if (showDebug)
                Debug.Log($"[PaintingItem]  Prefab linked via event: {paintingData.name}");
        }
    }

    private IEnumerator LoadThumbnail(string url)
    {
        using (UnityEngine.Networking.UnityWebRequest request =
               UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Texture2D texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);

                if (thumbnailImage != null && texture != null)
                {
                    Sprite sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f)
                    );
                    thumbnailImage.sprite = sprite;
                }
            }
        }
    }

    private void OnSelectPainting()
    {
        if (paintingData.is_used == 1)
        {
            if (showDebug)
                Debug.Log($"[PaintingItem] Cannot select - already used: {paintingData.name}");
            return;
        }

        if (showDebug)
            Debug.Log($"[PaintingItem] Selected: {paintingData.name}");

        if (PaintingInputInfo.Instance != null && paintingTexture != null)
        {
            PaintingInputInfo.Instance.Show(paintingData, paintingTexture, OnPaintingSaved);
        }
        else
        {
            Debug.LogError("[PaintingItem] PaintingInputInfo or texture is null!");
        }
    }

    private void OnTeleportButtonClicked()
    {
        if (showDebug)
        {
            Debug.Log($"[PaintingItem] ========================================");
            Debug.Log($"[PaintingItem] TELEPORT BUTTON CLICKED!");
            Debug.Log($"[PaintingItem] Painting: {paintingData?.name}");
            Debug.Log($"[PaintingItem] ID: {paintingData?.id}");
            Debug.Log($"[PaintingItem] is_used: {paintingData?.is_used}");
            Debug.Log($"[PaintingItem] Prefab Instance: {paintingPrefabInstance}");
            Debug.Log($"[PaintingItem] ========================================");
        }

        if (paintingData == null)
        {
            Debug.LogError("[PaintingItem] Painting data is null!");
            return;
        }

        if (paintingData.is_used != 1)
        {
            Debug.LogWarning($"[PaintingItem] Painting not spawned yet: {paintingData.name}");
            return;
        }

        if (paintingPrefabInstance == null)
        {
            if (showDebug)
                Debug.Log($"[PaintingItem] Prefab instance null, trying to find...");

            FindPaintingPrefabInScene();
        }

        if (paintingPrefabInstance == null)
        {
            Debug.LogError($"[PaintingItem] Painting prefab not found: {paintingData.name} (ID: {paintingData.id})");
            return;
        }

        Transform teleportPoint = paintingPrefabInstance.GetTeleportPoint();

        if (teleportPoint == null)
        {
            Debug.LogWarning($"[PaintingItem] TeleportPoint not assigned: {paintingData.name}");
            return;
        }

        if (showDebug)
            Debug.Log($"[PaintingItem] Teleport point: {teleportPoint.position}");

        if (PlayerTeleportManager.Instance != null)
        {
            PlayerTeleportManager.Instance.TeleportToPoint(teleportPoint);

            if (showDebug)
                Debug.Log($"[PaintingItem]  Teleport request sent");
        }
        else
        {
            Debug.LogError("[PaintingItem] PlayerTeleportManager not found!");
        }
    }

    private void FindPaintingPrefabInScene()
    {
        if (paintingData == null)
        {
            if (showDebug)
                Debug.LogWarning("[PaintingItem] paintingData is null");
            return;
        }

        if (PaintingPrefabManager.Instance != null)
        {
            paintingPrefabInstance = PaintingPrefabManager.Instance.FindPrefabByID(paintingData.id);

            if (paintingPrefabInstance != null)
            {
                if (showDebug)
                    Debug.Log($"[PaintingItem]  Found via PaintingPrefabManager: {paintingData.name}");

                UpdateTeleportButtonState();
                return;
            }
        }

        paintingPrefabInstance = null;

        PaintingPrefab[] allPaintings = FindObjectsOfType<PaintingPrefab>();

        if (showDebug)
            Debug.Log($"[PaintingItem] Searching in {allPaintings.Length} prefabs...");

        foreach (PaintingPrefab painting in allPaintings)
        {
            Painting data = painting.GetData();

            if (data != null && data.id == paintingData.id)
            {
                paintingPrefabInstance = painting;

                if (showDebug)
                    Debug.Log($"[PaintingItem]  Found via FindObjectsOfType: {paintingData.name}");

                break;
            }
        }

        if (paintingPrefabInstance == null && showDebug)
        {
            Debug.LogWarning($"[PaintingItem] ❌ Prefab NOT found: {paintingData.name} (ID: {paintingData.id})");
        }

        UpdateTeleportButtonState();
    }

    public void OnPaintingSaved(Painting updatedPainting)
    {
        if (showDebug)
            Debug.Log($"[PaintingItem] Painting saved callback: {updatedPainting.name}");

        paintingData.is_used = updatedPainting.is_used;
        paintingData.frame_type = updatedPainting.frame_type;
        paintingData.position = updatedPainting.position;
        paintingData.rotate = updatedPainting.rotate;

        UpdateButtonState();

        if (showDebug)
            Debug.Log($"[PaintingItem] Waiting for spawn event...");
    }

    private void UpdateButtonState()
    {
        if (selectButton != null)
        {
            selectButton.interactable = (paintingData.is_used == 0);
        }

        if (isUsedFrame != null)
        {
            isUsedFrame.SetActive(paintingData.is_used == 1);
        }

        UpdateTeleportButtonState();
    }

    private void UpdateTeleportButtonState()
    {
        if (teleportButton == null)
        {
            if (showDebug)
                Debug.LogWarning($"[PaintingItem] Teleport button is null");
            return;
        }

        bool isUsed = paintingData.is_used == 1;
        bool hasPrefab = paintingPrefabInstance != null;
        bool hasTeleportPoint = hasPrefab && paintingPrefabInstance.GetTeleportPoint() != null;
        
        bool canTeleport = isUsed && hasPrefab && hasTeleportPoint;

        teleportButton.interactable = canTeleport;
        teleportButton.gameObject.SetActive(isUsed);

        if (showDebug)
        {
            Debug.Log($"[PaintingItem] Button state for {paintingData.name}:");
            Debug.Log($"  - is_used: {isUsed}");
            Debug.Log($"  - hasPrefab: {hasPrefab}");
            Debug.Log($"  - hasTeleportPoint: {hasTeleportPoint}");
            Debug.Log($"  - canTeleport: {canTeleport}");
        }
    }

    public void OnPaintingRemoved(int paintingId)
    {
        if (paintingData != null && paintingData.id == paintingId)
        {
            if (showDebug)
                Debug.Log($"[PaintingItem] Painting removed: {paintingData.name}");

            paintingData.is_used = 0;
            paintingPrefabInstance = null;

            UpdateButtonState();
        }
    }

    public Painting GetData() => paintingData;
    public Texture2D GetTexture() => paintingTexture;

    private void OnDestroy()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
        }

        if (teleportButton != null)
        {
            teleportButton.onClick.RemoveAllListeners();
        }

        if (isSubscribedToEvent)
        {
            PaintingPrefabManager.OnPaintingPrefabSpawned -= OnPaintingPrefabSpawned;
        }
    }
}
