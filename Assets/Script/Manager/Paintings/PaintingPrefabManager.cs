using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class PaintingPrefabManager : MonoBehaviour
{
    private static PaintingPrefabManager instance;
    public static PaintingPrefabManager Instance
    {
        get
        {
            if (instance == null)
                instance = FindObjectOfType<PaintingPrefabManager>();
            return instance;
        }
    }

    public static event Action<int, PaintingPrefab> OnPaintingPrefabSpawned;
    public static event Action<int> OnPaintingPrefabRemoved;

    [Header("Prefab")]
    [SerializeField] private GameObject paintingPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnDistance      = 2f;
    [SerializeField] private float delayBetweenSpawns = 0.1f;

    [Header("Pool Settings")]
    [Tooltip("Số object giữ sẵn trong pool khi khởi động.")]
    [SerializeField] private int initialPoolSize = 10;
    [Tooltip("Số object tối đa pool giữ lại (inactive). Vượt quá sẽ Destroy thật.")]
    [SerializeField] private int maxPoolSize     = 60;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // Active objects
    private readonly Dictionary<int, GameObject>    spawnedPaintingObjects = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, PaintingPrefab> spawnedPaintingPrefabs = new Dictionary<int, PaintingPrefab>();

    // Pool — inactive objects chờ tái sử dụng
    private readonly Queue<GameObject> pool = new Queue<GameObject>();

    private bool isLoadingPaintings = false;

    // ════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (paintingPrefab == null)
        {
            Debug.LogError("[PaintingPrefabManager] paintingPrefab is NULL! Assign it in Inspector!");
            return;
        }

        PrewarmPool(initialPoolSize);

        if (showDebug)
            Debug.Log($"[PaintingPrefabManager] Initialized — pool prewarmed with {initialPoolSize}");

        if (APIManager.Instance != null)
        {
            APIManager.Instance.onApiResponseLoaded += OnAPILoaded;

            if (APIManager.Instance.apiResponse != null)
                LoadAllUsedPaintings();
        }
        else
        {
            Debug.LogError("[PaintingPrefabManager] APIManager not found!");
        }
    }

    private void OnDestroy()
    {
        if (APIManager.Instance != null)
            APIManager.Instance.onApiResponseLoaded -= OnAPILoaded;

        OnPaintingPrefabSpawned = null;
        OnPaintingPrefabRemoved = null;

        if (showDebug)
            Debug.Log("[PaintingPrefabManager] Destroyed");
    }

    // ════════════════════════════════════════════════
    // POOL HELPERS
    // ════════════════════════════════════════════════

    private void PrewarmPool(int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject obj = CreateNewPoolObject();
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
    }

    private GameObject CreateNewPoolObject()
    {
        GameObject obj = Instantiate(paintingPrefab, transform);
        return obj;
    }

    /// <summary>Lấy object từ pool hoặc tạo mới nếu pool rỗng.</summary>
    private GameObject GetFromPool()
    {
        while (pool.Count > 0)
        {
            GameObject obj = pool.Dequeue();
            if (obj != null)
            {
                obj.SetActive(true);
                return obj;
            }
        }
        // Pool cạn — tạo mới
        if (showDebug) Debug.Log("[PaintingPrefabManager] Pool empty — instantiating new object");
        return CreateNewPoolObject();
    }

    /// <summary>Trả object về pool. Nếu pool đã đầy thì Destroy thật.</summary>
    public void ReturnToPool(int paintingId)
    {
        if (!spawnedPaintingObjects.TryGetValue(paintingId, out GameObject obj)) return;

        spawnedPaintingObjects.Remove(paintingId);
        spawnedPaintingPrefabs.Remove(paintingId);

        OnPaintingPrefabRemoved?.Invoke(paintingId);

        if (obj == null) return;

        if (pool.Count < maxPoolSize)
        {
            obj.SetActive(false);
            pool.Enqueue(obj);
            if (showDebug) Debug.Log($"[PaintingPrefabManager] Returned to pool: ID {paintingId}");
        }
        else
        {
            Destroy(obj);
            if (showDebug) Debug.Log($"[PaintingPrefabManager] Pool full — destroyed: ID {paintingId}");
        }
    }

    // ════════════════════════════════════════════════
    // API EVENTS
    // ════════════════════════════════════════════════

    private void OnAPILoaded(APIResponse response)
    {
        if (showDebug) Debug.Log("[PaintingPrefabManager] API loaded — reloading paintings");
        ReloadAllPaintings();
    }

    // ════════════════════════════════════════════════
    // SPAWN — PUBLIC
    // ════════════════════════════════════════════════

    public GameObject SpawnPaintingFromItem(Painting painting, Texture2D texture)
    {
        if (painting == null || texture == null)
        {
            Debug.LogError("[PaintingPrefabManager] Invalid painting or texture!");
            return null;
        }

        if (spawnedPaintingObjects.ContainsKey(painting.id))
        {
            if (showDebug)
                Debug.LogWarning($"[PaintingPrefabManager] Already spawned: {painting.name} (ID:{painting.id})");
            return spawnedPaintingObjects[painting.id];
        }

        return SpawnInFrontOfCamera(painting, texture);
    }

    // ════════════════════════════════════════════════
    // SPAWN — INTERNAL
    // ════════════════════════════════════════════════

    private GameObject SpawnInFrontOfCamera(Painting painting, Texture2D texture)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[PaintingPrefabManager] Main camera not found!");
            return null;
        }

        Vector3    spawnPosition = mainCamera.transform.position + mainCamera.transform.forward * spawnDistance;
        spawnPosition.y = mainCamera.transform.position.y;
        Quaternion spawnRotation = Quaternion.LookRotation(-mainCamera.transform.forward);

        return SpawnObject(painting, texture, spawnPosition, spawnRotation);
    }

    private GameObject SpawnAtPosition(Painting painting, Texture2D texture, Position pos, Rotation rot)
    {
        if (spawnedPaintingObjects.ContainsKey(painting.id))
        {
            if (showDebug)
                Debug.LogWarning($"[PaintingPrefabManager] Already spawned: {painting.name} (ID:{painting.id})");
            return spawnedPaintingObjects[painting.id];
        }

        Vector3    position = new Vector3(pos.x, pos.y, pos.z);
        Quaternion rotation = Quaternion.Euler(rot.x, rot.y, rot.z);

        return SpawnObject(painting, texture, position, rotation);
    }

    private GameObject SpawnObject(Painting painting, Texture2D texture, Vector3 position, Quaternion rotation)
    {
        GameObject paintingObj = GetFromPool();

        paintingObj.name             = $"Painting_{painting.id}_{painting.name}";
        paintingObj.transform.position = position;
        paintingObj.transform.rotation = rotation;

        PaintingPrefab prefabComponent = paintingObj.GetComponent<PaintingPrefab>();

        if (prefabComponent == null)
        {
            Debug.LogError($"[PaintingPrefabManager] PaintingPrefab component missing on: {painting.name}");
            ReturnObjectToPoolRaw(paintingObj);
            return null;
        }

        prefabComponent.Setup(painting, texture);

        spawnedPaintingObjects[painting.id]  = paintingObj;
        spawnedPaintingPrefabs[painting.id]  = prefabComponent;

        StartCoroutine(TriggerSpawnEventNextFrame(painting.id, prefabComponent));

        if (showDebug)
            Debug.Log($"[PaintingPrefabManager] Spawned: {painting.name} (ID:{painting.id}) at {position}");

        return paintingObj;
    }

    private IEnumerator TriggerSpawnEventNextFrame(int paintingId, PaintingPrefab prefab)
    {
        yield return null;
        OnPaintingPrefabSpawned?.Invoke(paintingId, prefab);
    }

    // Raw return — dùng khi object chưa đăng ký vào dict
    private void ReturnObjectToPoolRaw(GameObject obj)
    {
        if (obj == null) return;
        if (pool.Count < maxPoolSize)
        {
            obj.SetActive(false);
            pool.Enqueue(obj);
        }
        else
        {
            Destroy(obj);
        }
    }

    // ════════════════════════════════════════════════
    // LOAD ALL
    // ════════════════════════════════════════════════

    public void LoadAllUsedPaintings()
    {
        if (isLoadingPaintings)
        {
            if (showDebug) Debug.LogWarning("[PaintingPrefabManager] Already loading!");
            return;
        }

        if (APIManager.Instance == null || APIManager.Instance.apiResponse == null)
        {
            if (showDebug) Debug.LogWarning("[PaintingPrefabManager] API data not ready!");
            return;
        }

        StartCoroutine(LoadUsedPaintingsCoroutine());
    }

    private IEnumerator LoadUsedPaintingsCoroutine()
    {
        isLoadingPaintings = true;

        var paintings = APIManager.Instance.GetPaintingList();
        if (paintings == null || paintings.Count == 0)
        {
            if (showDebug) Debug.Log("[PaintingPrefabManager] No paintings in API");
            isLoadingPaintings = false;
            yield break;
        }

        var paintingsToLoad = new List<Painting>();
        foreach (var p in paintings)
        {
            if (p.is_used == 1 &&
                !string.IsNullOrEmpty(p.path_url) &&
                p.position != null &&
                p.rotate   != null)
            {
                paintingsToLoad.Add(p);
            }
        }

        if (showDebug) Debug.Log($"[PaintingPrefabManager] Loading {paintingsToLoad.Count} paintings...");

        int loaded = 0;
        foreach (var painting in paintingsToLoad)
        {
            yield return StartCoroutine(LoadAndSpawnPainting(painting));
            loaded++;
            if (showDebug) Debug.Log($"[PaintingPrefabManager] Progress: {loaded}/{paintingsToLoad.Count}");
            yield return new WaitForSeconds(delayBetweenSpawns);
        }

        if (showDebug) Debug.Log($"[PaintingPrefabManager] Finished loading {loaded} paintings");
        isLoadingPaintings = false;
    }

    private IEnumerator LoadAndSpawnPainting(Painting painting)
    {
        // Dùng TextureCache — không download lại nếu đã có
        bool done = false;
        Texture2D texture = null;

        if (TextureCache.Instance != null)
        {
            TextureCache.Instance.GetTexture(painting.path_url, 10, (tex) =>
            {
                texture = tex;
                done    = true;
            });
            yield return new WaitUntil(() => done);
        }
        else
        {
            // Fallback nếu TextureCache chưa có trên scene
            using (var req = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(painting.path_url))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                    texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(req);
                else
                    Debug.LogError($"[PaintingPrefabManager] Failed to load texture: {painting.name} — {req.error}");
            }
        }

        if (texture != null)
            SpawnAtPosition(painting, texture, painting.position, painting.rotate);
        else
            Debug.LogError($"[PaintingPrefabManager] Texture null: {painting.name}");
    }

    // ════════════════════════════════════════════════
    // REMOVE / CLEAR
    // ════════════════════════════════════════════════

    public void RemovePainting(int paintingId)
    {
        if (spawnedPaintingObjects.ContainsKey(paintingId))
        {
            if (showDebug) Debug.Log($"[PaintingPrefabManager] Removing ID: {paintingId}");
            ReturnToPool(paintingId);
        }
        else
        {
            if (showDebug) Debug.LogWarning($"[PaintingPrefabManager] Painting not found: {paintingId}");
        }
    }

    public void ClearAllPaintings()
    {
        if (showDebug) Debug.Log($"[PaintingPrefabManager] Clearing {spawnedPaintingObjects.Count} paintings...");

        // Copy keys vì dict bị modify trong ReturnToPool
        var ids = new List<int>(spawnedPaintingObjects.Keys);
        foreach (int id in ids)
            ReturnToPool(id);

        if (showDebug) Debug.Log("[PaintingPrefabManager] All paintings cleared");
    }

    public void ReloadAllPaintings()
    {
        if (showDebug) Debug.Log("[PaintingPrefabManager] Reloading...");
        ClearAllPaintings();
        LoadAllUsedPaintings();
    }

    // ════════════════════════════════════════════════
    // QUERIES
    // ════════════════════════════════════════════════

    public bool IsPrefabSpawned(int paintingId)           => spawnedPaintingObjects.ContainsKey(paintingId);
    public int  GetSpawnedPaintingCount()                  => spawnedPaintingObjects.Count;

    public GameObject GetSpawnedPainting(int paintingId)
    {
        spawnedPaintingObjects.TryGetValue(paintingId, out GameObject obj);
        return obj;
    }

    public PaintingPrefab FindPrefabByID(int paintingId)
    {
        spawnedPaintingPrefabs.TryGetValue(paintingId, out PaintingPrefab p);
        return p;
    }

    public Dictionary<int, PaintingPrefab> GetAllSpawnedPrefabs()
        => new Dictionary<int, PaintingPrefab>(spawnedPaintingPrefabs);

#if UNITY_EDITOR
    [Header("Runtime Info (Read Only)")]
    [SerializeField] private int spawnedCount;
    [SerializeField] private int poolCount;
    [SerializeField] private bool isLoading;

    private void Update()
    {
        spawnedCount = spawnedPaintingObjects.Count;
        poolCount    = pool.Count;
        isLoading    = isLoadingPaintings;
    }
#endif
}
