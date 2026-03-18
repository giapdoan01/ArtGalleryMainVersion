using UnityEngine;
using Colyseus;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

public class NetworkManager : MonoBehaviour
{
    [Header("Server Settings")]
    [SerializeField] private string serverUrl = "wss://gallery-server.onrender.com";
    [SerializeField] private string roomName = "gallery";

    [Header("Sync Settings")]
    [SerializeField] private float networkUpdateRate = 0.1f;
    [SerializeField] private float positionLerpSpeed = 10f;
    [SerializeField] private float rotationLerpSpeed = 15f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // Singleton
    public static NetworkManager Instance { get; private set; }

    // Colyseus
    private ColyseusClient client;
    private ColyseusRoom<GalleryState> room;

    // Player Data
    public string PlayerName  { get; set; }
    public int    AvatarIndex { get; set; }  

    // Events
    public event Action                    OnConnected;
    public event Action                    OnDisconnected;
    public event Action<string>            OnError;
    public event Action<string, Player>    OnPlayerJoined;
    public event Action<string, Player>    OnPlayerLeft;
    public event Action<string, Vector3, float> OnPlayerPositionUpdated;
    public event Action<string, string>    OnPlayerAnimationUpdated;

    // Properties
    public bool         IsConnected => room != null;
    public string       SessionId   => room?.SessionId;
    public GalleryState State       => room?.State;

    // Track spawned players
    private HashSet<string>             spawnedPlayers  = new HashSet<string>();
    private Dictionary<string, Player>  previousPlayers = new Dictionary<string, Player>();

    // Interpolation
    private Dictionary<string, Vector3> targetPositions = new Dictionary<string, Vector3>();
    private Dictionary<string, float>   targetRotations = new Dictionary<string, float>();
    private Dictionary<string, long>    lastUpdateTime  = new Dictionary<string, long>();

    // ─────────────────────────────────────────────
    // LIFECYCLE
    // ─────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─────────────────────────────────────────────
    // CONNECT — overload cũ giữ lại để không break code khác
    // ─────────────────────────────────────────────

    /// <summary>Kết nối với avatarIndex đã set vào AvatarIndex property trước đó</summary>
    public void ConnectAndJoinRoom(string playerName, Action<bool, string> callback)
    {
        PlayerName = playerName;
        StartCoroutine(ConnectCoroutine(callback));
    }

    /// <summary>Kết nối và truyền avatarIndex trực tiếp</summary>
    public void ConnectAndJoinRoom(string playerName, int avatarIndex, Action<bool, string> callback)
    {
        PlayerName  = playerName;
        AvatarIndex = avatarIndex;

        PlayerPrefs.SetInt("AvatarIndex", avatarIndex);
        PlayerPrefs.Save();

        StartCoroutine(ConnectCoroutine(callback));
    }

    private IEnumerator ConnectCoroutine(Action<bool, string> callback)
    {
        client = new ColyseusClient(serverUrl);

        // ✅ Đọc avatarIndex từ property (đã set trước khi gọi Connect)
        int avatarIndex = AvatarIndex;
        if (avatarIndex <= 0)
            avatarIndex = PlayerPrefs.GetInt("AvatarIndex", 0);

        if (showDebug) Debug.Log($"[NetworkManager] Connecting as '{PlayerName}', avatarIndex={avatarIndex}");

        var options = new Dictionary<string, object>
        {
            { "username",    PlayerName  },
            { "avatarIndex", avatarIndex }   // ✅ Gửi index thay vì URL
        };

        Task<ColyseusRoom<GalleryState>> connectTask =
            client.JoinOrCreate<GalleryState>(roomName, options);

        while (!connectTask.IsCompleted) yield return null;

        if (connectTask.IsFaulted)
        {
            string errorMsg = connectTask.Exception?.Message ?? "Unknown error";
            Debug.LogError($"[NetworkManager] Connection failed: {errorMsg}");
            OnError?.Invoke(errorMsg);
            callback?.Invoke(false, errorMsg);
            yield break;
        }

        room = connectTask.Result;
        if (showDebug) Debug.Log($"[NetworkManager] Connected! SessionId: {room.SessionId}");

        SetupRoomListeners();
        OnConnected?.Invoke();
        callback?.Invoke(true, "Connected successfully");
    }

    // ─────────────────────────────────────────────
    // ROOM LISTENERS
    // ─────────────────────────────────────────────

    private void SetupRoomListeners()
    {
        room.OnLeave += (code) =>
        {
            if (showDebug) Debug.Log($"[NetworkManager] Left room: {code}");
            OnDisconnected?.Invoke();
        };

        room.OnError += (code, message) =>
        {
            Debug.LogError($"[NetworkManager] Room error {code}: {message}");
            OnError?.Invoke(message);
        };

        room.OnStateChange += (state, isFirstState) =>
        {
            if (state.players == null) return;

            state.players.ForEach((sessionId, player) =>
            {
                // ✅ Bỏ qua local player — không override vị trí do chính mình điều khiển
                if (sessionId.Equals(room.SessionId)) return;

                Vector3 position = new Vector3(player.x, player.y, player.z);
                targetPositions[sessionId] = position;
                targetRotations[sessionId] = player.rotationY;
                lastUpdateTime[sessionId]  = DateTimeOffset.Now.ToUnixTimeMilliseconds();

                OnPlayerPositionUpdated?.Invoke(sessionId, position, player.rotationY);
                OnPlayerAnimationUpdated?.Invoke(sessionId, player.animationState);
            });
        };

        StartCoroutine(MonitorPlayerChanges());
    }

    private IEnumerator MonitorPlayerChanges()
    {
        yield return new WaitForEndOfFrame();

        while (room != null && room.State != null)
        {
            if (room.State.players == null)
            {
                yield return new WaitForSeconds(networkUpdateRate);
                continue;
            }

            // ── Detect new players ──
            room.State.players.ForEach((sessionId, player) =>
            {
                if (!previousPlayers.ContainsKey(sessionId))
                {
                    if (showDebug)
                    {
                        Debug.Log($"[NetworkManager] New player: {player.username} ({sessionId})" +
                                  $" avatarIndex={player.avatarIndex}");
                    }

                    previousPlayers[sessionId] = player;

                    Vector3 position = new Vector3(player.x, player.y, player.z);
                    targetPositions[sessionId] = position;
                    targetRotations[sessionId] = player.rotationY;
                    lastUpdateTime[sessionId]  = DateTimeOffset.Now.ToUnixTimeMilliseconds();

                    if (!spawnedPlayers.Contains(sessionId))
                    {
                        spawnedPlayers.Add(sessionId);
                        OnPlayerJoined?.Invoke(sessionId, player);
                    }
                }
                else
                {
                    // ✅ Chỉ update remote players
                    if (!sessionId.Equals(room.SessionId))
                    {
                        Vector3 position = new Vector3(player.x, player.y, player.z);
                        targetPositions[sessionId] = position;
                        targetRotations[sessionId] = player.rotationY;
                        lastUpdateTime[sessionId]  = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    }
                }
            });

            // ── Detect removed players ──
            var toRemove = new List<string>();
            foreach (var entry in previousPlayers)
            {
                bool stillExists = false;
                room.State.players.ForEach((sid, _) => { if (sid.Equals(entry.Key)) stillExists = true; });
                if (!stillExists) toRemove.Add(entry.Key);
            }

            foreach (var sessionId in toRemove)
            {
                if (showDebug) Debug.Log($"[NetworkManager] Player removed: {sessionId}");
                Player removed = previousPlayers[sessionId];
                previousPlayers.Remove(sessionId);
                spawnedPlayers.Remove(sessionId);
                targetPositions.Remove(sessionId);
                targetRotations.Remove(sessionId);
                lastUpdateTime.Remove(sessionId);
                OnPlayerLeft?.Invoke(sessionId, removed);
            }

            yield return new WaitForSeconds(networkUpdateRate);
        }
    }

    // ─────────────────────────────────────────────
    // INTERPOLATION HELPERS
    // ─────────────────────────────────────────────

    public Vector3 GetInterpolatedPosition(string sessionId, Vector3 current)
    {
        if (!targetPositions.ContainsKey(sessionId)) return current;
        return Vector3.Lerp(current, targetPositions[sessionId], Time.deltaTime * positionLerpSpeed);
    }

    public float GetInterpolatedRotation(string sessionId, float current)
    {
        if (!targetRotations.ContainsKey(sessionId)) return current;
        return Mathf.LerpAngle(current, targetRotations[sessionId], Time.deltaTime * rotationLerpSpeed);
    }

    // ─────────────────────────────────────────────
    // CHECK / MARK PLAYERS
    // ─────────────────────────────────────────────

    public void CheckForNewPlayers()
    {
        if (room?.State?.players == null) return;

        room.State.players.ForEach((sessionId, player) =>
        {
            if (!spawnedPlayers.Contains(sessionId))
            {
                if (showDebug) Debug.Log($"[NetworkManager] CheckForNewPlayers: {player.username} ({sessionId})");
                spawnedPlayers.Add(sessionId);
                previousPlayers[sessionId] = player;

                Vector3 position = new Vector3(player.x, player.y, player.z);
                targetPositions[sessionId] = position;
                targetRotations[sessionId] = player.rotationY;
                lastUpdateTime[sessionId]  = DateTimeOffset.Now.ToUnixTimeMilliseconds();

                OnPlayerJoined?.Invoke(sessionId, player);
            }
        });
    }

    public void MarkPlayerRemoved(string sessionId)
    {
        spawnedPlayers.Remove(sessionId);
        previousPlayers.Remove(sessionId);
        targetPositions.Remove(sessionId);
        targetRotations.Remove(sessionId);
        lastUpdateTime.Remove(sessionId);
        if (showDebug) Debug.Log($"[NetworkManager] Player marked removed: {sessionId}");
    }

    // ─────────────────────────────────────────────
    // SEND MESSAGES
    // ─────────────────────────────────────────────

    public void SendPosition(Vector3 position, float rotationY) =>
        SendMove(position.x, position.y, position.z, rotationY);

    public void SendPosition(float x, float y, float z, float rotationY) =>
        SendMove(x, y, z, rotationY);

    public void SendMove(float x, float y, float z, float rotationY)
    {
        if (room == null) return;
        room.Send("move", new Dictionary<string, object>
        {
            { "x", x }, { "y", y }, { "z", z }, { "rotationY", rotationY }
        });
    }

    public void SendAnimation(string animationState)
    {
        if (room == null) return;
        room.Send("animation", new Dictionary<string, object>
        {
            { "state", animationState }
        });
    }

    public void SendChat(string message)
    {
        if (room == null) return;
        room.Send("chat", new Dictionary<string, object> { { "message", message } });
    }

    public async Task SendChatMessage(string message)
    {
        if (room == null) { Debug.LogError("[NetworkManager] Room is null"); return; }
        try
        {
            await room.Send("chat", new Dictionary<string, object> { { "message", message } });
            if (showDebug) Debug.Log($"[NetworkManager] Chat sent: {message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[NetworkManager] Chat error: {ex.Message}");
            throw;
        }
    }

    // ─────────────────────────────────────────────
    // DISCONNECT
    // ─────────────────────────────────────────────

    public void Disconnect()
    {
        if (room != null) { room.Leave(); room = null; }
    }

    private void OnDestroy()        => Disconnect();
    private void OnApplicationQuit() => Disconnect();
}