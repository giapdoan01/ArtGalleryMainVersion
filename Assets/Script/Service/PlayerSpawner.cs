using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerSpawner : MonoBehaviour
{
    [Header("Avatar Prefabs")]
    [Tooltip("Kéo các avatar prefab vào đây theo đúng thứ tự index (0, 1, 2, ...)")]
    [SerializeField] private GameObject[] avatarPrefabs;

    [Header("Spawn Settings")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("Map Settings")]
    [SerializeField] private GameObject followMapCamera; 
    [Header("Update Settings")]
    [SerializeField] private float checkInterval = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private Dictionary<string, GameObject> players = new Dictionary<string, GameObject>();
    private int   nextSpawnIndex = 0;
    private float lastCheckTime  = 0f;

    private const string LOCAL_SINGLE_ID = "local-player-single-mode";

    // ═══════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════

    private void Start()
    {
        if (NetworkManager.Instance == null)
        {
            Debug.LogError("[PlayerSpawner] NetworkManager not found!");
            return;
        }

        if (avatarPrefabs == null || avatarPrefabs.Length == 0)
            Debug.LogError("[PlayerSpawner] avatarPrefabs is empty! Assign prefabs in Inspector.");

        if (showDebug) Debug.Log("[PlayerSpawner] Start()");

        NetworkManager.Instance.OnPlayerJoined += OnPlayerJoined;
        NetworkManager.Instance.OnPlayerLeft   += OnPlayerLeft;

        if (NetworkManager.Instance.IsConnected)
            StartCoroutine(ForceCheckPlayers());
        else
            NetworkManager.Instance.OnConnected += OnConnected;
    }

    private void Update()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.IsConnected)
        {
            if (Time.time - lastCheckTime > checkInterval)
            {
                CheckForPlayers();
                UpdatePlayerPositions();
                lastCheckTime = Time.time;
            }
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnPlayerJoined -= OnPlayerJoined;
            NetworkManager.Instance.OnPlayerLeft   -= OnPlayerLeft;
            NetworkManager.Instance.OnConnected    -= OnConnected;
        }
    }

    // ═══════════════════════════════════════════════
    // CONNECTION HANDLERS
    // ═══════════════════════════════════════════════

    private void OnConnected()
    {
        if (showDebug) Debug.Log("[PlayerSpawner] Connected to room");
        CheckForPlayers();
    }

    private IEnumerator ForceCheckPlayers()
    {
        yield return new WaitForEndOfFrame();

        var state = NetworkManager.Instance.State;
        if (state?.players == null) { Debug.LogWarning("[PlayerSpawner] State/players null"); yield break; }

        if (showDebug) Debug.Log($"[PlayerSpawner] Force check: {state.players.Count} players");

        state.players.ForEach((sessionId, player) =>
        {
            if (!players.ContainsKey(sessionId))
                SpawnPlayer(sessionId, player);
        });
    }

    // ═══════════════════════════════════════════════
    // CHECK & UPDATE
    // ═══════════════════════════════════════════════

    private void CheckForPlayers()
    {
        var state = NetworkManager.Instance.State;
        if (state == null) return;

        NetworkManager.Instance.CheckForNewPlayers();

        var toRemove = new List<string>();
        foreach (var entry in players)
        {
            if (entry.Key == LOCAL_SINGLE_ID) continue;

            bool stillExists = false;
            state.players.ForEach((sid, _) => { if (sid.Equals(entry.Key)) stillExists = true; });
            if (!stillExists) toRemove.Add(entry.Key);
        }

        foreach (var sid in toRemove) OnPlayerLeft(sid, null);
    }

    private void UpdatePlayerPositions()
    {
        var state = NetworkManager.Instance.State;
        if (state == null) return;

        state.players.ForEach((sessionId, player) =>
        {
            if (sessionId.Equals(NetworkManager.Instance.SessionId)) return;

            if (players.TryGetValue(sessionId, out GameObject playerObj))
            {
                PlayerController controller = playerObj.GetComponent<PlayerController>();
                controller?.UpdateNetworkPosition(player.x, player.y, player.z, player.rotationY);
            }
        });
    }

    // ═══════════════════════════════════════════════
    // PLAYER JOIN / LEAVE
    // ═══════════════════════════════════════════════

    private void OnPlayerJoined(string sessionId, Player player)
    {
        if (showDebug)
            Debug.Log($"[PlayerSpawner] OnPlayerJoined: {player.username} ({sessionId})" +
                      $" avatarIndex={(int)player.avatarIndex}");

        SpawnPlayer(sessionId, player);
    }

    private void OnPlayerLeft(string sessionId, Player player)
    {
        if (players.TryGetValue(sessionId, out GameObject playerObj))
        {
            if (showDebug) Debug.Log($"[PlayerSpawner] Removing player: {sessionId}");
            Destroy(playerObj);
            players.Remove(sessionId);
            NetworkManager.Instance.MarkPlayerRemoved(sessionId);
        }
    }

    // ═══════════════════════════════════════════════
    // SPAWN — MULTIPLAYER
    // ═══════════════════════════════════════════════

    private void SpawnPlayer(string sessionId, Player player)
    {
        if (players.ContainsKey(sessionId))
        {
            if (showDebug) Debug.LogWarning($"[PlayerSpawner] Duplicate spawn skipped: {sessionId}");
            return;
        }

        GameObject prefab = GetAvatarPrefab(player.avatarIndex);
        if (prefab == null)
        {
            Debug.LogError($"[PlayerSpawner] No prefab for avatarIndex={(int)player.avatarIndex}");
            return;
        }

        bool isLocal = sessionId.Equals(NetworkManager.Instance.SessionId);

        GameObject playerObj = Instantiate(prefab, GetSpawnPosition(), Quaternion.identity);
        playerObj.name = $"Player_{player.username}_{sessionId.Substring(0, 5)}" +
                         $"_{(isLocal ? "LOCAL" : "REMOTE")}";

        if (isLocal) playerObj.tag = "Player";

        if (showDebug)
            Debug.Log($"[PlayerSpawner] Spawned: {player.username}" +
                      $" | isLocal={isLocal}" +
                      $" | avatarIndex={(int)player.avatarIndex}" +
                      $" | prefab={prefab.name}");

        PlayerController controller = playerObj.GetComponent<PlayerController>();
        if (controller != null)
            controller.Initialize(sessionId, player, isLocal);
        else
            Debug.LogError("[PlayerSpawner] PlayerController not found on prefab!");

        players[sessionId] = playerObj;

        // ✅ Gán FollowMapCamera làm con của local player
        if (isLocal) AttachFollowMapCamera(playerObj);

        if (isLocal && PlayerTeleportManager.Instance != null)
        {
            CharacterController cc = playerObj.GetComponent<CharacterController>();
            PlayerTeleportManager.Instance.RegisterLocalPlayer(playerObj.transform, cc);
            if (showDebug) Debug.Log("[PlayerSpawner] Registered local player with TeleportManager");
        }
    }

    // ═══════════════════════════════════════════════
    // SPAWN — SINGLE PLAYER
    // ═══════════════════════════════════════════════

    public void SpawnLocalPlayerSingleMode(Player player)
    {
        if (showDebug)
            Debug.Log($"[PlayerSpawner] SpawnLocalPlayerSingleMode:" +
                      $" {player.username}, avatarIndex={(int)player.avatarIndex}");

        if (players.ContainsKey(LOCAL_SINGLE_ID))
        {
            Destroy(players[LOCAL_SINGLE_ID]);
            players.Remove(LOCAL_SINGLE_ID);
        }

        GameObject prefab = GetAvatarPrefab(player.avatarIndex);
        if (prefab == null)
        {
            Debug.LogError($"[PlayerSpawner] No prefab for avatarIndex={(int)player.avatarIndex}");
            return;
        }

        GameObject playerObj = Instantiate(prefab, GetSpawnPosition(), Quaternion.identity);
        playerObj.name = $"Player_{player.username}_SINGLE_LOCAL";
        playerObj.tag  = "Player";

        PlayerController controller = playerObj.GetComponent<PlayerController>();
        if (controller != null)
            controller.Initialize(LOCAL_SINGLE_ID, player, true);
        else
            Debug.LogError("[PlayerSpawner] PlayerController not found on prefab!");

        players[LOCAL_SINGLE_ID] = playerObj;

        // ✅ Gán FollowMapCamera làm con của local player (single mode)
        AttachFollowMapCamera(playerObj);

        if (PlayerTeleportManager.Instance != null)
        {
            CharacterController cc = playerObj.GetComponent<CharacterController>();
            PlayerTeleportManager.Instance.RegisterLocalPlayer(playerObj.transform, cc);
            if (showDebug) Debug.Log("[PlayerSpawner] Registered single player with TeleportManager");
        }

        if (showDebug) Debug.Log("[PlayerSpawner] Single player spawned successfully ✅");
    }

    // ═══════════════════════════════════════════════
    // FOLLOW MAP CAMERA
    // ═══════════════════════════════════════════════

    private void AttachFollowMapCamera(GameObject localPlayerObj)
    {
        if (followMapCamera == null)
        {
            if (showDebug) Debug.LogWarning("[PlayerSpawner] followMapCamera is not assigned!");
            return;
        }

        // ✅ Gán làm con của local player — camera sẽ di chuyển theo player
        followMapCamera.transform.SetParent(localPlayerObj.transform, false);

        if (showDebug) Debug.Log($"[PlayerSpawner] FollowMapCamera attached to {localPlayerObj.name} ✅");
    }

    // ═══════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════

    private GameObject GetAvatarPrefab(float avatarIndex)
    {
        if (avatarPrefabs == null || avatarPrefabs.Length == 0) return null;

        int index     = (int)avatarIndex;
        int safeIndex = Mathf.Clamp(index, 0, avatarPrefabs.Length - 1);

        if (safeIndex != index)
            Debug.LogWarning($"[PlayerSpawner] avatarIndex={index} out of range," +
                             $" fallback to {safeIndex}");

        return avatarPrefabs[safeIndex];
    }

    private Vector3 GetSpawnPosition()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Vector3 pos = spawnPoints[nextSpawnIndex].position;
            nextSpawnIndex = (nextSpawnIndex + 1) % spawnPoints.Length;
            return pos;
        }
        return Vector3.zero;
    }
}