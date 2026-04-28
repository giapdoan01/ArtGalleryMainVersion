using UnityEngine;
using System.Collections.Generic;

public class Model3DCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform targetModel;

    [Header("Camera Settings")]
    [SerializeField] private float distance      = 3f;
    [SerializeField] private float rotateSpeed   = 3f;
    [SerializeField] private float smoothSpeed   = 10f;
    [SerializeField] private float minDistance   = 0.5f;
    [SerializeField] private float maxDistance   = 10f;
    [SerializeField] private float zoomSpeed     = 2f;
    [SerializeField] private float minPitchAngle = -80f;
    [SerializeField] private float maxPitchAngle =  80f;

    [Header("Preview Layer Settings")]
    [SerializeField] private string previewLayerName = "PreviewModel3D";
    [SerializeField] private bool   autoSetupLayer   = true;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // ────────────────────────────────────────────────
    private float   currentYaw   = 0f;
    private float   currentPitch = 20f;
    private bool    isDragging   = false;
    private Vector2 lastMousePos;

    private Camera previewCamera;
    private int    previewLayer;

    private Dictionary<GameObject, int> originalLayers = new Dictionary<GameObject, int>();

    // ════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════

    private void Awake()
    {
        previewCamera = GetComponent<Camera>();

        if (previewCamera == null)
        {
            Debug.LogError("[Model3DCamera] No Camera component found!");
            return;
        }

        previewLayer = LayerMask.NameToLayer(previewLayerName);

        if (previewLayer == -1)
        {
            Debug.LogWarning($"[Model3DCamera] Layer '{previewLayerName}' not found! " +
                             "Please add it in Edit > Project Settings > Tags and Layers.");
            return;
        }

        previewCamera.cullingMask = (1 << previewLayer);
        previewCamera.clearFlags  = CameraClearFlags.Skybox;

        if (showDebug)
            Debug.Log($"[Model3DCamera] CullingMask = '{previewLayerName}' (index {previewLayer})");
    }

    private void OnEnable()
    {
        currentYaw   = 0f;
        currentPitch = 20f;
        SnapCameraPosition();
    }

    private void LateUpdate()
    {
        if (targetModel == null) return;

        HandleMouseInput();
        HandleTouchInput();
        HandleZoom();
        ApplySmoothCamera();
    }

    // ════════════════════════════════════════════════
    // INPUT
    // ════════════════════════════════════════════════

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isDragging   = true;
            lastMousePos = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(0))
            isDragging = false;

        if (isDragging)
        {
            Vector2 delta = (Vector2)Input.mousePosition - lastMousePos;
            lastMousePos  = Input.mousePosition;

            currentYaw   += delta.x * rotateSpeed * Time.deltaTime * 10f;
            currentPitch -= delta.y * rotateSpeed * Time.deltaTime * 10f;
            currentPitch  = Mathf.Clamp(currentPitch, minPitchAngle, maxPitchAngle);

            if (showDebug)
                Debug.Log($"[Model3DCamera] Yaw: {currentYaw:F1} | Pitch: {currentPitch:F1}");
        }
    }

    private void HandleTouchInput()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
                lastMousePos = touch.position;
            else if (touch.phase == TouchPhase.Moved)
            {
                Vector2 delta = touch.position - lastMousePos;
                lastMousePos  = touch.position;

                currentYaw   += delta.x * rotateSpeed * Time.deltaTime * 10f;
                currentPitch -= delta.y * rotateSpeed * Time.deltaTime * 10f;
                currentPitch  = Mathf.Clamp(currentPitch, minPitchAngle, maxPitchAngle);
            }
        }

        if (Input.touchCount == 2)
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            float prevDist = Vector2.Distance(t0.position - t0.deltaPosition,
                                              t1.position - t1.deltaPosition);
            float currDist = Vector2.Distance(t0.position, t1.position);

            distance = Mathf.Clamp(
                distance + (prevDist - currDist) * zoomSpeed * 0.01f,
                minDistance, maxDistance);
        }
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.001f)
        {
            distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);
        }
    }

    // ════════════════════════════════════════════════
    // CAMERA POSITION
    // ════════════════════════════════════════════════

    /// <summary>
    /// Lerp position → mượt khi xoay/zoom
    /// LookAt target → luôn focus, không bao giờ lệch
    /// </summary>
    private void ApplySmoothCamera()
    {
        if (targetModel == null) return;

        // Tính desired position từ góc hiện tại
        Quaternion rotation      = Quaternion.Euler(currentPitch, currentYaw, 0f);
        Vector3    offset        = rotation * new Vector3(0f, 0f, -distance);
        Vector3    desiredPos    = targetModel.position + offset;

        //  Lerp position → xoay/zoom mượt
        transform.position = Vector3.Lerp(transform.position, desiredPos, smoothSpeed * Time.deltaTime);

        //  LookAt thẳng → luôn focus vào target, không bao giờ lệch
        transform.LookAt(targetModel.position);
    }

    /// <summary>Snap không lerp — dùng khi mới set target / OnEnable</summary>
    private void SnapCameraPosition()
    {
        if (targetModel == null) return;

        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        Vector3    offset   = rotation * new Vector3(0f, 0f, -distance);

        transform.position = targetModel.position + offset;
        transform.LookAt(targetModel.position);
    }

    // ════════════════════════════════════════════════
    // LAYER HELPERS
    // ════════════════════════════════════════════════

    private void SaveAndSetLayerRecursive(Transform root, int newLayer)
    {
        if (root == null) return;

        originalLayers[root.gameObject] = root.gameObject.layer;
        root.gameObject.layer           = newLayer;

        foreach (Transform child in root)
            SaveAndSetLayerRecursive(child, newLayer);

        if (showDebug)
            Debug.Log($"[Model3DCamera] {root.name}: layer {originalLayers[root.gameObject]} → {newLayer}");
    }

    private void RestoreOriginalLayerRecursive(Transform root)
    {
        if (root == null) return;

        if (originalLayers.TryGetValue(root.gameObject, out int savedLayer))
        {
            root.gameObject.layer = savedLayer;

            if (showDebug)
                Debug.Log($"[Model3DCamera] {root.name}: restored layer → {savedLayer}");
        }

        foreach (Transform child in root)
            RestoreOriginalLayerRecursive(child);
    }

    // ════════════════════════════════════════════════
    // PUBLIC API
    // ════════════════════════════════════════════════

    public void SetTarget(Transform target)
    {
        if (targetModel != null && autoSetupLayer && previewLayer != -1)
        {
            RestoreOriginalLayerRecursive(targetModel);
            originalLayers.Clear();
        }

        targetModel  = target;
        currentYaw   = 0f;
        currentPitch = 20f;

        if (target != null)
        {
            if (autoSetupLayer && previewLayer != -1)
            {
                originalLayers.Clear();
                SaveAndSetLayerRecursive(target, previewLayer);
            }

            // Snap ngay khi set target mới — không lerp từ vị trí cũ
            SnapCameraPosition();
        }

        if (showDebug)
            Debug.Log($"[Model3DCamera] Target set: {target?.name ?? "null"}");
    }

    public void ClearTarget()
    {
        if (targetModel != null && autoSetupLayer && previewLayer != -1)
        {
            RestoreOriginalLayerRecursive(targetModel);
            originalLayers.Clear();
        }

        targetModel = null;

        if (showDebug)
            Debug.Log("[Model3DCamera] Target cleared, original layers restored");
    }

    public void ResetView()
    {
        currentYaw   = 0f;
        currentPitch = 20f;
        // Không snap → lerp mượt về vị trí reset
    }
}