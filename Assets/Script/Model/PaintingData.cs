using System;
using UnityEngine;

[Serializable]
public class PaintingData
{
    public int id;
    public int project_id;
    public int category_id;
    public string name;
    public string frame;
    public string frame_type;
    public Position position;
    public Rotation rotate;
    public int is_active;
    public string author;
    public int is_used;
    public string thumbnail_url;
    public string path_url;
    public PaintingLang paintings_lang;

    // Helper methods
    public Vector3 GetPosition()
    {
        return position != null ? position.ToVector3() : Vector3.zero;
    }

    public Vector3 GetRotation()
    {
        return rotate != null ? rotate.ToVector3() : Vector3.zero;
    }

    public string GetDisplayName()
    {
        if (paintings_lang?.vi != null && !string.IsNullOrEmpty(paintings_lang.vi.name))
        {
            return paintings_lang.vi.name;
        }
        return name;
    }

    public string GetDescription()
    {
        return paintings_lang?.vi?.description ?? "";
    }

    public bool IsAvailable()
    {
        return is_used == 0 && is_active == 1;
    }
}