using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Model3DController : MonoBehaviour
{
    #region Singleton
    private static Model3DController instance;
    public static Model3DController Instance
    {
        get
        {
            if (instance == null)
                instance = FindObjectOfType<Model3DController>();
            return instance;
        }
    }
    #endregion

    [Header("References")]
    [SerializeField] private Model3DInfo model3DInfo;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    // ════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else if (instance != this)
        {
            Destroy(this);
            return;
        }
    }

    private void Start()
    {
        if (model3DInfo == null)
        {
            model3DInfo = FindObjectOfType<Model3DInfo>();

            if (model3DInfo == null)
                model3DInfo = FindObjectOfType<Model3DInfo>(true); // includeInactive = true

            if (model3DInfo == null)
                Debug.LogWarning("[Model3DController] Model3DInfo not found in scene!");
            else if (showDebug)
                Debug.Log($"[Model3DController] Model3DInfo found: {model3DInfo.gameObject.name}");
        }
        else
        {
            if (showDebug)
                Debug.Log($"[Model3DController] Model3DInfo assigned: {model3DInfo.gameObject.name}");
        }
    }

    // ════════════════════════════════════════════════
    // PUBLIC API
    // ════════════════════════════════════════════════

    /// <summary>
    /// Gọi thông thường — không có prefab reference, preview button sẽ bị disable
    /// </summary>
    public void ShowModel3DInfo(Model3D model3D, Texture2D texture = null)
    {
        if (!ValidateBeforeShow(model3D)) return;

        model3DInfo.ShowInfo(model3D, texture);

        if (showDebug)
            Debug.Log($"[Model3DController] ShowModel3DInfo: {model3D.name} (ID: {model3D.id})");
    }

    /// <summary>
    /// ✅ Gọi từ Model3DPrefab.OnInfoColliderClicked()
    /// Truyền prefab reference để Model3DInfo biết target cho Preview camera
    /// </summary>
    public void ShowModel3DInfoWithPrefab(Model3D model3D, Texture2D texture, Model3DPrefab prefab)
    {
        if (!ValidateBeforeShow(model3D)) return;

        model3DInfo.ShowInfoWithPrefab(model3D, texture, prefab);

        if (showDebug)
            Debug.Log($"[Model3DController] ShowModel3DInfoWithPrefab: {model3D.name} (ID: {model3D.id}) | Prefab: {prefab?.name ?? "null"}");
    }

    public void HideModel3DInfo()
    {
        if (model3DInfo != null)
            model3DInfo.HideInfo();
    }

    // ════════════════════════════════════════════════
    // PRIVATE
    // ════════════════════════════════════════════════

    private bool ValidateBeforeShow(Model3D model3D)
    {
        if (model3D == null)
        {
            Debug.LogError("[Model3DController] Model3D is null!");
            return false;
        }

        if (model3DInfo == null)
        {
            Debug.LogError("[Model3DController] Model3DInfo not assigned! Cannot show popup.");
            return false;
        }

        return true;
    }
}