using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Linq;
using System;

public class APIManager : MonoBehaviour
{
    public static APIManager Instance;

    [SerializeField] private string baseUrl = "https://projects-admin.vr360.com.vn/api/phongtranh";
    [SerializeField] private string tokenProject = "0733620161f67af3b761f113f7142a61";

    [Header("Auto Refresh Settings")]
    [SerializeField] private bool enableAutoRefresh = true;
    [SerializeField] private float refreshInterval = 30f;

    public int timeout = 30;
    [NonSerialized]
    public APIResponse apiResponse;

    public Action<APIResponse> onApiResponseLoaded;
    public Action<APIResponse> onApiResponseRefreshed;

    private Coroutine autoRefreshCoroutine;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        GetDataFromAPI();

        if (enableAutoRefresh)
        {
            StartAutoRefresh();
        }
    }

    public void StartAutoRefresh()
    {
        if (autoRefreshCoroutine != null)
        {
            StopCoroutine(autoRefreshCoroutine);
        }

        autoRefreshCoroutine = StartCoroutine(AutoRefreshCoroutine());
        Debug.Log($"[APIManager] Auto refresh started (interval: {refreshInterval}s)");
    }

    public void StopAutoRefresh()
    {
        if (autoRefreshCoroutine != null)
        {
            StopCoroutine(autoRefreshCoroutine);
            autoRefreshCoroutine = null;
        }

        Debug.Log("[APIManager] Auto refresh stopped");
    }

    private IEnumerator AutoRefreshCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(refreshInterval);

            Debug.Log("[APIManager] Auto refreshing data...");
            yield return RefreshDataCoroutine();
        }
    }

    public IEnumerator RefreshDataCoroutine()
    {
        string getDataUrl = $"{baseUrl}/data-masters";

        using (UnityWebRequest request = UnityWebRequest.Get(getDataUrl))
        {
            request.timeout = timeout;
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Project", tokenProject);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[APIManager] Data refreshed successfully");

                APIResponse oldResponse = apiResponse;
                apiResponse = JsonUtility.FromJson<APIResponse>(request.downloadHandler.text);

                if (HasDataChanged(oldResponse, apiResponse))
                {
                    Debug.Log("[APIManager] Data changed! Notifying listeners...");
                    onApiResponseRefreshed?.Invoke(apiResponse);
                }
                else
                {
                    Debug.Log("[APIManager] No data changes detected");
                }
            }
            else
            {
                Debug.LogError($"[APIManager] Refresh failed: {request.error}");
            }
        }
    }

    public void RefreshDataFromAPI()
    {
        StartCoroutine(RefreshDataCoroutine());
    }

    private bool HasDataChanged(APIResponse oldData, APIResponse newData)
    {
        if (oldData == null || newData == null)
        {
            Debug.Log("[APIManager] Data changed: null data detected");
            return true;
        }

        if (oldData.data == null || newData.data == null)
        {
            Debug.Log("[APIManager] Data changed: null data.data detected");
            return true;
        }

        //  CHECK MODEL3DS
        int oldModel3DCount = oldData.data.model3ds?.Count ?? 0;
        int newModel3DCount = newData.data.model3ds?.Count ?? 0;

        if (oldModel3DCount != newModel3DCount)
        {
            Debug.Log($"[APIManager] Model3D count changed: {oldModel3DCount} → {newModel3DCount}");
            return true;
        }

        //  CHECK MODEL3D is_used
        if (oldData.data.model3ds != null && newData.data.model3ds != null)
        {
            for (int i = 0; i < oldData.data.model3ds.Count; i++)
            {
                Model3D oldM = oldData.data.model3ds[i];
                Model3D newM = newData.data.model3ds.FirstOrDefault(m => m.id == oldM.id);

                if (newM == null)
                {
                    Debug.Log($"[APIManager] Model3D removed: {oldM.name} (ID: {oldM.id})");
                    return true;
                }

                if (oldM.is_used != newM.is_used)
                {
                    Debug.Log($"[APIManager] Model3D '{newM.name}' (ID: {newM.id}): is_used changed {oldM.is_used} → {newM.is_used}");
                    return true;
                }
            }
        }

        //  CHECK PAINTINGS
        int oldCount = oldData.data.paintings?.Count ?? 0;
        int newCount = newData.data.paintings?.Count ?? 0;

        if (oldCount != newCount)
        {
            Debug.Log($"[APIManager] Painting count changed: {oldCount} → {newCount}");
            return true;
        }

        if (oldData.data.paintings != null && newData.data.paintings != null)
        {
            for (int i = 0; i < oldData.data.paintings.Count; i++)
            {
                Painting oldP = oldData.data.paintings[i];
                Painting newP = newData.data.paintings.FirstOrDefault(p => p.id == oldP.id);

                if (newP == null) return true;

                if (oldP.is_used != newP.is_used)
                {
                    Debug.Log($"[APIManager] Painting {newP.name}: is_used changed {oldP.is_used} → {newP.is_used}");
                    return true;
                }

                if (oldP.frame_type != newP.frame_type)
                {
                    Debug.Log($"[APIManager] Painting {newP.name}: frame_type changed {oldP.frame_type} → {newP.frame_type}");
                    return true;
                }
            }
        }

        Debug.Log("[APIManager] No data changes detected");
        return false;
    }

    public void GetDataFromAPI()
    {
        string getDataUrl = $"{baseUrl}/data-masters";
        StartCoroutine(GetRequest(getDataUrl));
    }

    IEnumerator GetRequest(string url)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = timeout;
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Project", tokenProject);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("API Response: " + request.downloadHandler.text);
                apiResponse = JsonUtility.FromJson<APIResponse>(request.downloadHandler.text);
                onApiResponseLoaded?.Invoke(apiResponse);
            }
            else
            {
                Debug.LogError("API Error: " + request.error);
            }
        }
    }

    public List<Model3D> GetModel3DList()
    {
        if (apiResponse != null && apiResponse.data != null)
        {
            return apiResponse.data.model3ds;
        }
        return new List<Model3D>();
    }

    public Model3D GetModel3DById(int id)
    {
        return GetModel3DList().FirstOrDefault(m => m.id == id);
    }

    public List<Painting> GetPaintingList()
    {
        if (apiResponse != null && apiResponse.data != null)
        {
            return apiResponse.data.paintings;
        }
        return new List<Painting>();
    }

    public Painting GetPaintingById(int id)
    {
        return GetPaintingList().FirstOrDefault(p => p.id == id);
    }

    public void UpdatePaintingTransform(int id, Position position, Rotation rotation, Action onSuccess, Action<string> onError)
    {
        StartCoroutine(PatchPaintingTransform(id, position, rotation, onSuccess, onError));
    }

    public void UpdateModel3DTransform(int id, Position position, Rotation rotation, Size size, Action onSuccess, Action<string> onError)
    {
        StartCoroutine(PatchModel3DTransform(id, position, rotation, size, onSuccess, onError));
    }

    public void PatchPaintingIsUsedWithTransform(int id, int isUsed, Position position, Rotation rotation, Action onSuccess, Action<string> onError)
    {
        StartCoroutine(PatchPaintingIsUsedWithTransformCoroutine(id, isUsed, position, rotation, onSuccess, onError));
    }

    IEnumerator PatchPaintingTransform(int id, Position position, Rotation rotation, Action onSuccess, Action<string> onError)
    {
        string url = $"{baseUrl}/save-data";

        PaintingTransformUpdateData data = new PaintingTransformUpdateData
        {
            position = position,
            rotate = rotation
        };

        string json = JsonUtility.ToJson(data);

        using (UnityWebRequest request = new UnityWebRequest(url, "PATCH"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Id", id.ToString());
            request.SetRequestHeader("Type", "painting");
            request.SetRequestHeader("Project", tokenProject);
            request.timeout = timeout;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[APIManager] Painting transform updated successfully! ID: {id}");

                yield return RefreshDataCoroutine();

                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogError($"[APIManager] Painting update failed: {request.error}");
                onError?.Invoke(request.error);
            }
        }
    }

    IEnumerator PatchModel3DTransform(int id, Position position, Rotation rotation, Size size, Action onSuccess, Action<string> onError)
    {
        string url = $"{baseUrl}/save-data";

        Model3DTransformUpdateData data = new Model3DTransformUpdateData
        {
            position = position,
            rotate = rotation,
            size = size
        };

        string json = JsonUtility.ToJson(data);

        using (UnityWebRequest request = new UnityWebRequest(url, "PATCH"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Id", id.ToString());
            request.SetRequestHeader("Type", "model3d");
            request.SetRequestHeader("Project", tokenProject);
            request.timeout = timeout;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[APIManager] Model3D transform updated successfully! ID: {id}");

                yield return RefreshDataCoroutine();

                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogError($"[APIManager] Model3D update failed: {request.error}");
                onError?.Invoke(request.error);
            }
        }
    }

    IEnumerator PatchPaintingIsUsedWithTransformCoroutine(int id, int isUsed, Position position, Rotation rotation, Action onSuccess, Action<string> onError)
    {
        string url = $"{baseUrl}/save-data";

        PaintingFullUpdateData data = new PaintingFullUpdateData
        {
            is_used = isUsed,
            position = position,
            rotate = rotation
        };

        string json = JsonUtility.ToJson(data);

        Debug.Log($"[APIManager] Patching painting with data: {json}");

        using (UnityWebRequest request = new UnityWebRequest(url, "PATCH"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Id", id.ToString());
            request.SetRequestHeader("Type", "painting");
            request.SetRequestHeader("Project", tokenProject);
            request.timeout = timeout;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[APIManager] Painting is_used + transform updated successfully! ID: {id}");

                yield return RefreshDataCoroutine();

                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogError($"[APIManager] Painting update failed: {request.error}");
                onError?.Invoke(request.error);
            }
        }
    }

    public void PatchPaintingWithFrameType(int id, int isUsed, string frameType, Position position, Rotation rotation, Action onSuccess, Action<string> onError)
    {
        StartCoroutine(PatchPaintingWithFrameTypeCoroutine(id, isUsed, frameType, position, rotation, onSuccess, onError));
    }

    IEnumerator PatchPaintingWithFrameTypeCoroutine(int id, int isUsed, string frameType, Position position, Rotation rotation, Action onSuccess, Action<string> onError)
    {
        string url = $"{baseUrl}/save-data";

        PaintingFullUpdateWithFrameData data = new PaintingFullUpdateWithFrameData
        {
            is_used = isUsed,
            frame_type = frameType,
            position = position,
            rotate = rotation
        };

        string json = JsonUtility.ToJson(data);

        Debug.Log($"[APIManager] Patching painting with frame_type: {json}");

        using (UnityWebRequest request = new UnityWebRequest(url, "PATCH"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Id", id.ToString());
            request.SetRequestHeader("Type", "painting");
            request.SetRequestHeader("Project", tokenProject);
            request.timeout = timeout;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[APIManager] Painting updated with frame_type! ID: {id}");

                yield return RefreshDataCoroutine();

                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogError($"[APIManager] Painting update failed: {request.error}");
                onError?.Invoke(request.error);
            }
        }
    }

    public void RemovePaintingOrModel(int id, string type, Action onSuccess, Action<string> onError)
    {
        StartCoroutine(RemovePaintingOrModelCoroutine(id, type, onSuccess, onError));
    }

    IEnumerator RemovePaintingOrModelCoroutine(int id, string type, Action onSuccess, Action<string> onError)
    {
        string url = $"{baseUrl}/remove-data";

        Debug.Log($"[APIManager] Removing {type} ID: {id}");

        using (UnityWebRequest request = new UnityWebRequest(url, "PATCH"))
        {
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Id", id.ToString());
            request.SetRequestHeader("Type", type);
            request.SetRequestHeader("Project", tokenProject);
            request.timeout = timeout;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[APIManager]  {type} removed successfully! ID: {id}");

                yield return RefreshDataCoroutine();

                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogError($"[APIManager]  {type} removal failed: {request.error}");
                onError?.Invoke(request.error);
            }
        }
    }

    public void PatchModel3DWithUsed(int id, int isUsed, Position position, Rotation rotation, Size size, Action onSuccess, Action<string> onError)
    {
        StartCoroutine(PatchModel3DWithUsedCoroutine(id, isUsed, position, rotation, size, onSuccess, onError));
    }

    IEnumerator PatchModel3DWithUsedCoroutine(int id, int isUsed, Position position, Rotation rotation, Size size, Action onSuccess, Action<string> onError)
    {
        string url = $"{baseUrl}/save-data";

        Model3DFullUpdateData data = new Model3DFullUpdateData
        {
            is_used = isUsed,  
            position = position,
            rotate = rotation,
            size = size
        };

        string json = JsonUtility.ToJson(data);

        Debug.Log($"[APIManager] Patching model3D with is_used: {json}");

        using (UnityWebRequest request = new UnityWebRequest(url, "PATCH"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Id", id.ToString());
            request.SetRequestHeader("Type", "model3d");
            request.SetRequestHeader("Project", tokenProject);
            request.timeout = timeout;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[APIManager]  Model3D updated with is_used! ID: {id}");

                yield return RefreshDataCoroutine();

                onSuccess?.Invoke();
            }
            else
            {
                Debug.LogError($"[APIManager]  Model3D update failed: {request.error}");
                onError?.Invoke(request.error);
            }
        }
    }
    private void OnDestroy()
    {
        StopAutoRefresh();
    }
}



