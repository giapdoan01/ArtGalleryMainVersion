using UnityEngine;
using System.Collections;

public class PlayerTeleportManager : MonoBehaviour
{
    public static PlayerTeleportManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private CameraFollow cameraFollow;

    [Header("Teleport Settings")]
    [SerializeField] private float teleportDuration = 0.5f;
    [SerializeField] private AnimationCurve teleportCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool useFade = true;
    [SerializeField] private float fadeDuration = 0.3f;

    [Header("Camera Settings")]
    [SerializeField] private float cameraRotationSpeed = 5f;
    [SerializeField] private bool lockCameraAfterTeleport = false;
    [SerializeField] private float lockDuration = 1f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    private bool isTeleporting = false;
    private Coroutine currentTeleportCoroutine;
    private bool hasInitialized = false; //  THÊM

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); //  THÊM (optional)
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (cameraFollow == null)
        {
            cameraFollow = Camera.main?.GetComponent<CameraFollow>();
        }

        if (showDebug)
            Debug.Log("[PlayerTeleportManager] Initialized (waiting for player spawn)");
    }

    private bool EnsurePlayerReferences()
    {
        // Nếu đã có đầy đủ references, return true
        if (playerTransform != null && characterController != null)
        {
            hasInitialized = true;
            return true;
        }

        // Tìm player bằng tag
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        
        if (player != null)
        {
            playerTransform = player.transform;
            characterController = player.GetComponent<CharacterController>();

            if (characterController == null)
            {
                Debug.LogWarning("[PlayerTeleportManager] Player found but CharacterController missing!");
            }

            // Tìm camera nếu chưa có
            if (cameraFollow == null)
            {
                cameraFollow = Camera.main?.GetComponent<CameraFollow>();
            }

            hasInitialized = true;

            if (showDebug)
                Debug.Log($"[PlayerTeleportManager] Player references found: {player.name}");

            return true;
        }

        //  FALLBACK: Tìm bằng tên (nếu có pattern cụ thể)
        if (NetworkManager.Instance != null && !string.IsNullOrEmpty(NetworkManager.Instance.SessionId))
        {
            string sessionId = NetworkManager.Instance.SessionId;
            
            // Tìm player có tên chứa "LOCAL"
            GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
            foreach (GameObject p in allPlayers)
            {
                if (p.name.Contains("LOCAL"))
                {
                    playerTransform = p.transform;
                    characterController = p.GetComponent<CharacterController>();
                    hasInitialized = true;

                    if (showDebug)
                        Debug.Log($"[PlayerTeleportManager] Local player found: {p.name}");

                    return true;
                }
            }
        }

        if (showDebug)
            Debug.LogWarning("[PlayerTeleportManager] Player not found yet!");

        return false;
    }

    //  THÊM: Public method để PlayerSpawner gọi sau khi spawn
    public void RegisterLocalPlayer(Transform player, CharacterController controller)
    {
        playerTransform = player;
        characterController = controller;
        hasInitialized = true;

        if (showDebug)
            Debug.Log($"[PlayerTeleportManager] Local player registered: {player.name}");
    }

    /// <summary>
    /// Teleport player và camera đến TeleportPoint
    /// </summary>
    public void TeleportToPoint(Transform teleportPoint)
    {
        if (teleportPoint == null)
        {
            Debug.LogError("[PlayerTeleportManager] TeleportPoint is null!");
            return;
        }

        //  Tìm player nếu chưa có
        if (!EnsurePlayerReferences())
        {
            Debug.LogError("[PlayerTeleportManager] Player not found! Cannot teleport.");
            return;
        }

        if (isTeleporting)
        {
            if (showDebug)
                Debug.LogWarning("[PlayerTeleportManager] Already teleporting!");
            return;
        }

        if (currentTeleportCoroutine != null)
        {
            StopCoroutine(currentTeleportCoroutine);
        }

        currentTeleportCoroutine = StartCoroutine(TeleportCoroutine(teleportPoint));
    }

    /// <summary>
    /// Teleport ngay lập tức (không animation)
    /// </summary>
    public void TeleportInstant(Transform teleportPoint)
    {
        if (teleportPoint == null)
        {
            Debug.LogError("[PlayerTeleportManager] TeleportPoint is null!");
            return;
        }

        //  Tìm player nếu chưa có
        if (!EnsurePlayerReferences())
        {
            Debug.LogError("[PlayerTeleportManager] Player not found! Cannot teleport.");
            return;
        }

        // Disable CharacterController để set position
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        // Set player position
        playerTransform.position = teleportPoint.position;
        playerTransform.rotation = teleportPoint.rotation;

        // Re-enable CharacterController
        if (characterController != null)
        {
            characterController.enabled = true;
        }

        // Set camera rotation
        if (cameraFollow != null)
        {
            SetCameraRotation(teleportPoint.eulerAngles.y);
        }

        if (showDebug)
            Debug.Log($"[PlayerTeleportManager] Instant teleport to: {teleportPoint.position}");
    }

    private IEnumerator TeleportCoroutine(Transform teleportPoint)
    {
        isTeleporting = true;

        Vector3 startPosition = playerTransform.position;
        Quaternion startRotation = playerTransform.rotation;
        Vector3 targetPosition = teleportPoint.position;
        Quaternion targetRotation = teleportPoint.rotation;

        // Fade out (optional)
        if (useFade)
        {
            yield return StartCoroutine(FadeOut());
        }

        // Disable CharacterController
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        // Teleport animation
        float elapsed = 0f;

        while (elapsed < teleportDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / teleportDuration);
            float curveT = teleportCurve.Evaluate(t);

            // Lerp position
            playerTransform.position = Vector3.Lerp(startPosition, targetPosition, curveT);

            // Lerp rotation
            playerTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, curveT);

            // Update camera rotation
            if (cameraFollow != null)
            {
                float targetYaw = Quaternion.Slerp(startRotation, targetRotation, curveT).eulerAngles.y;
                SetCameraRotation(targetYaw);
            }

            yield return null;
        }

        // Ensure final position
        playerTransform.position = targetPosition;
        playerTransform.rotation = targetRotation;

        // Re-enable CharacterController
        if (characterController != null)
        {
            characterController.enabled = true;
        }

        // Set final camera rotation
        if (cameraFollow != null)
        {
            SetCameraRotation(targetRotation.eulerAngles.y);
        }

        // Fade in (optional)
        if (useFade)
        {
            yield return StartCoroutine(FadeIn());
        }

        // Lock camera (optional)
        if (lockCameraAfterTeleport)
        {
            yield return new WaitForSeconds(lockDuration);
        }

        isTeleporting = false;

        if (showDebug)
            Debug.Log($"[PlayerTeleportManager] Teleport complete: {targetPosition}");
    }

    private void SetCameraRotation(float yaw)
    {
        if (cameraFollow == null)
            return;

        //  Dùng public method (nếu đã thêm vào CameraFollow)
        cameraFollow.SetCameraYaw(yaw);

        if (showDebug)
            Debug.Log($"[PlayerTeleportManager] Camera yaw set to: {yaw}");
    }

    private IEnumerator FadeOut()
    {
        // TODO: Implement fade effect
        yield return new WaitForSeconds(fadeDuration);
    }

    private IEnumerator FadeIn()
    {
        // TODO: Implement fade effect
        yield return new WaitForSeconds(fadeDuration);
    }

    /// <summary>
    /// Set player và camera references (manual)
    /// </summary>
    public void SetReferences(Transform player, CharacterController controller, CameraFollow camera)
    {
        playerTransform = player;
        characterController = controller;
        cameraFollow = camera;
        hasInitialized = true;

        if (showDebug)
            Debug.Log("[PlayerTeleportManager] References set manually");
    }

    /// <summary>
    /// Check if currently teleporting
    /// </summary>
    public bool IsTeleporting()
    {
        return isTeleporting;
    }

    /// <summary>
    /// Check if player references are ready
    /// </summary>
    public bool IsReady()
    {
        return EnsurePlayerReferences();
    }
}
