# ArtGallery3D — Tài liệu Dự án

> Unity 3D | Multiplayer | WebGL
> Backend: Colyseus WebSocket + REST API
> Server: `wss://gallery-server.onrender.com` | API: `https://projects-admin.vr360.com.vn/api/phongtranh`

---

## Mục lục

1. [Tổng quan dự án](#1-tổng-quan-dự-án)
2. [Cấu trúc thư mục](#2-cấu-trúc-thư-mục)
3. [Kiến trúc hệ thống](#3-kiến-trúc-hệ-thống)
4. [Scripts — Chi tiết từng module](#4-scripts--chi-tiết-từng-module)
5. [Data Model & API](#5-data-model--api)
6. [Networking — Colyseus Schema](#6-networking--colyseus-schema)
7. [Prefabs](#7-prefabs)
8. [Scenes](#8-scenes)
9. [Luồng hoạt động chính](#9-luồng-hoạt-động-chính)
10. [External Dependencies](#10-external-dependencies)

---

## 1. Tổng quan dự án

**ArtGallery3D** là ứng dụng phòng tranh 3D ảo hỗ trợ cả chế độ **đơn người chơi** và **nhiều người chơi thời gian thực**. Người dùng có thể tham quan gallery, xem tranh, xem mô hình 3D, chat với nhau, và di chuyển bằng click chuột hoặc WASD.

### Tính năng nổi bật

| Tính năng | Mô tả |
|---|---|
| Multiplayer real-time | Đồng bộ vị trí, animation, chat qua Colyseus WebSocket |
| Chọn Avatar | 3 avatar prefab (index 0, 1, 2), lưu vào `PlayerPrefs` |
| Di chuyển | Click-to-move + WASD, tích hợp minimap click |
| Painting Gallery | Load tranh từ API, hiển thị theo frame landscape/portrait |
| 3D Model Gallery | Load model `.glb/.gltf` bằng GLTFast, preview camera xoay |
| Admin Panel | Chỉnh transform, thêm/xóa tranh và mô hình qua REST API |
| Minimap | Toggle small/big, click để teleport player |
| Chat | Gửi nhận tin nhắn real-time qua Colyseus room |
| Cursor thông minh | Context-aware cursor (default / ground / clickable) |
| NPC Patrol | NPC tự động tuần tra theo waypoints |
| Sound | Nhạc nền, toggle + lưu trạng thái `PlayerPrefs` |

---

## 2. Cấu trúc thư mục

```
Assets/
├── Colyseus/                    # Thư viện networking Colyseus (third-party)
├── Model/
│   ├── Audio/                   # File âm thanh
│   ├── AvatarVR/                # Avatar .glb, animation clips (đã xóa một số)
│   ├── avatar1/, avatar2/, avatar3/   # Mesh/texture từng avatar
│   ├── Materials/               # Vật liệu 3D
│   └── FolderNewAsset/          # Icon UI (minimap, zoom, location...)
├── Plugins/
│   ├── ParrelSync/              # Tool chạy nhiều instance Unity cùng lúc
│   └── WebGL/                   # Plugin hỗ trợ WebGL build
├── Prefab/
│   ├── Avatar/                  # Avatar1.prefab, Avatar2.prefab, Avatar3.prefab
│   │   └── NPCAnim/             # NPC animation prefabs
│   ├── gallery/                 # MinimapCamera.prefab, gallery prefabs
│   ├── ChatItem.prefab
│   ├── ModelItemVisitor.prefab
│   ├── PaintingItemVisitor.prefab
│   ├── PaintingPrefab.prefab
│   ├── cursorPreviewPrefab.prefab
│   ├── cursorPreviewPrefabMinimap.prefab
│   ├── mouseclickPrefab.prefab
│   └── mouseclickPrefabMinimap.prefab
├── Scenes/
│   ├── Admin/                   # Scene quản trị (Admin)
│   └── Client/
│       └── ArtGalleryMain.unity # Scene chính (visitor + single player)
├── Script/                      # Toàn bộ C# scripts (~56 files)
│   ├── Cameras/
│   ├── Controller/
│   ├── Manager/
│   │   ├── Paintings/
│   │   └── Model3D/
│   ├── Model/
│   ├── RuntimeGizmo/
│   ├── Schemas/
│   ├── Service/
│   ├── UI/
│   ├── View/
│   │   ├── Painting/
│   │   └── Model3D/
│   └── utils/
├── Shaders/
│   ├── SmallMinimapRender.renderTexture
│   └── BigMinimapRender.renderTexture
└── TextMesh Pro/
    └── Fonts/                   # CascadiaCode (Regular, Medium, Bold SDF)
```

---

## 3. Kiến trúc hệ thống

```
┌─────────────────────────────────────────────────────┐
│                    UNITY CLIENT                      │
│                                                      │
│  MenuView ──► MenuController ──► PlayerSpawner       │
│       │                               │              │
│       ▼                               ▼              │
│  GameManager              PlayerController           │
│       │                    ├── PlayerModel           │
│       │                    ├── PlayerView            │
│       │                    └── CameraFollow          │
│       │                                              │
│  NetworkManager ◄──────────────────────────────────  │
│       │ (Colyseus WebSocket)                         │
│       ▼                                              │
│  ┌─────────────┐   ┌──────────────────────────┐      │
│  │ PaintingMgr │   │   Model3DPrefabManager   │      │
│  │ PrefabMgr   │   │   (GLTFast loader)       │      │
│  └─────────────┘   └──────────────────────────┘      │
│       │                        │                     │
│       ▼                        ▼                     │
│  APIManager ─────────────────────────────────────    │
│       │ (REST API + auto-refresh 30s)                │
│       ▼                                              │
│  PaintingClickManager / Model3DClickManager          │
│  PaintingController / Model3DController              │
│  CombinedCursorManager / MinimapManager              │
│  ChatNetworkHandler / ChatUIManager                  │
└─────────────────────────────────────────────────────┘
         │                          │
         ▼                          ▼
  Colyseus Server            REST API Backend
  (WebSocket)          (projects-admin.vr360.com.vn)
```

### Singleton Managers

| Class | Singleton | Mô tả |
|---|---|---|
| `GameManager` | `Instance` | Quản lý game state, mode, leave game |
| `NetworkManager` | `Instance` | Kết nối Colyseus, sync players |
| `APIManager` | `Instance` | Fetch & PATCH dữ liệu REST |
| `PaintingPrefabManager` | `Instance` | Spawn/reload painting prefabs |
| `Model3DPrefabManager` | `Instance` | Spawn/reload 3D model prefabs |
| `PaintingClickManager` | `Instance` | Raycast click paintings |
| `Model3DClickManager` | `Instance` | Raycast click 3D models |
| `PaintingController` | `Instance` | Hiển thị info panel tranh |
| `Model3DController` | `Instance` | Hiển thị info panel model |
| `CombinedCursorManager` | `Instance` | Quản lý trạng thái cursor |
| `MenuController` | `Instance` | Menu + avatar selection |
| `PlayerTeleportManager` | `Instance` | Teleport player |
| `SoundManager` | `Instance` | Nhạc nền |

---

## 4. Scripts — Chi tiết từng module

### 4.1 Cameras

#### `CameraFollow.cs`
Camera bám theo player với chế độ third-person, hỗ trợ:
- **Drag to rotate**: giữ chuột trái kéo để xoay camera
- **IsCleanClick**: property để `PlayerController` phân biệt click thuần vs drag
- Orbit camera xung quanh target theo spherical coordinates

---

### 4.2 Controllers

#### `MenuController.cs`
- Quản lý menu chọn avatar (3 prefab: index 0, 1, 2)
- Xử lý `OnSinglePlayerButtonClicked` và `OnMultiPlayerButtonClicked`
- `GetPreviewInstanceSet()` trả về set các preview avatar để `GameManager` không destroy nhầm
- `ResetToMenuState()` về trạng thái menu sau khi leave game

#### `PlayerController.cs`
- `[RequireComponent(CharacterController, PlayerView)]`
- **Local player**: WASD + click-to-move + gửi position/animation lên server
- **Remote player**: nhận position từ server, interpolation mượt
- Quản lý `cursorPreviewPrefab` (3D object theo cursor trên ground)
- `IsShowingGroundCursor` (static) → `CombinedCursorManager` đọc để ẩn/hiện system cursor
- `SetMoveTarget(worldPos)` → `MinimapClickMove` gọi để teleport từ minimap

```csharp
// Khởi tạo player
controller.Initialize(sessionId, playerNetworkState, isLocal);

// Di chuyển từ minimap
controller.SetMoveTarget(worldPosition);
```

#### `PaintingController.cs` / `Model3DController.cs`
- Nhận sự kiện click từ `PaintingClickManager` / `Model3DClickManager`
- Gọi `ShowPaintingInfo(painting, texture)` / `ShowModel3DInfo(model3d)`
- Hiển thị panel thông tin với animation fade/slide

---

### 4.3 Managers

#### `GameManager.cs`
```
Lifecycle: Awake → RegisterMenuEvents → Start → SetupMoveButtonGuide + SetupLoadingPanel
```
- **Single/Multi mode**: ẩn/hiện nút Chat theo mode
- **MoveButtonGuide**: auto-show 10s sau khi LoadingPanel ẩn, user có thể mở/đóng tay
- **LeaveGame**: disconnect Colyseus (timeout 3s), destroy player objects, reset về menu

#### `NetworkManager.cs`
```
Server: wss://gallery-server.onrender.com
Room: "gallery"
Update rate: 0.1s | Position lerp: 10f | Rotation lerp: 15f
```
- Kết nối qua `JoinOrCreate<GalleryState>(roomName, { username, avatarIndex })`
- `MonitorPlayerChanges()` coroutine poll 0.1s để detect join/leave
- Interpolation: `GetInterpolatedPosition()` / `GetInterpolatedRotation()`
- Messages: `move` (x,y,z,rotY), `animation` (state), `chat` (message)

#### `APIManager.cs`
```
Base URL: https://projects-admin.vr360.com.vn/api/phongtranh
Token: Project header
Auto refresh: 30s (configurable)
```

**Endpoints:**

| Method | URL | Header | Mô tả |
|---|---|---|---|
| GET | `/data-masters` | `Project: token` | Lấy toàn bộ paintings + model3ds |
| PATCH | `/save-data` | `Id, Type, Project` | Cập nhật transform/is_used |
| PATCH | `/remove-data` | `Id, Type, Project` | Xóa painting/model3d |

**Auto-refresh**: So sánh count và `is_used`, `frame_type` để detect thay đổi, emit `onApiResponseRefreshed`.

#### `PaintingPrefabManager.cs`
- Lắng nghe `APIManager.onApiResponseLoaded` và `onApiResponseRefreshed`
- Spawn `PaintingPrefab` cho mỗi painting có `is_used = 1`
- Download texture từ `path_url` bằng `UnityWebRequestTexture`
- `ReloadAllPaintings()` xóa tất cả và spawn lại

#### `Model3DPrefabManager.cs`
- Tương tự `PaintingPrefabManager` nhưng cho mô hình 3D
- Dùng **GLTFast** để load file `.glb/.gltf` từ URL
- Mỗi model có thumbnail, position, rotation, size

#### `PaintingClickManager.cs`
- Singleton, các `PaintingPrefab` tự đăng ký/hủy đăng ký qua `RegisterPainting()` / `UnregisterPainting()`
- **Anti-drag**: ghi nhận vị trí MouseDown, nếu drag > 8px thì bỏ qua click
- Không raycast lại lúc MouseUp — dùng painting đã ghi nhận lúc MouseDown

#### `Model3DClickManager.cs`
- Tương tự `PaintingClickManager`, thêm **hover state tracking**
- Detect hover để đổi cursor qua `CombinedCursorManager`

#### `MinimapManager.cs`
- Toggle giữa small minimap và big minimap
- Mỗi minimap dùng `RenderTexture` riêng (`SmallMinimapRender`, `BigMinimapRender`)

#### `CombinedCursorManager.cs` (SimpleCursorManager)
Priority states cho cursor:
1. **Default** — system cursor bình thường
2. **Ground** — cursor ẩn, hiện `cursorPreviewPrefab` 3D
3. **Clickable** — cursor dạng "hand" khi hover painting/model

#### `PlayerTeleportManager.cs`
- `RegisterLocalPlayer(transform, characterController)`
- Teleport với animation fade + sync camera

#### `ChatNetworkHandler.cs` + `ChatNetworkAdapter.cs`
- `ChatNetworkAdapter`: dùng reflection để subscribe Colyseus room message listener
- `ChatNetworkHandler`: validate, rate-limit, gửi qua `NetworkManager.SendChatMessage()`
- `ChatUIManager`: hiển thị messages với màu khác nhau (local/remote)

#### `SoundManager.cs`
- Toggle nhạc nền, lưu trạng thái vào `PlayerPrefs`

---

### 4.4 Models (Data Classes)

#### `PlayerModel.cs`
State của player cục bộ:
- `IsLocalPlayer`, `IsMovingToTarget`, `TargetPosition`
- `SetTargetPosition(pos)`, `HasReachedTarget(pos)`
- `UpdateFromNetwork(x, y, z, rotY)`
- Event `OnSpeedChanged` → `PlayerView.UpdateAnimationSpeed()`

#### `ModelData.cs` — Data structures từ API

```csharp
APIResponse {
    int status;
    ResponseData data {
        List<Category> categories;
        List<Model3D>  model3ds;
        List<Painting> paintings;
    }
}

Painting {
    int id, project_id, category_id;
    string name, frame, frame_type, author;
    Position position; Rotation rotate;
    int is_active, is_used;
    string thumbnail_url, path_url;
    PaintingLang paintings_lang { LanguageData vi; }
}

Model3D {
    int id, project_id, category_id;
    string name, author;
    Position position; Rotation rotate; Size size;
    int is_active, is_used;
    string thumbnail_url, path_url;
    Model3DLang model3ds_lang { LanguageData vi; }
}

// frame_type values:
// "wood_horizontal" | "landscape" | "1"  → landscape frame
// "wood_vertical"   | "portrait"  | "2"  → portrait frame
```

---

### 4.5 Schemas (Colyseus Network)

#### `GalleryState.cs`
```csharp
GalleryState : Schema {
    MapSchema<Player>        players;       // map sessionId → Player
    ArraySchema<ChatMessage> chatMessages;
    float                    serverTime;
}

Player : Schema {
    string sessionId, username;
    float  avatarIndex;   // NOTE: float vì Colyseus JS "number" = float
    float  x, y, z, rotationY;
    string animationState;
    bool   isMoving;
}

ChatMessage : Schema {
    string id, sessionId, username, message;
    float  timestamp;
}
```

> **Quan trọng**: `avatarIndex` là `float` (không phải `int`) do Colyseus encode "number" thành float. Khi dùng làm array index phải cast: `(int)player.avatarIndex`.

---

### 4.6 Service

#### `PlayerSpawner.cs`
- `Start()` đăng ký `NetworkManager.OnPlayerJoined/OnPlayerLeft`
- `SpawnPlayer(sessionId, player)`: instantiate avatar prefab theo `avatarIndex`, gọi `controller.Initialize()`
- `SpawnLocalPlayerSingleMode(player)`: spawn cho chế độ đơn
- `AttachFollowMapCamera(playerObj)`: gán minimap follow camera làm con của local player
- Sau khi spawn local player: đăng ký với `PlayerTeleportManager`

---

### 4.7 Views

#### `PaintingPrefab.cs`
Component gắn vào prefab tranh trong scene:
- `Setup(painting, texture)`: apply texture, chọn frame, set name tag, apply transform
- **Frame types**: `landscape` (wood_horizontal/1) vs `portrait` (wood_vertical/2) — tự scale quad + frame theo aspect ratio ảnh
- **Hover outline**: animation fade in/out, scale pulse khi hover
- **Name tag**: TMP_Text hiển thị tên tranh + tác giả (luôn hiển thị)
- **Teleport point**: auto-create nếu không có, offset 2m trước tranh
- **Admin buttons**: Transform (mở Gizmo + popup) và Remove (confirm popup → API)

```csharp
// Các components chính
MeshRenderer quadRenderer;      // Quad hiển thị ảnh tranh
Transform landscapeFrame;       // Frame ngang
Transform portraitFrame;        // Frame dọc
RuntimeTransformGizmo gizmo;    // Gizmo chỉnh transform runtime
TMP_Text nameTagText;           // Tên tranh
TMP_Text authorTagText;         // Tên tác giả
Transform teleportPoint;        // Vị trí teleport khi click
```

#### `PlayerView.cs`
- Name tag (TMP_Text) luôn nhìn về camera, ẩn với local player
- Map icon (Image): sprite khác nhau cho local/remote player, luôn giữ rotation (0,180,0) world space
- `UpdateAnimationSpeed(float)`: set Animator "Speed" parameter

#### `Model3DInfo.cs`
- Hiển thị thông tin model 3D
- Preview camera: xoay model trong panel nhỏ

---

### 4.8 Utils

#### `MinimapClickMove.cs`
Chuyển đổi click trên minimap UI → di chuyển player:
```
Screen position → Local point trên RawImage → UV (0..1)
→ áp dụng uvRect → Ray từ MinimapCamera → Raycast Ground → worldPos
→ PlayerController.SetMoveTarget(worldPos)
```

#### `NPCPatrol.cs`
- Patrol theo mảng waypoints
- NavMesh hoặc di chuyển thủ công

#### `UIBillboard.cs`
- 3D UI luôn quay mặt về camera
- Theo dõi đối tượng trên bề mặt cầu

#### `LoadingPanel.cs`
- Auto-hide sau 5s với fade effect
- Event `OnPanelHidden` → `GameManager` bắt đầu timer MoveButtonGuide

---

### 4.9 UI

#### `PanelListItemVisitor.cs`
- Quản lý animation và visibility của các panel: ListItem, Chat, Model3D, Painting
- `SetChatButtonVisible(bool)` — GameManager gọi khi switch Single/Multi mode

#### `UIEffectUtils.cs`
- `Fade(CanvasGroup, from, to, duration)`
- `Scale(Transform, from, to, duration)`
- `Slide(RectTransform, from, to, duration)`

#### `ChatUIManager.cs`
- Hiển thị messages với màu khác nhau
- Auto-scroll xuống tin mới nhất

---

## 5. Data Model & API

### API Request/Response

**GET `/data-masters`**
```json
{
  "status": 200,
  "message": "success",
  "data": {
    "categories": [...],
    "paintings": [
      {
        "id": 1,
        "name": "Tên tranh",
        "frame_type": "wood_horizontal",
        "author": "Tác giả",
        "is_used": 1,
        "thumbnail_url": "https://...",
        "path_url": "https://...",
        "position": {"x": 0, "y": 2, "z": 5},
        "rotate": {"x": 0, "y": 90, "z": 0},
        "paintings_lang": { "vi": { "name": "...", "description": "..." } }
      }
    ],
    "model3ds": [
      {
        "id": 1,
        "name": "Tên model",
        "is_used": 1,
        "path_url": "https://.../model.glb",
        "position": {...}, "rotate": {...},
        "size": {"x": 1, "y": 1, "z": 1}
      }
    ]
  }
}
```

**PATCH `/save-data`** — Headers: `Id`, `Type` (painting/model3d), `Project`
```json
// Painting
{ "is_used": 1, "frame_type": "wood_horizontal", "position": {...}, "rotate": {...} }

// Model3D
{ "is_used": 1, "position": {...}, "rotate": {...}, "size": {...} }
```

**PATCH `/remove-data`** — Headers: `Id`, `Type`, `Project` (no body)

---

## 6. Networking — Colyseus Schema

### Room: `"gallery"`
```
Server: wss://gallery-server.onrender.com
Join options: { username: string, avatarIndex: number }
```

### Messages gửi từ client

| Message | Payload | Mô tả |
|---|---|---|
| `move` | `{x, y, z, rotationY}` | Cập nhật vị trí player |
| `animation` | `{state: "idle"\|"walk"}` | Cập nhật animation |
| `chat` | `{message: string}` | Gửi tin nhắn chat |

### State sync

- Server broadcast `GalleryState` → client nhận qua `OnStateChange`
- Local player bị bỏ qua trong state update (không override vị trí do chính mình điều khiển)
- Interpolation: `positionLerpSpeed=10f`, `rotationLerpSpeed=15f`

---

## 7. Prefabs

### Avatar Prefabs (`Assets/Prefab/Avatar/`)

| Prefab | Index | Mô tả |
|---|---|---|
| `Avatar1.prefab` | 0 | Avatar nữ |
| `Avatar2.prefab` | 1 | Avatar nam |
| `Avatar3.prefab` | 2 | Avatar thứ 3 |

Mỗi avatar prefab cần có:
- `PlayerController` component
- `PlayerView` component
- `CharacterController` component
- `Animator` với parameter `Speed` (float)
- Name tag Canvas (ẩn với local player)
- Map icon Canvas

### Gallery Prefabs (`Assets/Prefab/`)

| Prefab | Mô tả |
|---|---|
| `PaintingPrefab.prefab` | Tranh trong scene (quad + frame + collider) |
| `cursorPreviewPrefab.prefab` | Preview vị trí cursor trên ground (main scene) |
| `cursorPreviewPrefabMinimap.prefab` | Preview cursor trên minimap |
| `mouseclickPrefab.prefab` | Hiệu ứng click trên ground (main) |
| `mouseclickPrefabMinimap.prefab` | Hiệu ứng click từ minimap |
| `ChatItem.prefab` | Item trong danh sách chat |
| `ModelItemVisitor.prefab` | Item model 3D trong danh sách visitor |
| `PaintingItemVisitor.prefab` | Item tranh trong danh sách visitor |

### Gallery Infrastructure (`Assets/Prefab/gallery/`)

| Prefab | Mô tả |
|---|---|
| `MinimapCamera.prefab` | Camera overhead cho minimap, gắn theo local player |

---

## 8. Scenes

### `ArtGalleryMain.unity` (Client)
Scene chính, chứa toàn bộ hệ thống:
- `GameManager` (DontDestroyOnLoad)
- `NetworkManager` (DontDestroyOnLoad)
- `APIManager` (DontDestroyOnLoad)
- `PlayerSpawner` — spawn players theo network events
- `PaintingPrefabManager` — quản lý painting prefabs
- `Model3DPrefabManager` — quản lý model3D prefabs
- `PaintingClickManager` — detect click tranh
- `Model3DClickManager` — detect click model
- Menu Canvas (MenuView, MenuController)
- HUD Canvas (Chat, Minimap, Panels, MoveGuide)
- Gallery environment (walls, floor, lights)

### Admin Scene (`Assets/Scenes/Admin/`)
Scene cho admin:
- Popup xác nhận xóa tranh/model: `ConfirmRemovePaintingPopup.prefab`, `ConfirmRemoveModelPopup.prefab`
- Popup chỉnh sửa transform: `PaintingTransformEditPopup.prefab`, `Model3DTransformEditPopup.prefab`
- Popup nhập thông tin: `InputInfoPopup.prefab`, `ImageTransformPopup.prefab`

---

## 9. Luồng hoạt động chính

### 9.1 Khởi động game

```
1. Scene load
   ├── GameManager.Awake() → RegisterMenuEvents
   ├── APIManager.Start() → GetDataFromAPI() → StartAutoRefresh(30s)
   └── LoadingPanel auto-hide (5s)
       └── GameManager → StartMoveButtonGuideTimer()

2. User nhập tên + chọn avatar → click Single/Multi
   ├── Single: MenuController → PlayerSpawner.SpawnLocalPlayerSingleMode()
   └── Multi:  MenuController → NetworkManager.ConnectAndJoinRoom()
               └── NetworkManager.OnConnected → PlayerSpawner → SpawnPlayer()
```

### 9.2 Spawn Player

```
PlayerSpawner.SpawnPlayer(sessionId, player)
├── GetAvatarPrefab(player.avatarIndex)  // clamp to valid range
├── Instantiate(prefab, spawnPosition)
├── PlayerController.Initialize(sessionId, playerState, isLocal)
│   ├── new PlayerModel(...)
│   ├── PlayerView.Initialize(username, isLocal)
│   │   ├── Set name tag text
│   │   ├── Set map icon sprite
│   │   └── isLocal → CameraFollow.SetTarget(this.transform)
│   └── isLocal → InitializeCursorPreview()
├── isLocal → AttachFollowMapCamera(playerObj)
└── isLocal → PlayerTeleportManager.RegisterLocalPlayer()
```

### 9.3 Di chuyển Player

```
// Click-to-move
Input.GetMouseButtonDown(0) → CameraFollow.IsCleanClick
└── PlayerController.TrySetMoveTarget()
    ├── Raycast ground layer
    ├── PlayerModel.SetTargetPosition(pos)
    └── SpawnClickEffect(pos)

// WASD
Input.GetAxis("Horizontal/Vertical")
└── PlayerController.MoveWithWASD()
    └── camera-relative direction movement

// Minimap click
MinimapClickMove.OnPointerClick()
├── ScreenPos → UV → MinimapCamera ray → Raycast Ground → worldPos
└── PlayerController.SetMoveTarget(worldPos)

// Network sync (0.1s interval)
PlayerController.SendPositionToServer()
└── NetworkManager.SendPosition(x, y, z, rotY)
```

### 9.4 Load và hiển thị Tranh

```
APIManager.onApiResponseLoaded
└── PaintingPrefabManager.OnApiDataLoaded()
    └── foreach painting where is_used == 1:
        ├── Instantiate(PaintingPrefab)
        ├── StartCoroutine(DownloadTexture(path_url))
        │   └── UnityWebRequestTexture.GetTexture(url)
        └── PaintingPrefab.Setup(painting, texture)
            ├── ApplyTexture(texture)          // set quad material
            ├── SetupFrame(frame_type, texture) // scale quad + frame theo AR
            ├── SetNameTag(name, author)        // TMP_Text
            ├── ApplyTransformFromData(painting) // set position/rotation
            └── PaintingClickManager.RegisterPainting(this)
```

### 9.5 Click xem thông tin Tranh

```
PaintingClickManager.Update()
├── MouseDown → RaycastPainting() → lưu pressedPainting
├── MouseDrag > 8px → isDragging = true
└── MouseUp (không drag, không UI) → pressedPainting.OnInfoColliderClicked()
    └── PaintingController.ShowPaintingInfo(painting, texture)
        └── Hiển thị panel với fade/slide animation
```

### 9.6 Leave Game

```
GameManager.LeaveGame()
├── NetworkManager.Disconnect() + chờ OnDisconnected (timeout 3s)
├── ClearPlayerSession() → PlayerPrefs.DeleteKey
├── DestroyAllPlayerObjects() (tag "Player", skip preview instances)
├── MenuView.HideInGameUI()
└── MenuController.ResetToMenuState()
```

---

## 10. External Dependencies

| Package | Phiên bản | Mục đích |
|---|---|---|
| **Colyseus** | SDK 0.14.x | WebSocket multiplayer networking |
| **GLTFast** | 5.x | Load file .glb/.gltf runtime |
| **TextMesh Pro** | Unity built-in | Text rendering chất lượng cao |
| **ParrelSync** | - | Test multiplayer với nhiều Unity instance |
| **Unity New Input System** | - | Input handling (WASD, mouse) |

### PlayerPrefs Keys

| Key | Type | Mô tả |
|---|---|---|
| `AvatarIndex` | int | Avatar đã chọn (0, 1, 2) |
| `CurrentSessionId` | string | Session ID hiện tại |
| `LastPosition` | string | Vị trí cuối trước khi leave |
| `SoundEnabled` | int | Trạng thái nhạc nền (0/1) |

---

## Ghi chú kỹ thuật quan trọng

### Colyseus avatarIndex là float
```csharp
// ĐÚNG
int index = (int)player.avatarIndex;
// SAI — crash khi decode
// [Type(2, "number")] public int avatarIndex;
```

### Camera drag vs click
`CameraFollow` expose `IsCleanClick` (true chỉ khi mouse không drag).
`PlayerController` đọc `IsCleanClick` thay vì `Input.GetMouseButtonUp(0)` để tránh bắn move event khi user đang xoay camera.

### Cursor management
`PlayerController` chỉ quản lý 3D `cursorPreviewPrefab` (hiện/ẩn), **không bao giờ** set `Cursor.visible` trực tiếp — delegate hoàn toàn cho `CombinedCursorManager`.

### Frame scaling
`PaintingPrefab.SetupFrame()` tự tính aspect ratio từ texture và scale quad + frame tương ứng để giữ đúng tỉ lệ ảnh với cả hai loại frame (landscape/portrait).

### Auto-refresh API
`APIManager` so sánh `count`, `is_used`, `frame_type` trước khi emit event để tránh re-spawn không cần thiết. Chỉ emit `onApiResponseRefreshed` khi có thay đổi thực sự.
