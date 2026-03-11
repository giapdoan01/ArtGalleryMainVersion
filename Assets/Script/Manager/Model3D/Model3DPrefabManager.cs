using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using GLTFast;

public class Model3DPrefabManager : MonoBehaviour
{
    private static Model3DPrefabManager instance;
    public static Model3DPrefabManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<Model3DPrefabManager>();
            }
            return instance;
        }
    }

    [Header("Prefab")]
    [SerializeField] private GameObject model3DPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnDistance = 3f;
    [SerializeField] private float delayBetweenSpawns = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    //  Dictionary lưu model đã spawn
    private Dictionary<int, GameObject> spawnedModels = new Dictionary<int, GameObject>();
    
    //  Dictionary lưu rotation gốc của GLB (khi load lần đầu)
    private Dictionary<int, Vector3> originalRotations = new Dictionary<int, Vector3>();
    
    private bool isLoadingModels = false;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (model3DPrefab == null)
        {
            Debug.LogError("[Model3DPrefabManager] model3DPrefab is NULL! Assign it in Inspector!");
            return;
        }

        if (APIManager.Instance != null)
        {
            APIManager.Instance.onApiResponseLoaded += OnAPILoaded;

            if (APIManager.Instance.apiResponse != null)
            {
                LoadAllUsedModels();
            }
        }
        else
        {
            Debug.LogError("[Model3DPrefabManager] APIManager not found!");
        }
    }

    private void OnAPILoaded(APIResponse response)
    {
        ReloadAllModels();
    }

    //  THÊM: Lấy rotation gốc của model (từ GLB đã load)
    public Vector3 GetOriginalRotation(int modelId)
    {
        if (originalRotations.ContainsKey(modelId))
        {
            return originalRotations[modelId];
        }

        //  Nếu chưa có, thử lấy từ model đã spawn
        if (spawnedModels.ContainsKey(modelId))
        {
            GameObject modelObj = spawnedModels[modelId];
            if (modelObj != null)
            {
                Model3DPrefab prefab = modelObj.GetComponent<Model3DPrefab>();
                if (prefab != null && prefab.glbInstance != null)
                {
                    Vector3 rotation = prefab.glbInstance.transform.localEulerAngles;
                    
                    // Lưu lại để dùng sau
                    originalRotations[modelId] = rotation;
                    
                    if (showDebug)
                        Debug.Log($"[Model3DPrefabManager] Cached original rotation for ID {modelId}: {rotation}");
                    
                    return rotation;
                }
            }
        }

        //  Mặc định trả về (0,0,0)
        if (showDebug)
            Debug.LogWarning($"[Model3DPrefabManager] No original rotation found for ID {modelId}, using (0,0,0)");
        
        return Vector3.zero;
    }

    //  THÊM: Lưu rotation gốc khi GLB load xong
    public void RegisterOriginalRotation(int modelId, Vector3 rotation)
    {
        if (!originalRotations.ContainsKey(modelId))
        {
            originalRotations[modelId] = rotation;
            
            if (showDebug)
                Debug.Log($"[Model3DPrefabManager] Registered original rotation for ID {modelId}: {rotation}");
        }
    }

    public GameObject SpawnModel3DFromItem(Model3D model3D, Texture2D thumbnail)
    {
        if (model3D == null || thumbnail == null)
        {
            Debug.LogError("[Model3DPrefabManager] Invalid model3D or thumbnail!");
            return null;
        }

        if (spawnedModels.ContainsKey(model3D.id))
        {
            if (showDebug)
                Debug.LogWarning($"[Model3DPrefabManager] Model {model3D.id} already spawned!");
            return spawnedModels[model3D.id];
        }

        return SpawnInFrontOfCamera(model3D, thumbnail);
    }

    private GameObject SpawnInFrontOfCamera(Model3D model3D, Texture2D thumbnail)
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[Model3DPrefabManager] Main camera not found!");
            return null;
        }

        Vector3 spawnPosition = mainCamera.transform.position + mainCamera.transform.forward * spawnDistance;
        spawnPosition.y = mainCamera.transform.position.y;

        Quaternion spawnRotation = Quaternion.LookRotation(-mainCamera.transform.forward);

        GameObject modelObj = Instantiate(model3DPrefab, spawnPosition, spawnRotation);

        if (modelObj == null)
        {
            Debug.LogError("[Model3DPrefabManager] Failed to instantiate model3D");
            return null;
        }

        modelObj.name = $"Model3DPrefab({model3D.name})";

        Model3DPrefab prefab = modelObj.GetComponent<Model3DPrefab>();

        if (prefab != null)
        {
            prefab.Setup(model3D, thumbnail);
            
            //  Lưu vào dictionary
            spawnedModels[model3D.id] = modelObj;
            
            return modelObj;
        }
        else
        {
            Debug.LogError("[Model3DPrefabManager] Model3DPrefab component not found!");
            Destroy(modelObj);
            return null;
        }
    }

    private GameObject SpawnAtPosition(Model3D model3D, Texture2D thumbnail, Position pos, Rotation rot, Size size)
    {
        if (spawnedModels.ContainsKey(model3D.id))
        {
            if (showDebug)
                Debug.LogWarning($"[Model3DPrefabManager] Model {model3D.id} already spawned!");
            return spawnedModels[model3D.id];
        }

        Vector3 position = new Vector3(pos.x, pos.y, pos.z);
        Quaternion rotation = Quaternion.Euler(rot.x, rot.y, rot.z);

        GameObject modelObj = Instantiate(model3DPrefab, position, rotation);

        if (modelObj == null)
        {
            Debug.LogError($"[Model3DPrefabManager] Failed to instantiate: {model3D.name}");
            return null;
        }

        modelObj.name = $"Model3DPrefab_{model3D.id}_{model3D.name}";

        Model3DPrefab prefab = modelObj.GetComponent<Model3DPrefab>();

        if (prefab == null)
        {
            Debug.LogError($"[Model3DPrefabManager] Model3DPrefab component not found on: {model3D.name}");
            Destroy(modelObj);
            return null;
        }

        prefab.Setup(model3D, thumbnail);

        spawnedModels.Add(model3D.id, modelObj);

        if (showDebug)
            Debug.Log($"[Model3DPrefabManager] Spawned model {model3D.id} at position {position}");

        return modelObj;
    }

    public void LoadAllUsedModels()
    {
        if (isLoadingModels)
        {
            if (showDebug)
                Debug.LogWarning("[Model3DPrefabManager] Already loading models!");
            return;
        }

        if (APIManager.Instance == null || APIManager.Instance.apiResponse == null)
        {
            if (showDebug)
                Debug.LogWarning("[Model3DPrefabManager] APIManager or apiResponse is null!");
            return;
        }

        StartCoroutine(LoadUsedModelsCoroutine());
    }

    private IEnumerator LoadUsedModelsCoroutine()
    {
        isLoadingModels = true;

        var models = APIManager.Instance.GetModel3DList();

        if (models == null || models.Count == 0)
        {
            if (showDebug)
                Debug.LogWarning("[Model3DPrefabManager] No models to load!");
            
            isLoadingModels = false;
            yield break;
        }

        List<Model3D> modelsToLoad = new List<Model3D>();

        foreach (var model3D in models)
        {
            if (model3D.is_used == 1 &&
                !string.IsNullOrEmpty(model3D.path_url) &&
                model3D.position != null &&
                model3D.rotate != null)
            {
                modelsToLoad.Add(model3D);
            }
        }

        if (showDebug)
            Debug.Log($"[Model3DPrefabManager] Loading {modelsToLoad.Count} used models...");

        foreach (var model3D in modelsToLoad)
        {
            yield return StartCoroutine(LoadAndSpawnModel(model3D));
            yield return new WaitForSeconds(delayBetweenSpawns);
        }

        if (showDebug)
            Debug.Log($"[Model3DPrefabManager] Finished loading all models!");

        isLoadingModels = false;
    }

    private IEnumerator LoadAndSpawnModel(Model3D model3D)
    {
        Texture2D thumbnail = null;

        if (!string.IsNullOrEmpty(model3D.thumbnail_url))
        {
            using (UnityEngine.Networking.UnityWebRequest request =
                   UnityEngine.Networking.UnityWebRequestTexture.GetTexture(model3D.thumbnail_url))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();

                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    thumbnail = UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);
                }
                else
                {
                    Debug.LogError($"[Model3DPrefabManager] Failed to load thumbnail: {model3D.name}");
                }
            }
        }

        SpawnAtPosition(model3D, thumbnail, model3D.position, model3D.rotate, model3D.size);
    }

    public void RemoveModel(int modelId)
    {
        if (spawnedModels.ContainsKey(modelId))
        {
            GameObject modelObj = spawnedModels[modelId];

            if (modelObj != null)
            {
                Destroy(modelObj);
            }

            spawnedModels.Remove(modelId);
            
            if (showDebug)
                Debug.Log($"[Model3DPrefabManager] Removed model {modelId}");
        }
    }

    public bool IsModel3DSpawned(int modelId)
    {
        return spawnedModels.ContainsKey(modelId);
    }

    public GameObject GetSpawnedModel(int modelId)
    {
        if (spawnedModels.ContainsKey(modelId))
        {
            return spawnedModels[modelId];
        }
        return null;
    }

    public void ClearAllModels()
    {
        foreach (var kvp in spawnedModels)
        {
            if (kvp.Value != null)
            {
                Destroy(kvp.Value);
            }
        }

        spawnedModels.Clear();
        
        if (showDebug)
            Debug.Log("[Model3DPrefabManager] Cleared all models");
    }

    public void ReloadAllModels()
    {
        if (showDebug)
            Debug.Log("[Model3DPrefabManager] Reloading all models...");
        
        ClearAllModels();
        LoadAllUsedModels();
    }

    private void OnDestroy()
    {
        if (APIManager.Instance != null)
        {
            APIManager.Instance.onApiResponseLoaded -= OnAPILoaded;
        }
    }
}
