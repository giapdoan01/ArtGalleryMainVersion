using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerView : MonoBehaviour
{
    [SerializeField] private Canvas          nameTagCanvas;
    [SerializeField] private TextMeshProUGUI nameTagText;

    [Header("Map Icon")]
    [SerializeField] private Canvas mapIconCanvas;      // ✅ Canvas của MapIcon
    [SerializeField] private Image  mapIconImage;
    [SerializeField] private Sprite iconLocalPlayer;
    [SerializeField] private Sprite iconRemotePlayer;

    private Animator   animator;
    private Transform  cameraTransform;
    private bool       isLocalPlayer;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");

    // ═══════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();

        if (animator == null)
            Debug.LogWarning("[PlayerView] Animator not found on prefab children!");
    }

    private void Update()
    {
        UpdateNameTagRotation();
        UpdateMapIconRotation(); // ✅ Giữ rotation cố định mỗi frame
    }

    // ═══════════════════════════════════════════════
    // INITIALIZE
    // ═══════════════════════════════════════════════

    public void Initialize(string username, bool isLocal)
    {
        isLocalPlayer = isLocal;

        // Name tag
        if (nameTagText   != null) nameTagText.text = username;
        if (nameTagCanvas != null) nameTagCanvas.gameObject.SetActive(!isLocal);

        // Map icon: gán sprite theo loại player
        if (mapIconImage != null)
        {
            mapIconImage.sprite  = isLocal ? iconLocalPlayer : iconRemotePlayer;
            mapIconImage.enabled = mapIconImage.sprite != null;
        }

        // Camera follow cho local player
        if (isLocal)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                cameraTransform = mainCamera.transform;

                CameraFollow cameraFollow = mainCamera.GetComponent<CameraFollow>();
                if (cameraFollow == null)
                    cameraFollow = mainCamera.gameObject.AddComponent<CameraFollow>();

                cameraFollow.SetTarget(transform);
            }
            else
            {
                Debug.LogWarning("[PlayerView] Camera.main not found!");
            }
        }

        if (animator != null)
            animator.SetFloat(SpeedHash, 0f);
    }

    // ═══════════════════════════════════════════════
    // ANIMATION
    // ═══════════════════════════════════════════════

    public void UpdateAnimationSpeed(float speed)
    {
        if (animator != null)
            animator.SetFloat(SpeedHash, speed);
    }

    public void SetAnimator(Animator newAnimator)
    {
        if (newAnimator == null) return;
        animator = newAnimator;
        animator.SetFloat(SpeedHash, 0f);
        if (showDebug) Debug.Log("[PlayerView] Animator updated");
    }

    [SerializeField] private bool showDebug = false;

    // ═══════════════════════════════════════════════
    // TRANSFORM
    // ═══════════════════════════════════════════════

    public void SetPosition(Vector3 position)         => transform.position = position;
    public void SetRotation(float rotationY)          => transform.rotation = Quaternion.Euler(0, rotationY, 0);

    public void LerpToPosition(Vector3 target, float speed)
        => transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * speed);

    public void LerpToRotation(Quaternion target, float speed)
        => transform.rotation = Quaternion.Lerp(transform.rotation, target, Time.deltaTime * speed);

    // ═══════════════════════════════════════════════
    // NAME TAG
    // ═══════════════════════════════════════════════

    private void UpdateNameTagRotation()
    {
        if (nameTagCanvas == null || isLocalPlayer) return;

        if (cameraTransform == null)
            cameraTransform = Camera.main?.transform;

        if (cameraTransform == null) return;

        nameTagCanvas.transform.LookAt(cameraTransform);
        nameTagCanvas.transform.Rotate(0, 180, 0);
    }

    // ═══════════════════════════════════════════════
    // MAP ICON — Luôn cố định rotation (0, 180, 0) world space
    // ═══════════════════════════════════════════════

    private static readonly Quaternion MapIconFixedRotation = Quaternion.Euler(0f, 180f, 0f);

    private void UpdateMapIconRotation()
    {
        if (mapIconCanvas == null) return;

        // ✅ Set thẳng vào rotation world space — bất kể player xoay thế nào
        mapIconCanvas.transform.rotation = MapIconFixedRotation;
    }
} 