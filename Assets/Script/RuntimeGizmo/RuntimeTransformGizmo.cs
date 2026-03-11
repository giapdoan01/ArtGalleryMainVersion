using UnityEngine;
using System;

public class RuntimeTransformGizmo : MonoBehaviour
{
    [Header("Gizmo Settings")]
    [SerializeField] private float gizmoSize = 2f;
    [SerializeField] private float lineWidth = 0.5f;
    [SerializeField] private float hoverRadius = 0.15f;
    [SerializeField] private bool scaleWithDistance = true;

    [Header("Colors")]
    [SerializeField] private Color xAxisColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private Color yAxisColor = new Color(0.2f, 1f, 0.2f, 1f);
    [SerializeField] private Color zAxisColor = new Color(0.2f, 0.2f, 1f, 1f);
    [SerializeField] private Color selectedColor = Color.yellow;
    [SerializeField] private Color hoverColor = Color.white;

    [Header("Interaction")]
    [SerializeField] private float dragSensitivity = 1f;
    [SerializeField] private float rotateSensitivity = 180f;
    [SerializeField] private bool snapEnabled = false;
    [SerializeField] private float moveSnapValue = 0.5f;
    [SerializeField] private float rotateSnapValue = 15f;

    [Header("References")]
    [SerializeField] private Camera renderCamera;

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;

    // State
    public TransformMode currentMode = TransformMode.Move;
    public TransformSpace currentSpace = TransformSpace.Local;
    private GizmoAxis hoveredAxis = GizmoAxis.None;
    private GizmoAxis selectedAxis = GizmoAxis.None;
    private bool isDragging = false;
    private bool isActive = false;

    // GL Material
    private Material glMaterial;

    // Drag state
    private Vector3 dragStartMousePos;
    private Vector3 dragStartPosition;
    private Vector3 dragStartRotation;
    private Plane dragPlane;

    // Camera control reference
    private CameraFollow cameraFollow;

    // Callbacks
    public event Action<Vector3, Vector3> OnTransformChanged;

    // Public property to check if gizmo is active
    public bool IsActive => isActive;

    #region Unity Lifecycle

    private void Awake()
    {
        if (renderCamera == null)
            renderCamera = Camera.main;

        // Tìm CameraFollow component
        if (renderCamera != null)
        {
            cameraFollow = renderCamera.GetComponent<CameraFollow>();
            if (cameraFollow == null && showDebug)
            {
                Debug.LogWarning("[RuntimeTransformGizmo] CameraFollow component not found on camera!");
            }
        }

        // Create GL material
        CreateGLMaterial();

        // Disable by default
        isActive = false;
        enabled = false;

        if (showDebug)
            Debug.Log($"[RuntimeTransformGizmo]  Awake on {gameObject.name}");
    }

    private void Update()
    {
        if (!isActive)
            return;

        // Kiểm tra camera
        if (renderCamera == null)
        {
            renderCamera = Camera.main;
            if (renderCamera == null)
            {
                Debug.LogError($"[RuntimeTransformGizmo] Cannot find Camera.main!");
                return;
            }
        }

        // Update hover (chỉ khi không drag)
        if (!isDragging)
        {
            UpdateHover();
        }

        // Handle input
        HandleInput();
    }

    //  GL RENDERING - Luôn render trên cùng
    private void OnRenderObject()
    {
        if (!isActive || glMaterial == null)
            return;

        glMaterial.SetPass(0);

        GL.PushMatrix();
        GL.MultMatrix(Matrix4x4.identity);
        GL.Begin(GL.LINES);

        float size = CalculateGizmoSize();
        Vector3 origin = transform.position;

        //  Move dùng Local, Rotate dùng World
        Quaternion rotation = (currentMode == TransformMode.Move)
            ? transform.rotation  // Local cho Move
            : Quaternion.identity; // World cho Rotate

        //  X axis (Red)
        Color xColor = GetAxisColor(GizmoAxis.X, xAxisColor);
        float xWidth = GetAxisWidth(GizmoAxis.X);
        DrawGLLine(origin, origin + rotation * Vector3.right * size, xColor, xWidth);

        //  Y axis (Green)
        Color yColor = GetAxisColor(GizmoAxis.Y, yAxisColor);
        float yWidth = GetAxisWidth(GizmoAxis.Y);
        DrawGLLine(origin, origin + rotation * Vector3.up * size, yColor, yWidth);

        //  Z axis (Blue)
        Color zColor = GetAxisColor(GizmoAxis.Z, zAxisColor);
        float zWidth = GetAxisWidth(GizmoAxis.Z);
        DrawGLLine(origin, origin + rotation * Vector3.forward * size, zColor, zWidth);

        GL.End();
        GL.PopMatrix();
    }

    private void OnDestroy()
    {
        if (glMaterial != null)
        {
            Destroy(glMaterial);
        }
    }

    #endregion

    #region GL Material Setup

    private void CreateGLMaterial()
    {
        if (glMaterial == null)
        {
            Shader shader = Shader.Find("Hidden/Internal-Colored");
            glMaterial = new Material(shader);
            glMaterial.hideFlags = HideFlags.HideAndDontSave;

            //  KEY: Luôn render, không check depth
            glMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            glMaterial.SetInt("_ZWrite", 0);
            glMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

            if (showDebug)
                Debug.Log("[RuntimeTransformGizmo]  GL Material created");
        }
    }

    private void DrawGLLine(Vector3 start, Vector3 end, Color color, float width)
    {
        //  Draw multiple lines để tạo độ dày
        int segments = Mathf.Max(1, Mathf.RoundToInt(width * 10f));

        for (int i = 0; i < segments; i++)
        {
            float offset = (i - segments / 2f) * 0.01f;
            Vector3 perpendicular = Vector3.Cross((end - start).normalized, renderCamera.transform.forward) * offset;

            GL.Color(color);
            GL.Vertex3(start.x + perpendicular.x, start.y + perpendicular.y, start.z + perpendicular.z);
            GL.Vertex3(end.x + perpendicular.x, end.y + perpendicular.y, end.z + perpendicular.z);
        }
    }

    private float GetAxisWidth(GizmoAxis axis)
    {
        if (selectedAxis == axis)
            return lineWidth * 4f;
        else if (hoveredAxis == axis)
            return lineWidth * 3f;
        else
            return lineWidth;
    }

    #endregion

    #region Public Methods

    public void Activate()
    {
        isActive = true;
        enabled = true;
        isDragging = false;
        selectedAxis = GizmoAxis.None;
        hoveredAxis = GizmoAxis.None;

        if (showDebug)
            Debug.Log($"[RuntimeTransformGizmo] ACTIVATED for: {gameObject.name}");
    }

    public void Deactivate()
    {
        isActive = false;
        enabled = false;
        isDragging = false;
        selectedAxis = GizmoAxis.None;
        hoveredAxis = GizmoAxis.None;

        if (showDebug)
            Debug.Log($"[RuntimeTransformGizmo] DEACTIVATED for: {gameObject.name}");
    }

    public void SetMode(TransformMode mode)
    {
        currentMode = mode;
        selectedAxis = GizmoAxis.None;
        hoveredAxis = GizmoAxis.None;

        Model3DTransformEditPopup model3DTransformEditPopup = FindObjectOfType<Model3DTransformEditPopup>();
        if (model3DTransformEditPopup != null)
        {
            model3DTransformEditPopup.onGizmoModeChanged?.Invoke(mode.ToString());
        }

        PaintingTransformEditPopup paintingTransformEditPopup = FindObjectOfType<PaintingTransformEditPopup>();
        if (paintingTransformEditPopup != null)
        {
            paintingTransformEditPopup.onGizmoModeChanged?.Invoke(mode.ToString());
        }

        if (showDebug)
            Debug.Log($"[RuntimeTransformGizmo] Mode: {mode} (Space: {(mode == TransformMode.Move ? "Local" : "World")})");
    }

    public void SetSpace(TransformSpace space)
    {
        currentSpace = space;

        if (showDebug)
            Debug.Log($"[RuntimeTransformGizmo] Space: {space} (Note: Move=Local, Rotate=World)");
    }

    public void SetSnapEnabled(bool enabled)
    {
        snapEnabled = enabled;
    }

    #endregion

    #region Input Handling

    private void HandleInput()
    {
        // Ignore if pointer over UI input field
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            var currentSelected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            if (currentSelected != null && currentSelected.GetComponent<TMPro.TMP_InputField>() != null)
            {
                return;
            }
        }

        // Start drag
        if (Input.GetMouseButtonDown(0))
        {
            if (hoveredAxis != GizmoAxis.None)
            {
                StartDrag();
            }
        }

        // Update drag
        if (isDragging)
        {
            UpdateDrag();
        }

        // End drag
        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            EndDrag();
        }

        // Keyboard shortcuts
        if (Input.GetKeyDown(KeyCode.Q))
            SetMode(TransformMode.Move);
        else if (Input.GetKeyDown(KeyCode.E))
            SetMode(TransformMode.Rotate);
    }

    private void StartDrag()
    {
        isDragging = true;
        selectedAxis = hoveredAxis;
        dragStartMousePos = Input.mousePosition;
        dragStartPosition = transform.position;
        dragStartRotation = transform.eulerAngles;

        Vector3 normal = GetDragPlaneNormal();
        dragPlane = new Plane(normal, transform.position);

        // Disable camera control khi bắt đầu drag
        if (cameraFollow != null)
        {
            cameraFollow.enabled = false;
            if (showDebug)
                Debug.Log("[RuntimeTransformGizmo] CameraFollow disabled");
        }

        if (showDebug)
        {
            Debug.Log($"[RuntimeTransformGizmo] ========== START DRAG ==========");
            Debug.Log($"[RuntimeTransformGizmo] Mode: {currentMode}");
            Debug.Log($"[RuntimeTransformGizmo] Selected axis: {selectedAxis}");
            Debug.Log($"[RuntimeTransformGizmo] Start position: {dragStartPosition}");
            Debug.Log($"[RuntimeTransformGizmo] =====================================");
        }
    }

    private void UpdateDrag()
    {
        switch (currentMode)
        {
            case TransformMode.Move:
                UpdateMoveDrag();
                break;
            case TransformMode.Rotate:
                UpdateRotateDrag();
                break;
        }
    }

    private void EndDrag()
    {
        if (showDebug)
        {
            Debug.Log($"[RuntimeTransformGizmo] ========== END DRAG ==========");
            Debug.Log($"[RuntimeTransformGizmo] Final position: {transform.position}");
            Debug.Log($"[RuntimeTransformGizmo] Final rotation: {transform.eulerAngles}");
            Debug.Log($"[RuntimeTransformGizmo] =====================================");
        }

        isDragging = false;
        selectedAxis = GizmoAxis.None;

        // Enable lại camera control khi kết thúc drag
        if (cameraFollow != null)
        {
            cameraFollow.enabled = true;
            if (showDebug)
                Debug.Log("[RuntimeTransformGizmo] CameraFollow enabled");
        }
    }

    #endregion

    #region Hover Detection

    private void UpdateHover()
    {
        // Ignore if dragging
        if (isDragging)
            return;

        // Ignore if pointer over UI input field
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
            var currentSelected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;
            if (currentSelected != null && currentSelected.GetComponent<TMPro.TMP_InputField>() != null)
            {
                hoveredAxis = GizmoAxis.None;
                return;
            }
        }

        if (renderCamera == null)
        {
            hoveredAxis = GizmoAxis.None;
            return;
        }

        Ray ray = renderCamera.ScreenPointToRay(Input.mousePosition);
        float size = CalculateGizmoSize();

        Vector3 origin = transform.position;

        //  Move dùng Local, Rotate dùng World
        Quaternion rotation = (currentMode == TransformMode.Move)
            ? transform.rotation  // Local cho Move
            : Quaternion.identity; // World cho Rotate

        // Sử dụng screen-space distance
        float screenHoverRadius = hoverRadius * 100f; // pixels

        // Tìm axis gần nhất trên màn hình
        GizmoAxis closestAxis = GizmoAxis.None;
        float closestScreenDistance = float.MaxValue;

        // Check X axis
        float screenDistX = GetScreenDistanceToAxis(ray, origin, rotation * Vector3.right, size);
        if (screenDistX < screenHoverRadius && screenDistX < closestScreenDistance)
        {
            closestScreenDistance = screenDistX;
            closestAxis = GizmoAxis.X;
        }

        // Check Y axis
        float screenDistY = GetScreenDistanceToAxis(ray, origin, rotation * Vector3.up, size);
        if (screenDistY < screenHoverRadius && screenDistY < closestScreenDistance)
        {
            closestScreenDistance = screenDistY;
            closestAxis = GizmoAxis.Y;
        }

        // Check Z axis
        float screenDistZ = GetScreenDistanceToAxis(ray, origin, rotation * Vector3.forward, size);
        if (screenDistZ < screenHoverRadius && screenDistZ < closestScreenDistance)
        {
            closestScreenDistance = screenDistZ;
            closestAxis = GizmoAxis.Z;
        }

        // Update hovered axis
        hoveredAxis = closestAxis;
    }

    private float GetScreenDistanceToAxis(Ray ray, Vector3 origin, Vector3 direction, float length)
    {
        Vector3 dir = direction.normalized;

        // Sample nhiều điểm trên axis
        int samples = 20;
        float minScreenDistance = float.MaxValue;

        for (int i = 0; i <= samples; i++)
        {
            float t = (float)i / samples;
            Vector3 pointOnAxis = origin + dir * (t * length);

            // Convert to screen space
            Vector3 screenPoint = renderCamera.WorldToScreenPoint(pointOnAxis);

            // Check if behind camera
            if (screenPoint.z < 0)
                continue;

            // Calculate screen distance
            Vector2 screenPos = new Vector2(screenPoint.x, screenPoint.y);
            Vector2 mousePos = Input.mousePosition;
            float screenDist = Vector2.Distance(screenPos, mousePos);

            if (screenDist < minScreenDistance)
            {
                minScreenDistance = screenDist;
            }
        }

        return minScreenDistance;
    }

    #endregion

    #region Move Drag

    private void UpdateMoveDrag()
    {
        if (renderCamera == null)
        {
            Debug.LogError($"[RuntimeTransformGizmo] renderCamera is null!");
            return;
        }

        Ray ray = renderCamera.ScreenPointToRay(Input.mousePosition);

        if (!dragPlane.Raycast(ray, out float enter))
        {
            return;
        }

        Vector3 hitPoint = ray.GetPoint(enter);
        Vector3 dragStartHitPoint = GetDragStartHitPoint();
        Vector3 delta = hitPoint - dragStartHitPoint;

        //  Move luôn dùng Local rotation
        Quaternion rotation = transform.rotation;
        Vector3 constrainedDelta = ConstrainToAxis(delta, rotation);

        if (snapEnabled)
        {
            constrainedDelta = SnapVector(constrainedDelta, moveSnapValue);
        }

        Vector3 newPosition = dragStartPosition + constrainedDelta;

        // Apply position to THIS transform
        transform.position = newPosition;

        // Trigger event
        OnTransformChanged?.Invoke(newPosition, transform.eulerAngles);
    }

    private Vector3 GetDragStartHitPoint()
    {
        Ray ray = renderCamera.ScreenPointToRay(dragStartMousePos);
        dragPlane.Raycast(ray, out float enter);
        return ray.GetPoint(enter);
    }

    private Vector3 ConstrainToAxis(Vector3 delta, Quaternion rotation)
    {
        switch (selectedAxis)
        {
            case GizmoAxis.X:
                Vector3 xAxis = rotation * Vector3.right;
                return xAxis * Vector3.Dot(delta, xAxis);
            case GizmoAxis.Y:
                Vector3 yAxis = rotation * Vector3.up;
                return yAxis * Vector3.Dot(delta, yAxis);
            case GizmoAxis.Z:
                Vector3 zAxis = rotation * Vector3.forward;
                return zAxis * Vector3.Dot(delta, zAxis);
            default:
                return delta;
        }
    }

    private Vector3 SnapVector(Vector3 vector, float snapValue)
    {
        return new Vector3(
            Mathf.Round(vector.x / snapValue) * snapValue,
            Mathf.Round(vector.y / snapValue) * snapValue,
            Mathf.Round(vector.z / snapValue) * snapValue
        );
    }

    #endregion

    #region Rotate Drag

    private void UpdateRotateDrag()
    {
        Vector2 mouseDelta = (Vector2)Input.mousePosition - (Vector2)dragStartMousePos;
        float angle = (mouseDelta.x / Screen.width) * rotateSensitivity;

        if (snapEnabled)
        {
            angle = Mathf.Round(angle / rotateSnapValue) * rotateSnapValue;
        }

        Vector3 rotationAxis = GetRotationAxis();
        Quaternion rotation = Quaternion.AngleAxis(angle, rotationAxis);

        //  Rotate luôn dùng World space
        transform.rotation = rotation * Quaternion.Euler(dragStartRotation);

        OnTransformChanged?.Invoke(transform.position, transform.eulerAngles);
    }

    private Vector3 GetRotationAxis()
    {
        //  Rotate luôn dùng World axes
        switch (selectedAxis)
        {
            case GizmoAxis.X:
                return Vector3.right; // World X
            case GizmoAxis.Y:
                return Vector3.up; // World Y
            case GizmoAxis.Z:
                return Vector3.forward; // World Z
            default:
                return Vector3.up;
        }
    }

    #endregion

    #region Utilities

    private Color GetAxisColor(GizmoAxis axis, Color baseColor)
    {
        if (selectedAxis == axis)
            return selectedColor;

        if (hoveredAxis == axis)
            return hoverColor;

        return baseColor;
    }

    private float CalculateGizmoSize()
    {
        if (!scaleWithDistance || renderCamera == null)
            return gizmoSize;

        float distance = Vector3.Distance(renderCamera.transform.position, transform.position);
        return gizmoSize * (distance / 10f);
    }

    private Vector3 GetDragPlaneNormal()
    {
        if (renderCamera == null)
            return Vector3.up;

        //  Move dùng Local, Rotate dùng World
        Quaternion rotation = (currentMode == TransformMode.Move)
            ? transform.rotation  // Local cho Move
            : Quaternion.identity; // World cho Rotate

        Vector3 cameraForward = renderCamera.transform.forward;

        switch (selectedAxis)
        {
            case GizmoAxis.X:
                Vector3 xAxis = rotation * Vector3.right;
                return Vector3.Cross(xAxis, cameraForward).normalized;
            case GizmoAxis.Y:
                Vector3 yAxis = rotation * Vector3.up;
                return Vector3.Cross(yAxis, cameraForward).normalized;
            case GizmoAxis.Z:
                Vector3 zAxis = rotation * Vector3.forward;
                return Vector3.Cross(zAxis, cameraForward).normalized;
            default:
                return cameraForward;
        }
    }

    #endregion
}

public enum TransformMode { Move, Rotate, Scale }
public enum TransformSpace { Local, World }
public enum GizmoAxis { None, X, Y, Z }
