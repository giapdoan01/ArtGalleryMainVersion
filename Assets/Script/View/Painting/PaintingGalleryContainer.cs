using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class PaintingGalleryContainer : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject galleryPanel;
    [SerializeField] private Transform contentContainer;
    [SerializeField] private GameObject paintingItemPrefab;
    [SerializeField] private Button refreshButton;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private GameObject loadingPanel;

    [Header("Settings")]
    [SerializeField] private bool loadOnStart = true;
    [SerializeField] private bool autoHideAfterLoad = true;
    [SerializeField] private float maxWaitTime = 15f;
    
    //  THÊM: Filter cho danh sách khách tham quan
    [Header("Filter Settings")]
    [SerializeField] private bool listIsUsedPainting = false; // true = Chỉ hiện tranh đã dùng (khách tham quan)
    [Space(5)]
    [SerializeField] private TextMeshProUGUI filterInfoText; // Optional: Hiển thị thông tin filter

    [Header("Panel Integration")]
    [SerializeField] private PaintingInfo paintingInfo;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private List<PaintingItem> paintingItems = new List<PaintingItem>();
    private bool isDataLoaded = false;
    private bool isLoadingInProgress = false;
    private bool isInitialized = false;
    private Coroutine activeLoadCoroutine;

    private void OnEnable()
    {
        AdminModeManager.OnAdminModeChanged += ApplyAdminMode;
        ApplyAdminMode(AdminModeManager.Instance != null && AdminModeManager.Instance.IsAdmin);
    }

    private void OnDisable()
    {
        AdminModeManager.OnAdminModeChanged -= ApplyAdminMode;
    }

    private void ApplyAdminMode(bool isAdmin)
    {
        if (refreshButton != null) refreshButton.gameObject.SetActive(isAdmin);

        bool newFilter = !isAdmin; // admin → false (thấy tất cả), visitor → true (chỉ tranh đã dùng)
        if (listIsUsedPainting == newFilter) return;

        listIsUsedPainting = newFilter;
        UpdateFilterInfo();

        // Reload danh sách chỉ khi gallery đã khởi tạo xong
        if (isInitialized)
        {
            if (activeLoadCoroutine != null) StopCoroutine(activeLoadCoroutine);
            isLoadingInProgress = false;
            activeLoadCoroutine = StartCoroutine(LoadPaintingsCoroutine(shouldAutoHide: false));
        }
    }

    private void Start()
    {
        if (refreshButton != null)
            refreshButton.onClick.AddListener(RefreshGallery);

        if (APIManager.Instance != null)
            APIManager.Instance.onApiResponseRefreshed += OnAPIRefreshed;

        UpdateFilterInfo();

        isInitialized = true;

        if (loadOnStart)
            StartCoroutine(InitializeGallery());
    }

    private IEnumerator InitializeGallery()
    {
        if (showDebug) Debug.Log("[PaintingGallery] Initializing gallery...");

        ShowLoading(true);
        UpdateStatus("Initializing...");

        float elapsed = 0f;
        while (APIManager.Instance == null && elapsed < maxWaitTime)
        {
            if (showDebug) Debug.Log("[PaintingGallery] Waiting for APIManager...");
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        if (APIManager.Instance == null)
        {
            Debug.LogError("[PaintingGallery] APIManager not found after waiting!");
            ShowLoading(false);
            UpdateStatus("Error: APIManager not found");
            yield break;
        }

        APIManager.Instance.onApiResponseLoaded += OnAPILoaded;

        elapsed = 0f;
        while (APIManager.Instance.apiResponse == null && elapsed < maxWaitTime)
        {
            if (showDebug) Debug.Log($"[PaintingGallery] Waiting for API data... ({elapsed:F1}s)");
            UpdateStatus($"Loading API data... ({elapsed:F1}s)");
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        if (APIManager.Instance.apiResponse == null)
        {
            Debug.LogError("[PaintingGallery] API data not ready after waiting!");
            ShowLoading(false);
            UpdateStatus("Error: API timeout");
            yield break;
        }

        if (showDebug) Debug.Log("[PaintingGallery] API data ready, loading paintings...");
        activeLoadCoroutine = StartCoroutine(LoadPaintingsCoroutine());
        yield return activeLoadCoroutine;
    }

    private void OnAPIRefreshed(APIResponse response)
    {
        if (showDebug) Debug.Log("[PaintingGallery] API auto-refreshed! Updating existing items...");
        UpdateExistingItems(response);
    }

    private void OnAPILoaded(APIResponse response)
    {
        if (showDebug) Debug.Log("[PaintingGallery] API data loaded callback!");
        isDataLoaded = true;

        if (!isLoadingInProgress)
        {
            activeLoadCoroutine = StartCoroutine(LoadPaintingsCoroutine());
        }
    }

    public void LoadPaintings()
    {
        if (isLoadingInProgress)
        {
            if (showDebug) Debug.LogWarning("[PaintingGallery] Already loading, skipping...");
            return;
        }

        StartCoroutine(LoadPaintingsCoroutine());
    }

    private IEnumerator LoadPaintingsCoroutine(bool shouldAutoHide = true)
    {
        if (isLoadingInProgress)
        {
            if (showDebug) Debug.LogWarning("[PaintingGallery] Load already in progress!");
            yield break;
        }

        isLoadingInProgress = true;

        if (showDebug) Debug.Log("[PaintingGallery] Loading paintings...");
        ShowLoading(true);
        UpdateStatus("Loading paintings...");

        if (APIManager.Instance == null || APIManager.Instance.apiResponse == null)
        {
            if (showDebug) Debug.LogWarning("[PaintingGallery] API data not ready!");
            ShowLoading(false);
            UpdateStatus("API data not ready");
            isLoadingInProgress = false;
            yield break;
        }

        ClearItems();

        //  LẤY DANH SÁCH VÀ LỌC THEO listIsUsedPainting
        List<Painting> paintings = GetFilteredPaintings();

        if (paintings == null || paintings.Count == 0)
        {
            ShowLoading(false);
            
            //  Hiển thị message phù hợp
            if (listIsUsedPainting)
            {
                UpdateStatus("Chưa có tranh nào được trưng bày");
                if (showDebug) Debug.Log("[PaintingGallery] No used paintings available (visitor view)");
            }
            else
            {
                UpdateStatus("No paintings found");
                if (showDebug) Debug.Log("[PaintingGallery] No paintings available");
            }
            
            isLoadingInProgress = false;
            yield break;
        }

        //  Log thông tin filter
        if (showDebug)
        {
            if (listIsUsedPainting)
            {
                Debug.Log($"[PaintingGallery] Visitor Mode: Found {paintings.Count} used paintings");
            }
            else
            {
                Debug.Log($"[PaintingGallery] Admin Mode: Found {paintings.Count} total paintings");
            }
        }

        UpdateStatus($"Loading {paintings.Count} paintings...");

        yield return StartCoroutine(LoadAllPaintingsCoroutine(paintings, shouldAutoHide));

        isLoadingInProgress = false;
    }

    //  THÊM: Method lọc paintings theo listIsUsedPainting
    private List<Painting> GetFilteredPaintings()
    {
        // Lấy tất cả paintings từ API
        List<Painting> allPaintings = APIManager.Instance.GetPaintingList();
        
        if (allPaintings == null || allPaintings.Count == 0)
        {
            return new List<Painting>();
        }

        //  NẾU listIsUsedPainting = true → CHỈ lấy tranh đã dùng (is_used = 1)
        if (listIsUsedPainting)
        {
            List<Painting> usedPaintings = allPaintings
                .Where(p => p.is_used == 1) // Chỉ lấy tranh đã được trưng bày
                .ToList();
            
            if (showDebug)
            {
                Debug.Log($"[PaintingGallery] Filtered: {usedPaintings.Count}/{allPaintings.Count} used paintings");
            }
            
            return usedPaintings;
        }
        
        //  NẾU listIsUsedPainting = false → Lấy TẤT CẢ (admin view)
        if (showDebug)
        {
            Debug.Log($"[PaintingGallery] No filter: Showing all {allPaintings.Count} paintings");
        }
        
        return allPaintings;
    }

    //  THÊM: Method cập nhật thông tin filter (optional)
    private void UpdateFilterInfo()
    {
        if (filterInfoText != null)
        {
            if (listIsUsedPainting)
            {
                filterInfoText.text = "Danh sách tranh đang trưng bày";
            }
            else
            {
                filterInfoText.text = "Tất cả tranh trong kho";
            }
        }
    }

    private IEnumerator LoadAllPaintingsCoroutine(List<Painting> paintings, bool shouldAutoHide = true)
    {
        int loadedCount = 0;
        int totalCount = paintings.Count;

        foreach (Painting painting in paintings)
        {
            CreatePaintingItem(painting);
            loadedCount++;

            UpdateStatus($"Creating items... {loadedCount}/{totalCount}");

            if (loadedCount % 5 == 0)
            {
                yield return null;
            }
        }

        if (showDebug) Debug.Log($"[PaintingGallery] Created {paintingItems.Count} items");

        UpdateStatus("Loading textures...");
        yield return StartCoroutine(WaitForAllTexturesLoaded());

        yield return StartCoroutine(RebuildLayoutNextFrame());

        ShowLoading(false);
        
        //  Hiển thị message phù hợp
        if (listIsUsedPainting)
        {
            UpdateStatus($" Đã tải {paintingItems.Count} tranh đang trưng bày");
        }
        else
        {
            UpdateStatus($" Loaded {paintingItems.Count} paintings");
        }

        if (showDebug) Debug.Log($"[PaintingGallery]  All loaded successfully!");

        if (shouldAutoHide && autoHideAfterLoad && galleryPanel != null)
        {
            yield return new WaitForSeconds(0.5f);
            galleryPanel.SetActive(false);
            if (showDebug) Debug.Log("[PaintingGallery] Panel hidden after load");
        }
        else if (!shouldAutoHide)
        {
            if (showDebug) Debug.Log("[PaintingGallery] Panel kept open (manual refresh)");
        }
    }

    private IEnumerator WaitForAllTexturesLoaded()
    {
        float timeout = 15f;
        float elapsed = 0f;
        int lastLoadedCount = 0;

        while (elapsed < timeout)
        {
            int loadedCount = 0;
            int totalCount = 0;

            foreach (PaintingItem item in paintingItems)
            {
                if (item != null && item.paintingData != null)
                {
                    totalCount++;

                    if (string.IsNullOrEmpty(item.paintingData.path_url) || item.GetTexture() != null)
                    {
                        loadedCount++;
                    }
                }
            }

            if (loadedCount != lastLoadedCount)
            {
                UpdateStatus($"Loading textures... {loadedCount}/{totalCount}");
                lastLoadedCount = loadedCount;
            }

            if (loadedCount >= totalCount && totalCount > 0)
            {
                if (showDebug) Debug.Log($"[PaintingGallery] All {totalCount} textures loaded!");
                yield break;
            }

            elapsed += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        if (showDebug) Debug.LogWarning($"[PaintingGallery] Texture loading timeout! ({lastLoadedCount}/{paintingItems.Count} loaded)");
    }

    private void CreatePaintingItem(Painting painting)
    {
        if (paintingItemPrefab == null || contentContainer == null)
        {
            Debug.LogError("[PaintingGallery] Missing prefab or container!");
            return;
        }

        GameObject itemObj = Instantiate(paintingItemPrefab, contentContainer);
        PaintingItem item = itemObj.GetComponent<PaintingItem>();

        if (item != null)
        {
            item.SetPaintingInfo(paintingInfo);

            if (!string.IsNullOrEmpty(painting.path_url))
            {
                StartCoroutine(LoadPaintingTexture(painting, item));
            }
            else
            {
                item.Setup(painting, null);
            }

            paintingItems.Add(item);

            if (showDebug)
                Debug.Log($"[PaintingGallery] Created: {painting.name}");
        }
        else
        {
            Debug.LogError("[PaintingGallery] PaintingItem component not found!");
            Destroy(itemObj);
        }
    }

    private IEnumerator LoadPaintingTexture(Painting painting, PaintingItem item)
    {
        if (showDebug) Debug.Log($"[PaintingGallery] Loading texture: {painting.name}");

        using (UnityEngine.Networking.UnityWebRequest request =
               UnityEngine.Networking.UnityWebRequestTexture.GetTexture(painting.path_url))
        {
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                Texture2D texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);

                if (texture != null)
                {
                    item.Setup(painting, texture);
                    if (showDebug)
                        Debug.Log($"[PaintingGallery] Texture loaded: {painting.name}");
                }
                else
                {
                    Debug.LogError($"[PaintingGallery] Texture is null: {painting.name}");
                    item.Setup(painting, null);
                }
            }
            else
            {
                Debug.LogError($"[PaintingGallery] Failed to load texture: {request.error}");
                item.Setup(painting, null);
            }
        }
    }

    private void UpdateExistingItems(APIResponse response)
    {
        if (response?.data?.paintings == null) return;

        foreach (PaintingItem item in paintingItems)
        {
            if (item == null || item.paintingData == null) continue;

            Painting updatedPainting = response.data.paintings.FirstOrDefault(p => p.id == item.paintingData.id);

            if (updatedPainting != null)
            {
                if (item.paintingData.is_used == 1 && updatedPainting.is_used == 0)
                {
                    if (showDebug)
                        Debug.Log($"[PaintingGallery]  Painting removed: {updatedPainting.name}");

                    item.OnPaintingRemoved(updatedPainting.id);
                }
                else if (item.paintingData.is_used == 0 && updatedPainting.is_used == 1)
                {
                    if (showDebug)
                        Debug.Log($"[PaintingGallery]  Painting added: {updatedPainting.name}");

                    item.OnPaintingSaved(updatedPainting);
                }
            }
        }
    }

    private void ClearItems()
    {
        foreach (PaintingItem item in paintingItems)
        {
            if (item != null) Destroy(item.gameObject);
        }
        paintingItems.Clear();

        if (showDebug) Debug.Log("[PaintingGallery]  All items cleared");
    }

    public void RefreshGallery()
    {
        if (showDebug) Debug.Log("[PaintingGallery]  Refreshing from API...");

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
            Debug.LogError("[PaintingGallery] APIManager not found!");
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
                if (showDebug) Debug.Log("[PaintingGallery]  New API data received!");
                break;
            }

            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (elapsed >= maxWaitTime)
        {
            Debug.LogError("[PaintingGallery] API refresh timeout!");
            ShowLoading(false);
            UpdateStatus("Error: API timeout");

            if (APIManager.Instance != null)
            {
                APIManager.Instance.onApiResponseLoaded += OnAPILoaded;
            }

            yield break;
        }

        if (showDebug) Debug.Log("[PaintingGallery] Loading paintings from new data...");
        yield return StartCoroutine(LoadPaintingsCoroutine(shouldAutoHide: false));

        if (APIManager.Instance != null)
        {
            APIManager.Instance.onApiResponseLoaded += OnAPILoaded;
            if (showDebug) Debug.Log("[PaintingGallery] Re-subscribed to onApiResponseLoaded");
        }
    }

    private void ShowLoading(bool show)
    {
        if (loadingPanel != null) loadingPanel.SetActive(show);
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        if (showDebug) Debug.Log($"[PaintingGallery] Status: {message}");
    }

    private IEnumerator RebuildLayoutNextFrame()
    {
        yield return null;
        yield return null;

        if (contentContainer != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer as RectTransform);
            if (showDebug) Debug.Log("[PaintingGallery] Layout rebuilt");
        }
    }
    
    public void OnPaintingRemovedFromScene(int paintingId)
    {
        if (showDebug)
            Debug.Log($"[PaintingGallery]  Painting removed from scene: {paintingId}");

        PaintingItem item = paintingItems.FirstOrDefault(i => i != null && i.paintingData != null && i.paintingData.id == paintingId);

        if (item != null)
        {
            item.OnPaintingRemoved(paintingId);

            if (showDebug)
                Debug.Log($"[PaintingGallery]  Item enabled: {item.paintingData.name}");
        }
        else
        {
            if (showDebug)
                Debug.LogWarning($"[PaintingGallery] Item not found for painting ID: {paintingId}");
        }
    }

    //  THÊM: Public method để toggle filter (optional - có thể gọi từ UI button)
    public void SetFilterUsedPaintings(bool showOnlyUsed)
    {
        listIsUsedPainting = showOnlyUsed;
        UpdateFilterInfo();
        
        if (showDebug)
        {
            Debug.Log($"[PaintingGallery] Filter changed to: {(showOnlyUsed ? "Used Only" : "All Paintings")}");
        }
        
        // Reload danh sách với filter mới
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
