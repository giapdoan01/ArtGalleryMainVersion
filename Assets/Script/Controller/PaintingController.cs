using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PaintingController : MonoBehaviour
{
    #region Singleton
    private static PaintingController instance;
    public static PaintingController Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<PaintingController>();
            }
            return instance;
        }
    }
    #endregion

    [Header("References")]
    [SerializeField] private PaintingInfo paintingInfo;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;


    private void Start()
    {
        if (paintingInfo == null)
        {
            paintingInfo = FindObjectOfType<PaintingInfo>();

            if (paintingInfo == null)
            {
                paintingInfo = FindObjectOfType<PaintingInfo>(true);
            }

            if (paintingInfo == null)
            {
                Debug.LogWarning("[PaintingController] PaintingInfo not found in scene!");
            }
            else
            {
                if (showDebug)
                    Debug.Log($"[PaintingController] PaintingInfo found: {paintingInfo.gameObject.name}");
            }
        }
        else
        {
            if (showDebug)
                Debug.Log($"[PaintingController] PaintingInfo assigned: {paintingInfo.gameObject.name}");
        }
    }
    public void ShowPaintingInfo(Painting painting, Texture2D texture = null)
    {
        if (painting == null)
        {
            Debug.LogError("[PaintingController] Painting is null!");
            return;
        }

        if (paintingInfo == null)
        {
            Debug.LogError("[PaintingController] PaintingInfo not assigned! Cannot show popup.");
            return;
        }

        if (showDebug)
            Debug.Log($"[PaintingController] Showing info for: {painting.name} (ID: {painting.id})");

        paintingInfo.ShowInfo(painting, texture);
    }

    private PaintingData ConvertToPaintingData(Painting painting)
    {
        return new PaintingData
        {
            id = painting.id,
            project_id = painting.project_id,
            category_id = painting.category_id,
            name = painting.name,
            frame = painting.frame,
            frame_type = painting.frame_type,
            position = painting.position,
            rotate = painting.rotate,
            is_active = painting.is_active,
            author = painting.author,
            is_used = painting.is_used,
            thumbnail_url = painting.thumbnail_url,
            path_url = painting.path_url,
            paintings_lang = painting.paintings_lang
        };
    }
}
