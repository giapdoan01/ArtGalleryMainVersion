using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class APIResponse
{
    public int status;
    public string message;
    public ResponseData data;
}

[Serializable]
public class ResponseData
{
    public List<Category> categories;
    public List<Model3D> model3ds;
    public List<Painting> paintings;
}

[Serializable]
public class Category
{
    public int id;
    public int project_id;
    public string name;
    public int parent_id;
    public int is_active;
    [NonSerialized]
    public List<Category> children;
}

[Serializable]
public class Model3D
{
    public int id;
    public int project_id;
    public int category_id;
    public string name;
    public Position position;
    public Rotation rotate;
    public Size size;
    public int is_active;
    public string author;
    public int is_used;
    public string thumbnail_url;
    public string path_url;
    public Model3DLang model3ds_lang;
}

[Serializable]
public class Painting
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
}

[Serializable]
public class Position
{
    public float x;
    public float y;
    public float z;
    
    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}

[Serializable]
public class Rotation
{
    public float x;
    public float y;
    public float z;
    
    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}

[Serializable]
public class Size
{
    public float x;
    public float y;
    public float z;
    
    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}

[Serializable]
public class Model3DLang
{
    public LanguageData vi;
}

[Serializable]
public class PaintingLang
{
    public LanguageData vi;
}

[Serializable]
public class LanguageData
{
    public int model_3d_id;
    public int painting_id;
    public string language;
    public string name;
    public string description;
}
[Serializable]
public class PaintingFullUpdateWithFrameData
{
    public int is_used;
    public string frame_type;
    public Position position;
    public Rotation rotate;
}
[Serializable]
public class PaintingTransformUpdateData
{
    public Position position;
    public Rotation rotate;
}

[Serializable]
public class Model3DTransformUpdateData
{
    public Position position;
    public Rotation rotate;
    public Size size;
}

[Serializable]
public class PaintingFullUpdateData
{
    public int is_used;
    public Position position;
    public Rotation rotate;
}

//  NEW: Data class cho Model3D full update (is_active + transform + size)
[Serializable]
public class Model3DFullUpdateData
{
    public int is_used;  //  ĐỔI is_active → is_used
    public Position position;
    public Rotation rotate;
    public Size size;
}