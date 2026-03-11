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
            {
                instance = FindObjectOfType<PaintingPrefabManager>();
            }
            return instance;
        }
    }

    //  EVENT: Trigger khi prefab được spawn
    public static event Action<int, PaintingPrefab> OnPaintingPrefabSpawned;
    
    //  EVENT: Trigger khi prefab bị remove
    public static event Action<int> OnPaintingPrefabRemoved;

    [Header("Prefab")]
    [SerializeField] private GameObject paintingPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnDistance = 2f;
    [SerializeField] private float delayBetweenSpawns = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    //  Lưu cả GameObject và PaintingPrefab component
    private Dictionary<int, GameObject> spawnedPaintingObjects = new Dictionary<int, GameObject>();
    private Dictionary<int, PaintingPrefab> spawnedPaintingPrefabs = new Dictionary<int, PaintingPrefab>();
    
    private bool isLoadingPaintings = false;

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
    }

    private void Start()
    {
        if (paintingPrefab == null)
        {
            Debug.LogError("[PaintingPrefabManager] paintingPrefab is NULL! Assign it in Inspector!");
            return;
        }

        if (showDebug)
            Debug.Log("[PaintingPrefabManager] Initialized");

        if (APIManager.Instance != null)
        {
            APIManager.Instance.onApiResponseLoaded += OnAPILoaded;

            if (APIManager.Instance.apiResponse != null)
            {
                if (showDebug)
                    Debug.Log("[PaintingPrefabManager] API data already loaded, loading paintings...");
                
                LoadAllUsedPaintings();
            }
            else
            {
                if (showDebug)
                    Debug.Log("[PaintingPrefabManager] Waiting for API data...");
            }
        }
        else
        {
            Debug.LogError("[PaintingPrefabManager] APIManager not found!");
        }
    }

    private void OnAPILoaded(APIResponse response)
    {
        if (showDebug)
            Debug.Log("[PaintingPrefabManager] API data loaded, reloading paintings...");
        
        ReloadAllPaintings();
    }

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
                Debug.LogWarning($"[PaintingPrefabManager] Painting already spawned: {painting.name} (ID: {painting.id})");
            
            return spawnedPaintingObjects[painting.id];
        }

        if (showDebug)
            Debug.Log($"[PaintingPrefabManager] Spawning from item: {painting.name} (ID: {painting.id})");

        return SpawnInFrontOfCamera(painting, texture);
    }

    private GameObject SpawnInFrontOfCamera(Painting painting, Texture2D texture)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[PaintingPrefabManager] Main camera not found!");
            return null;
        }

        Vector3 spawnPosition = mainCamera.transform.position + mainCamera.transform.forward * spawnDistance;
        spawnPosition.y = mainCamera.transform.position.y;

        Quaternion spawnRotation = Quaternion.LookRotation(-mainCamera.transform.forward);

        GameObject paintingObj = Instantiate(paintingPrefab, spawnPosition, spawnRotation);

        if (paintingObj == null)
        {
            Debug.LogError("[PaintingPrefabManager] Failed to instantiate painting");
            return null;
        }

        paintingObj.name = $"Painting_{painting.id}_{painting.name}";

        PaintingPrefab prefabComponent = paintingObj.GetComponent<PaintingPrefab>();

        if (prefabComponent != null)
        {
            prefabComponent.Setup(painting, texture);

            spawnedPaintingObjects[painting.id] = paintingObj;
            spawnedPaintingPrefabs[painting.id] = prefabComponent;

            StartCoroutine(TriggerSpawnEventNextFrame(painting.id, prefabComponent));

            if (showDebug)
                Debug.Log($"[PaintingPrefabManager]  Spawned in front of camera: {painting.name} (ID: {painting.id})");

            return paintingObj;
        }
        else
        {
            Debug.LogError("[PaintingPrefabManager] PaintingPrefab component not found!");
            Destroy(paintingObj);
            return null;
        }
    }

    private GameObject SpawnAtPosition(Painting painting, Texture2D texture, Position pos, Rotation rot)
    {
        if (spawnedPaintingObjects.ContainsKey(painting.id))
        {
            if (showDebug)
                Debug.LogWarning($"[PaintingPrefabManager] Painting already spawned: {painting.name} (ID: {painting.id})");
            
            return spawnedPaintingObjects[painting.id];
        }

        Vector3 position = new Vector3(pos.x, pos.y, pos.z);
        Quaternion rotation = Quaternion.Euler(rot.x, rot.y, rot.z);

        GameObject paintingObj = Instantiate(paintingPrefab, position, rotation);

        if (paintingObj == null)
        {
            Debug.LogError($"[PaintingPrefabManager] Failed to instantiate: {painting.name}");
            return null;
        }

        paintingObj.name = $"Painting_{painting.id}_{painting.name}";

        PaintingPrefab prefabComponent = paintingObj.GetComponent<PaintingPrefab>();

        if (prefabComponent == null)
        {
            Debug.LogError($"[PaintingPrefabManager] PaintingPrefab component not found on: {painting.name}");
            Destroy(paintingObj);
            return null;
        }

        prefabComponent.Setup(painting, texture);

        spawnedPaintingObjects[painting.id] = paintingObj;
        spawnedPaintingPrefabs[painting.id] = prefabComponent;

        StartCoroutine(TriggerSpawnEventNextFrame(painting.id, prefabComponent));

        if (showDebug)
            Debug.Log($"[PaintingPrefabManager]  Spawned at position: {painting.name} (ID: {painting.id}) at {position}");

        return paintingObj;
    }

    private IEnumerator TriggerSpawnEventNextFrame(int paintingId, PaintingPrefab prefab)
    {
        yield return null;

        OnPaintingPrefabSpawned?.Invoke(paintingId, prefab);

        if (showDebug)
            Debug.Log($"[PaintingPrefabManager]  Event triggered for painting ID: {paintingId}");
    }

    public void LoadAllUsedPaintings()
    {
        if (isLoadingPaintings)
        {
            if (showDebug)
                Debug.LogWarning("[PaintingPrefabManager] Already loading paintings!");
            return;
        }

        if (APIManager.Instance == null || APIManager.Instance.apiResponse == null)
        {
            if (showDebug)
                Debug.LogWarning("[PaintingPrefabManager] API data not ready!");
            return;
        }

        StartCoroutine(LoadUsedPaintingsCoroutine());
    }

    private IEnumerator LoadUsedPaintingsCoroutine()
    {
        isLoadingPaintings = true;

        if (showDebug)
            Debug.Log("[PaintingPrefabManager] Loading used paintings...");

        var paintings = APIManager.Instance.GetPaintingList();

        if (paintings == null || paintings.Count == 0)
        {
            if (showDebug)
                Debug.Log("[PaintingPrefabManager] No paintings found in API");
            
            isLoadingPaintings = false;
            yield break;
        }

        List<Painting> paintingsToLoad = new List<Painting>();

        foreach (var painting in paintings)
        {
            if (painting.is_used == 1 &&
                !string.IsNullOrEmpty(painting.path_url) &&
                painting.position != null &&
                painting.rotate != null)
            {
                paintingsToLoad.Add(painting);
            }
        }

        if (showDebug)
            Debug.Log($"[PaintingPrefabManager] Found {paintingsToLoad.Count} used paintings to load");

        int loadedCount = 0;
        foreach (var painting in paintingsToLoad)
        {
            yield return StartCoroutine(LoadAndSpawnPainting(painting));
            loadedCount++;

            if (showDebug)
                Debug.Log($"[PaintingPrefabManager] Progress: {loadedCount}/{paintingsToLoad.Count}");

            yield return new WaitForSeconds(delayBetweenSpawns);
        }

        if (showDebug)
            Debug.Log($"[PaintingPrefabManager]  Finished loading {loadedCount} paintings");

        isLoadingPaintings = false;
    }

    private IEnumerator LoadAndSpawnPainting(Painting painting)
    {
        if (showDebug)
            Debug.Log($"[PaintingPrefabManager] Loading texture for: {painting.name} (ID: {painting.id})");

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
                    SpawnAtPosition(painting, texture, painting.position, painting.rotate);
                }
                else
                {
                    Debug.LogError($"[PaintingPrefabManager] Texture is null: {painting.name}");
                }
            }
            else
            {
                Debug.LogError($"[PaintingPrefabManager] Failed to load texture: {painting.name} - {request.error}");
            }
        }
    }

    public void RemovePainting(int paintingId)
    {
        if (spawnedPaintingObjects.ContainsKey(paintingId))
        {
            GameObject paintingObj = spawnedPaintingObjects[paintingId];

            if (paintingObj != null)
            {
                if (showDebug)
                    Debug.Log($"[PaintingPrefabManager] Removing painting ID: {paintingId}");

                Destroy(paintingObj);
            }

            spawnedPaintingObjects.Remove(paintingId);
            spawnedPaintingPrefabs.Remove(paintingId);

            OnPaintingPrefabRemoved?.Invoke(paintingId);

            if (showDebug)
                Debug.Log($"[PaintingPrefabManager] Remove event triggered for ID: {paintingId}");
        }
        else
        {
            if (showDebug)
                Debug.LogWarning($"[PaintingPrefabManager] Painting not found: {paintingId}");
        }
    }

    public bool IsPrefabSpawned(int paintingId)
    {
        return spawnedPaintingObjects.ContainsKey(paintingId);
    }

    public GameObject GetSpawnedPainting(int paintingId)
    {
        if (spawnedPaintingObjects.ContainsKey(paintingId))
        {
            return spawnedPaintingObjects[paintingId];
        }
        return null;
    }

    public PaintingPrefab FindPrefabByID(int paintingId)
    {
        if (spawnedPaintingPrefabs.ContainsKey(paintingId))
        {
            return spawnedPaintingPrefabs[paintingId];
        }
        return null;
    }

    public Dictionary<int, PaintingPrefab> GetAllSpawnedPrefabs()
    {
        return new Dictionary<int, PaintingPrefab>(spawnedPaintingPrefabs);
    }

    public void ClearAllPaintings()
    {
        if (showDebug)
            Debug.Log($"[PaintingPrefabManager] Clearing {spawnedPaintingObjects.Count} paintings...");

        foreach (var kvp in spawnedPaintingObjects)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }

        spawnedPaintingObjects.Clear();
        spawnedPaintingPrefabs.Clear();

        if (showDebug)
            Debug.Log("[PaintingPrefabManager]  All paintings cleared");
    }

    public void ReloadAllPaintings()
    {
        if (showDebug)
            Debug.Log("[PaintingPrefabManager] Reloading all paintings...");

        ClearAllPaintings();
        LoadAllUsedPaintings();
    }

    public int GetSpawnedPaintingCount()
    {
        return spawnedPaintingObjects.Count;
    }

    private void OnDestroy()
    {
        if (APIManager.Instance != null)
        {
            APIManager.Instance.onApiResponseLoaded -= OnAPILoaded;
        }

        OnPaintingPrefabSpawned = null;
        OnPaintingPrefabRemoved = null;

        if (showDebug)
            Debug.Log("[PaintingPrefabManager] Destroyed");
    }

#if UNITY_EDITOR
    [Header("Runtime Info (Read Only)")]
    [SerializeField] private int spawnedCount = 0;
    [SerializeField] private bool isLoading = false;

    private void Update()
    {
        spawnedCount = spawnedPaintingObjects.Count;
        isLoading = isLoadingPaintings;
    }
#endif
}
