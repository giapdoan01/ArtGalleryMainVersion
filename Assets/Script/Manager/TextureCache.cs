using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System;

/// <summary>
/// Cache Texture2D theo URL để tránh download lại khi reload gallery.
/// Lifetime = scene (DontDestroyOnLoad không dùng — gallery là single-scene).
/// </summary>
public class TextureCache : MonoBehaviour
{
    private static TextureCache instance;
    public static TextureCache Instance
    {
        get
        {
            if (instance == null)
                instance = FindObjectOfType<TextureCache>();
            return instance;
        }
    }

    [Header("Settings")]
    [Tooltip("Số texture tối đa giữ trong cache. Khi vượt quá, LRU cũ nhất bị xóa.")]
    [SerializeField] private int maxCacheSize = 100;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // url → texture
    private readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

    // LRU tracking: url theo thứ tự dùng gần nhất (đầu = cũ nhất)
    private readonly LinkedList<string> lruList = new LinkedList<string>();
    private readonly Dictionary<string, LinkedListNode<string>> lruNodes = new Dictionary<string, LinkedListNode<string>>();

    // Đang download: url → list callback chờ
    private readonly Dictionary<string, List<Action<Texture2D>>> pending = new Dictionary<string, List<Action<Texture2D>>>();

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

    /// <summary>
    /// Lấy texture từ cache hoặc download. Callback nhận null nếu lỗi.
    /// </summary>
    public void GetTexture(string url, int timeoutSeconds, Action<Texture2D> callback)
    {
        if (string.IsNullOrEmpty(url))
        {
            callback?.Invoke(null);
            return;
        }

        // Cache hit
        if (cache.TryGetValue(url, out Texture2D cached))
        {
            TouchLRU(url);
            if (showDebug) Debug.Log($"[TextureCache] HIT: {url}");
            callback?.Invoke(cached);
            return;
        }

        // Đang download — chỉ queue callback, không download lại
        if (pending.ContainsKey(url))
        {
            if (showDebug) Debug.Log($"[TextureCache] PENDING queue: {url}");
            pending[url].Add(callback);
            return;
        }

        // Download mới
        if (showDebug) Debug.Log($"[TextureCache] MISS — downloading: {url}");
        pending[url] = new List<Action<Texture2D>> { callback };
        StartCoroutine(DownloadTexture(url, timeoutSeconds));
    }

    private IEnumerator DownloadTexture(string url, int timeoutSeconds)
    {
        using (UnityWebRequest req = UnityWebRequestTexture.GetTexture(url))
        {
            req.timeout = timeoutSeconds;
            yield return req.SendWebRequest();

            Texture2D texture = null;

            if (req.result == UnityWebRequest.Result.Success)
            {
                texture = DownloadHandlerTexture.GetContent(req);
                if (texture != null)
                {
                    AddToCache(url, texture);
                    if (showDebug) Debug.Log($"[TextureCache] Downloaded & cached: {url}");
                }
                else
                {
                    Debug.LogError($"[TextureCache] Texture null after download: {url}");
                }
            }
            else
            {
                Debug.LogError($"[TextureCache] Download failed: {url} — {req.error}");
            }

            // Flush tất cả callback đang chờ
            if (pending.TryGetValue(url, out List<Action<Texture2D>> callbacks))
            {
                pending.Remove(url);
                foreach (var cb in callbacks)
                    cb?.Invoke(texture);
            }
        }
    }

    private void AddToCache(string url, Texture2D texture)
    {
        if (cache.ContainsKey(url)) return;

        // Evict LRU nếu đầy
        while (cache.Count >= maxCacheSize && lruList.Count > 0)
        {
            string oldest = lruList.First.Value;
            lruList.RemoveFirst();
            lruNodes.Remove(oldest);
            cache.Remove(oldest);
            if (showDebug) Debug.Log($"[TextureCache] Evicted LRU: {oldest}");
        }

        cache[url] = texture;
        var node = lruList.AddLast(url);
        lruNodes[url] = node;
    }

    private void TouchLRU(string url)
    {
        if (!lruNodes.TryGetValue(url, out LinkedListNode<string> node)) return;
        lruList.Remove(node);
        lruNodes[url] = lruList.AddLast(url);
    }

    /// <summary>
    /// Xóa toàn bộ cache (gọi khi đổi gallery hoặc logout).
    /// </summary>
    public void ClearAll()
    {
        cache.Clear();
        lruList.Clear();
        lruNodes.Clear();
        if (showDebug) Debug.Log("[TextureCache] Cleared all");
    }

    /// <summary>
    /// Xóa một URL cụ thể khỏi cache.
    /// </summary>
    public void Invalidate(string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        cache.Remove(url);
        if (lruNodes.TryGetValue(url, out LinkedListNode<string> node))
        {
            lruList.Remove(node);
            lruNodes.Remove(url);
        }
    }

    public bool IsCached(string url) => !string.IsNullOrEmpty(url) && cache.ContainsKey(url);

    public int CacheCount => cache.Count;

#if UNITY_EDITOR
    [Header("Runtime Info (Read Only)")]
    [SerializeField] private int cachedCount;
    [SerializeField] private int pendingCount;

    private void Update()
    {
        cachedCount  = cache.Count;
        pendingCount = pending.Count;
    }
#endif
}
