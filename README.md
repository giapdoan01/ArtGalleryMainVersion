# 🖼️ ArtGallery3D

A real-time 3D virtual art gallery built with Unity WebGL, supporting multiplayer exploration, painting/3D model management, and admin controls.

---

## ✨ Features

- **Multiplayer & Single Player** — Explore the gallery alone or with others in real-time via Colyseus
- **Painting Display** — Load paintings from a REST API with correct frame types (landscape/portrait) and positions
- **3D Model Display** — Load and display GLB models with auto-fit scaling and hover outline effects
- **Admin Mode** — Move, rotate, and remove paintings/models directly in the scene
- **Avatar Selection** — Choose from multiple avatars before entering the gallery
- **Click-to-Move & WASD** — Two movement modes supported simultaneously
- **Live Chat** — In-game chat between players (multiplayer only)
- **Minimap** — Top-down map with click-to-teleport

---

## 🛠️ Tech Stack

| Layer | Technology |
|---|---|
| Engine | Unity 3D (WebGL + Desktop) |
| Language | C# |
| Multiplayer | [Colyseus](https://colyseus.io/) real-time server |
| Backend | REST API (JSON) |
| 3D Models | GLTFast (GLB loader) |
| UI | Unity UI + TextMeshPro |

---

## 🏗️ Architecture

The project follows a **Manager → Controller → View** pattern.

```
┌─────────────────────────────────────────┐
│              Manager Layer              │
│  GameManager  APIManager  NetworkManager│
│  PaintingPrefabManager  Model3DPrefabManager │
│  TextureCache  SoundManager             │
└────────────────────┬────────────────────┘
                     │
┌────────────────────▼────────────────────┐
│             Controller Layer            │
│  PlayerController  PaintingController  │
│  MenuController  Model3DController     │
└────────────────────┬────────────────────┘
                     │
┌────────────────────▼────────────────────┐
│               View / Prefab Layer       │
│  PaintingPrefab  Model3DPrefab          │
│  PlayerView  PaintingInfo  Model3DInfo  │
└─────────────────────────────────────────┘
```

### Key Managers

| Manager | Responsibility |
|---|---|
| `GameManager` | Game mode, session lifecycle, loading flow |
| `APIManager` | Fetch & auto-refresh paintings/models every 30s, PATCH transforms |
| `NetworkManager` | Colyseus connection, player sync, position interpolation |
| `PaintingPrefabManager` | Spawn/pool painting GameObjects |
| `Model3DPrefabManager` | Spawn 3D model GameObjects, cache GLB rotations |
| `TextureCache` | LRU texture cache — avoids re-downloading the same image |

---

## 🔄 Core Flows

### Game Start

```
Menu → Select Mode + Avatar + Name
  ↓
GameManager.HandleGameStarted()
  ↓
[Multiplayer] NetworkManager.ConnectAndJoinRoom()
[Single]      PlayerSpawner.SpawnLocalPlayerSingleMode()
  ↓
APIManager loads paintings + models → PrefabManagers spawn them
```

### Multiplayer Sync

```
PlayerController moves → SendPositionToServer() every 0.1s
  ↓
Colyseus broadcasts → NetworkManager receives state change
  ↓
Remote player positions interpolated each frame (smooth movement)
```

### API Auto-Refresh

```
APIManager.GetDataFromAPI() on Start
  ↓
Every 30s: re-fetch → HasDataChanged()?
  ↓
Yes → fire onApiResponseRefreshed → UI + prefabs update
```

---

## ⚙️ Performance Optimizations

### Object Pool (Paintings)
`PaintingPrefabManager` maintains a pool of inactive GameObjects. Instead of `Instantiate`/`Destroy` on every reload, objects are reused via `SetActive`. This avoids GC spikes on WebGL.

- `initialPoolSize` — pre-warmed objects at Start (default: 10)
- `maxPoolSize` — maximum inactive objects kept in pool (default: 60)

### Texture Cache
`TextureCache` caches downloaded `Texture2D` by URL using an LRU eviction strategy.

- Avoids re-downloading textures on gallery reload
- Deduplicates concurrent requests for the same URL (only one real HTTP request)
- Configurable max size (default: 100 textures)

### Material Instance Reuse
Each `PaintingPrefab` creates its quad material instance **once** and reuses it across pool cycles — only swapping the texture. Previously, a new `Material` was created on every `Setup()` call, leaking GPU memory.

---

## 📁 Project Structure

```
Assets/
├── Script/
│   ├── Manager/
│   │   ├── GameManager.cs
│   │   ├── APIManager.cs
│   │   ├── NetworkManager.cs
│   │   ├── TextureCache.cs
│   │   ├── Paintings/
│   │   │   └── PaintingPrefabManager.cs
│   │   └── Model3D/
│   │       └── Model3DPrefabManager.cs
│   ├── Controller/
│   │   ├── PlayerController.cs
│   │   ├── PaintingController.cs
│   │   └── MenuController.cs
│   ├── View/
│   │   ├── Painting/
│   │   │   ├── PaintingPrefab.cs
│   │   │   └── PaintingInfo.cs
│   │   └── Model3D/
│   │       ├── Model3DPrefab.cs
│   │       └── Model3DInfo.cs
│   ├── Model/
│   │   ├── PaintingData.cs
│   │   └── ModelData.cs
│   └── Schemas/
│       ├── GalleryState.cs
│       └── AdminState.cs
└── Scenes/
    └── Client/
        └── ArtGalleryMain.unity
```

---

## 🌐 API

The game connects to a REST backend to load gallery content.

**Base URL:** `https://projects-admin.vr360.com.vn/api/phongtranh`

### Data Models

**Painting**
```json
{
  "id": 1,
  "name": "Mona Lisa",
  "author": "Leonardo da Vinci",
  "frame_type": "landscape",
  "path_url": "https://...",
  "thumbnail_url": "https://...",
  "position": { "x": 0.0, "y": 1.5, "z": -3.0 },
  "rotate":   { "x": 0.0, "y": 90.0, "z": 0.0 },
  "is_used": 1
}
```

**Model3D**
```json
{
  "id": 1,
  "name": "Sculpture",
  "path_url": "https://.../model.glb",
  "thumbnail_url": "https://...",
  "position": { "x": 2.0, "y": 0.0, "z": 0.0 },
  "rotate":   { "x": 0.0, "y": 0.0, "z": 0.0 },
  "size":     { "x": 1.0, "y": 1.0, "z": 1.0 },
  "is_used": 1
}
```

---

## 🚀 Getting Started

### Requirements
- Unity 2022.3 LTS or newer
- WebGL Build Support module installed

### Run in Editor
1. Clone this repository
2. Open the project in Unity Hub
3. Open scene: `Assets/Scenes/Client/ArtGalleryMain.unity`
4. Press **Play**

### WebGL Build
1. **File → Build Settings** → select **WebGL**
2. **Player Settings → Publishing Settings** → set Compression Format to **Gzip**
3. Click **Build**
4. Serve the output folder with any static HTTP server (e.g. `npx serve`)

### Scene Setup Checklist
Make sure the following components exist in the scene:
- `TextureCache` — empty GameObject with `TextureCache` script attached
- `PaintingPrefabManager` — with `paintingPrefab` assigned in Inspector
- `Model3DPrefabManager` — with `model3DPrefab` assigned in Inspector
- `APIManager`, `NetworkManager`, `GameManager` — as scene singletons

---

## 🎮 Controls

| Action | Input |
|---|---|
| Move | `W A S D` or click on the ground |
| Cancel move target | Any WASD key |
| Interact with painting | Click on painting |
| Admin — move/rotate object | Click transform button on object |
| Admin — remove object | Click remove button on object |

---

## 📝 Notes

- **avatarIndex** is stored as `float` in Colyseus schema — cast to `(int)` when used as array index
- **Chat** is only visible in multiplayer mode
- **WebGL** integration: `MenuController.StartFromWebMenu(jsonData)` can be called from HTML to auto-start with preset name/avatar
- **MeshCollider** on 3D models is disabled on WebGL (IL2CPP limitation) — a pre-assigned `infoCollider` on the prefab is used instead
