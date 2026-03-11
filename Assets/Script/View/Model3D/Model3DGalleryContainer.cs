using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class Model3DGalleryContainer : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject galleryPanel;
    [SerializeField] private Transform contentContainer;
    [SerializeField] private GameObject model3DItemPrefab;
    [SerializeField] private Button refreshButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject loadingPanel;

    [Header("Settings")]
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private bool autoHideAfterLoad = true;
    [SerializeField] private float maxWaitTime = 15f;
    
    [Header("Filter Settings")]
    [SerializeField] private bool listIsUsedModel3D = false;
    [Space(5)]
    [SerializeField] private TextMeshProUGUI filterInfoText;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private List<Model3DItem> model3DItems = new List<Model3DItem>();
    private bool isDataLoaded = false;
    private bool isLoadingInProgress = false;

    private void Start()
    {
        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(RefreshGallery);
        }

        if (APIManager.Instance != null)
        {
            APIManager.Instance.onApiResponseRefreshed += OnAPIRefreshed;
        }

        UpdateFilterInfo();

        if (loadOnStart)
        {
            StartCoroutine(InitializeGallery());
        }
    }

    private IEnumerator InitializeGallery()
    {
        if (showDebug) Debug.Log("[Model3DGallery] Initializing gallery...");

        ShowLoading(true);
        UpdateStatus("Initializing...");

        float elapsed = 0f;
        while (APIManager.Instance == null && elapsed < maxWaitTime)
        {
            if (showDebug) Debug.Log("[Model3DGallery] Waiting for APIManager...");
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        if (APIManager.Instance == null)
        {
            Debug.LogError("[Model3DGallery] APIManager not found after waiting!");
            ShowLoading(false);
            UpdateStatus("Error: APIManager not found");
            yield break;
        }

        APIManager.Instance.onApiResponseLoaded += OnAPILoaded;

        elapsed = 0f;
        while (APIManager.Instance.apiResponse == null && elapsed < maxWaitTime)
        {
            if (showDebug) Debug.Log($"[Model3DGallery] Waiting for API data... ({elapsed:F1}s)");
            UpdateStatus($"Loading API data... ({elapsed:F1}s)");
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        if (APIManager.Instance.apiResponse == null)
        {
            Debug.LogError("[Model3DGallery] API data not ready after waiting!");
            ShowLoading(false);
            UpdateStatus("Error: API timeout");
            yield break;
        }

        if (showDebug) Debug.Log("[Model3DGallery] API data ready, loading models...");
        yield return StartCoroutine(LoadModelsCoroutine());
    }

    private void OnAPIRefreshed(APIResponse response)
    {
        if (showDebug) Debug.Log("[Model3DGallery] API auto-refreshed! Updating existing items...");
        UpdateExistingItems(response);
    }

    private void OnAPILoaded(APIResponse response)
    {
        if (showDebug) Debug.Log("[Model3DGallery] API data loaded callback!");
        isDataLoaded = true;

        if (!isLoadingInProgress)
        {
            StartCoroutine(LoadModelsCoroutine());
        }
    }

    public void LoadModels()
    {
        if (isLoadingInProgress)
        {
            if (showDebug) Debug.LogWarning("[Model3DGallery] Already loading, skipping...");
            return;
        }

        StartCoroutine(LoadModelsCoroutine());
    }

    private IEnumerator LoadModelsCoroutine(bool shouldAutoHide = true)
    {
        if (isLoadingInProgress)
        {
            if (showDebug) Debug.LogWarning("[Model3DGallery] Load already in progress!");
            yield break;
        }

        isLoadingInProgress = true;

        if (showDebug) Debug.Log("[Model3DGallery] Loading models...");
        ShowLoading(true);
        UpdateStatus("Loading models...");

        if (APIManager.Instance == null || APIManager.Instance.apiResponse == null)
        {
            if (showDebug) Debug.LogWarning("[Model3DGallery] API data not ready!");
            ShowLoading(false);
            UpdateStatus("API data not ready");
            isLoadingInProgress = false;
            yield break;
        }

        ClearItems();

        List<Model3D> models = GetFilteredModels();

        if (models == null || models.Count == 0)
        {
            ShowLoading(false);
            
            if (listIsUsedModel3D)
            {
                UpdateStatus("Chua co mo hinh 3D nao duoc trung bay");
                if (showDebug) Debug.Log("[Model3DGallery] No used models available (visitor view)");
            }
            else
            {
                UpdateStatus("No models found");
                if (showDebug) Debug.Log("[Model3DGallery] No models available");
            }
            
            isLoadingInProgress = false;
            yield break;
        }

        if (showDebug)
        {
            if (listIsUsedModel3D)
            {
                Debug.Log($"[Model3DGallery] Visitor Mode: Found {models.Count} used models");
            }
            else
            {
                Debug.Log($"[Model3DGallery] Admin Mode: Found {models.Count} total models");
            }
        }

        UpdateStatus($"Loading {models.Count} models...");

        yield return StartCoroutine(LoadAllModelsCoroutine(models, shouldAutoHide));

        isLoadingInProgress = false;
    }

    private List<Model3D> GetFilteredModels()
    {
        List<Model3D> allModels = APIManager.Instance.GetModel3DList();
        
        if (allModels == null || allModels.Count == 0)
        {
            return new List<Model3D>();
        }

        if (listIsUsedModel3D)
        {
            List<Model3D> usedModels = allModels
                .Where(m => m.is_used == 1)
                .ToList();
            
            if (showDebug)
            {
                Debug.Log($"[Model3DGallery] Filtered: {usedModels.Count}/{allModels.Count} used models");
            }
            
            return usedModels;
        }
        
        if (showDebug)
        {
            Debug.Log($"[Model3DGallery] No filter: Showing all {allModels.Count} models");
        }
        
        return allModels;
    }

    private void UpdateFilterInfo()
    {
        if (filterInfoText != null)
        {
            if (listIsUsedModel3D)
            {
                filterInfoText.text = "Danh sach mo hinh 3D dang trung bay";
            }
            else
            {
                filterInfoText.text = "Tat ca mo hinh 3D trong kho";
            }
        }
    }

    private IEnumerator LoadAllModelsCoroutine(List<Model3D> models, bool shouldAutoHide = true)
    {
        int loadedCount = 0;
        int totalCount = models.Count;

        foreach (Model3D model3D in models)
        {
            CreateModel3DItem(model3D);
            loadedCount++;

            UpdateStatus($"Creating items... {loadedCount}/{totalCount}");

            if (loadedCount % 5 == 0)
            {
                yield return null;
            }
        }

        if (showDebug) Debug.Log($"[Model3DGallery] Created {model3DItems.Count} items");

        UpdateStatus("Loading thumbnails...");
        yield return StartCoroutine(WaitForAllThumbnailsLoaded());

        yield return StartCoroutine(RebuildLayoutNextFrame());

        ShowLoading(false);
        
        if (listIsUsedModel3D)
        {
            UpdateStatus($"Da tai {model3DItems.Count} mo hinh 3D dang trung bay");
        }
        else
        {
            UpdateStatus($"Loaded {model3DItems.Count} models");
        }

        if (showDebug) Debug.Log($"[Model3DGallery] All loaded successfully!");

        if (shouldAutoHide && autoHideAfterLoad && galleryPanel != null)
        {
            yield return new WaitForSeconds(0.5f);
            galleryPanel.SetActive(false);
            if (showDebug) Debug.Log("[Model3DGallery] Panel hidden after load");
        }
        else if (!shouldAutoHide)
        {
            if (showDebug) Debug.Log("[Model3DGallery] Panel kept open (manual refresh)");
        }
    }

    private IEnumerator WaitForAllThumbnailsLoaded()
    {
        float timeout = 15f;
        float elapsed = 0f;
        int lastLoadedCount = 0;

        while (elapsed < timeout)
        {
            int loadedCount = 0;
            int totalCount = 0;

            foreach (Model3DItem item in model3DItems)
            {
                if (item != null && item.model3DData != null)
                {
                    totalCount++;

                    if (string.IsNullOrEmpty(item.model3DData.thumbnail_url) ||
                        (item.thumbnailImage != null && item.thumbnailImage.sprite != null))
                    {
                        loadedCount++;
                    }
                }
            }

            if (loadedCount != lastLoadedCount)
            {
                UpdateStatus($"Loading thumbnails... {loadedCount}/{totalCount}");
                lastLoadedCount = loadedCount;
            }

            if (loadedCount >= totalCount && totalCount > 0)
            {
                if (showDebug) Debug.Log($"[Model3DGallery] All {totalCount} thumbnails loaded!");
                yield break;
            }

            elapsed += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        if (showDebug) Debug.LogWarning($"[Model3DGallery] Thumbnail loading timeout! ({lastLoadedCount}/{model3DItems.Count} loaded)");
    }

    private void CreateModel3DItem(Model3D model3D)
    {
        if (model3DItemPrefab == null || contentContainer == null)
        {
            Debug.LogError("[Model3DGallery] Missing prefab or container!");
            return;
        }

        GameObject itemObj = Instantiate(model3DItemPrefab, contentContainer);
        Model3DItem item = itemObj.GetComponent<Model3DItem>();

        if (item != null)
        {
            item.Setup(model3D, null);
            model3DItems.Add(item);

            if (!string.IsNullOrEmpty(model3D.thumbnail_url))
            {
                StartCoroutine(LoadModel3DThumbnail(model3D, item));
            }

            if (showDebug)
                Debug.Log($"[Model3DGallery] Created: {model3D.name} (ID: {model3D.id})");
        }
        else
        {
            Debug.LogError("[Model3DGallery] Model3DItem component not found!");
            Destroy(itemObj);
        }
    }

    private IEnumerator LoadModel3DThumbnail(Model3D model3D, Model3DItem item)
    {
        if (showDebug) Debug.Log($"[Model3DGallery] Loading thumbnail: {model3D.name}");

        using (UnityEngine.Networking.UnityWebRequest request =
               UnityEngine.Networking.UnityWebRequestTexture.GetTexture(model3D.thumbnail_url))
        {
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Texture2D texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);

                if (texture != null && item != null && item.thumbnailImage != null)
                {
                    Sprite sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f)
                    );
                    item.thumbnailImage.sprite = sprite;

                    if (showDebug)
                        Debug.Log($"[Model3DGallery] Thumbnail loaded: {model3D.name}");
                }
            }
            else
            {
                Debug.LogError($"[Model3DGallery] Failed to load thumbnail: {request.error}");
            }
        }
    }

    private void UpdateExistingItems(APIResponse response)
    {
        if (response?.data?.model3ds == null)
        {
            if (showDebug) Debug.LogWarning("[Model3DGallery] No model3ds in response!");
            return;
        }

        if (showDebug)
            Debug.Log($"[Model3DGallery] Updating {model3DItems.Count} existing items with {response.data.model3ds.Count} API models...");

        int updatedCount = 0;
        int removedCount = 0;
        int addedCount = 0;

        foreach (Model3DItem item in model3DItems)
        {
            if (item == null || item.model3DData == null)
            {
                if (showDebug) Debug.LogWarning("[Model3DGallery] Null item or data found!");
                continue;
            }

            Model3D updatedModel3D = response.data.model3ds.FirstOrDefault(m => m.id == item.model3DData.id);

            if (updatedModel3D != null)
            {
                if (showDebug)
                    Debug.Log($"[Model3DGallery] Checking '{updatedModel3D.name}' (ID: {updatedModel3D.id}): old is_used={item.model3DData.is_used}, new is_used={updatedModel3D.is_used}");

                if (item.model3DData.is_used == 1 && updatedModel3D.is_used == 0)
                {
                    if (showDebug)
                        Debug.Log($"[Model3DGallery] Model3D REMOVED: {updatedModel3D.name} (ID: {updatedModel3D.id})");

                    item.OnModel3DRemoved(updatedModel3D.id);
                    removedCount++;
                }
                else if (item.model3DData.is_used == 0 && updatedModel3D.is_used == 1)
                {
                    if (showDebug)
                        Debug.Log($"[Model3DGallery] Model3D ADDED: {updatedModel3D.name} (ID: {updatedModel3D.id})");

                    item.OnModel3DSaved(updatedModel3D);
                    addedCount++;
                }
                else
                {
                    if (showDebug)
                        Debug.Log($"[Model3DGallery] No change for: {updatedModel3D.name} (is_used={updatedModel3D.is_used})");
                }

                updatedCount++;
            }
            else
            {
                if (showDebug)
                    Debug.LogWarning($"[Model3DGallery] Model3D not found in response: {item.model3DData.name} (ID: {item.model3DData.id})");
            }
        }

        if (showDebug)
            Debug.Log($"[Model3DGallery] Update complete: {updatedCount} checked, {removedCount} removed, {addedCount} added");
    }

    public void OnModel3DRemovedFromScene(int model3DId)
    {
        if (showDebug)
            Debug.Log($"[Model3DGallery] Model3D removed from scene: {model3DId}");

        Model3DItem item = model3DItems.FirstOrDefault(i => i != null && i.model3DData != null && i.model3DData.id == model3DId);

        if (item != null)
        {
            item.OnModel3DRemoved(model3DId);

            if (showDebug)
                Debug.Log($"[Model3DGallery] Item enabled: {item.model3DData.name}");
        }
        else
        {
            if (showDebug)
                Debug.LogWarning($"[Model3DGallery] Item not found for model3D ID: {model3DId}");
        }
    }

    private void ClearItems()
    {
        foreach (Model3DItem item in model3DItems)
        {
            if (item != null) Destroy(item.gameObject);
        }
        model3DItems.Clear();

        if (showDebug) Debug.Log("[Model3DGallery] All items cleared");
    }

    public void RefreshGallery()
    {
        if (showDebug) Debug.Log("[Model3DGallery] Refreshing from API...");

        if (galleryPanel != null)
        {
            galleryPanel.SetActive(true);
        }

        StartCoroutine(RefreshGalleryCoroutine());
    }

    private IEnumerator RefreshGalleryCoroutine()
    {
        ShowLoading(true);
        UpdateStatus("Fetching new data...");

        ClearItems();

        if (APIManager.Instance != null)
        {
            APIManager.Instance.onApiResponseLoaded -= OnAPILoaded;
        }

        isLoadingInProgress = false;

        if (APIManager.Instance != null)
        {
            APIManager.Instance.GetDataFromAPI();
        }
        else
        {
            Debug.LogError("[Model3DGallery] APIManager not found!");
            ShowLoading(false);
            UpdateStatus("Error: APIManager not found");
            
            if (APIManager.Instance != null)
            {
                APIManager.Instance.onApiResponseLoaded += OnAPILoaded;
            }
            
            yield break;
        }

        float elapsed = 0f;
        APIResponse oldResponse = APIManager.Instance.apiResponse;

        while (elapsed < maxWaitTime)
        {
            if (APIManager.Instance.apiResponse != null && 
                APIManager.Instance.apiResponse != oldResponse)
            {
                if (showDebug) Debug.Log("[Model3DGallery] New API data received!");
                break;
            }

            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (elapsed >= maxWaitTime)
        {
            Debug.LogError("[Model3DGallery] API refresh timeout!");
            ShowLoading(false);
            UpdateStatus("Error: API timeout");
            
            if (APIManager.Instance != null)
            {
                APIManager.Instance.onApiResponseLoaded += OnAPILoaded;
            }
            
            yield break;
        }

        if (showDebug) Debug.Log("[Model3DGallery] Loading models from new data...");
        yield return StartCoroutine(LoadModelsCoroutine(shouldAutoHide: false));

        if (APIManager.Instance != null)
        {
            APIManager.Instance.onApiResponseLoaded += OnAPILoaded;
            if (showDebug) Debug.Log("[Model3DGallery] Re-subscribed to onApiResponseLoaded");
        }
    }

    private void ShowLoading(bool show)
    {
        if (loadingPanel != null) loadingPanel.SetActive(show);
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        if (showDebug) Debug.Log($"[Model3DGallery] Status: {message}");
    }

    private IEnumerator RebuildLayoutNextFrame()
    {
        yield return null;
        yield return null;

        if (contentContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer as RectTransform);
            if (showDebug) Debug.Log("[Model3DGallery] Layout rebuilt");
        }
    }

    public void SetFilterUsedModels(bool showOnlyUsed)
    {
        listIsUsedModel3D = showOnlyUsed;
        UpdateFilterInfo();
        
        if (showDebug)
        {
            Debug.Log($"[Model3DGallery] Filter changed to: {(showOnlyUsed ? "Used Only" : "All Models")}");
        }
        
        RefreshGallery();
    }

    private void OnDestroy()
    {
        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveListener(RefreshGallery);
        }

        if (APIManager.Instance != null)
        {
            APIManager.Instance.onApiResponseLoaded -= OnAPILoaded;
            APIManager.Instance.onApiResponseRefreshed -= OnAPIRefreshed;
        }
    }
}
