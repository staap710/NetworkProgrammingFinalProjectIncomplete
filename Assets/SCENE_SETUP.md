# Coin Rush — Unity Scene Setup Guide

This file explains how to wire up the three scenes in the Unity Editor.
All C# scripts are already written in `Assets/Scripts/`.

---

## Before You Begin

1. Open **Build Settings** (`File → Build Settings`).
2. Add these three scenes **in this order**:
   - `Assets/Scenes/LobbyScene`
   - `Assets/Scenes/GameScene`
   - `Assets/Scenes/ResultScene`
3. The scenes load by name, so the names must match exactly.

---

## Shared Prefabs to Create First

### NetworkManager Prefab
1. Create an empty GameObject → name it `NetworkManager`.
2. Add component: `CoinRush.Networking.NetworkManager`.
3. Drag it into `Assets/Prefabs/` to make a prefab.

### GameManager Prefab
1. Create an empty GameObject → name it `GameManager`.
2. Add component: `CoinRush.Game.GameManager`.
3. Set Inspector fields (optional defaults are fine):
   - `Game Duration Seconds`: 90
   - `Coin Count`: 20
   - Coin Area bounds: Min X = -8, Max X = 8, Min Y = -1, Max Y = 4
4. Drag into `Assets/Prefabs/`.

### Player Prefab
1. Create an empty GameObject → name it `Player`.
2. Add **Sprite Renderer** → assign any sprite (or create a coloured square: right-click in Project → Create → 2D → Sprites → Square).
3. Add **Rigidbody2D** → Freeze Rotation Z = true.
4. Add **Box Collider 2D** (or Capsule) to fit the sprite.
5. Add component: `CoinRush.Game.PlayerController`.
   - Assign the `Sprite Renderer` field.
   - Create a child empty GameObject named `GroundCheck`, position it at the bottom of the sprite (e.g., Y = -0.5). Assign it to `Ground Check Point`.
   - Set `Ground Layer` to the layer you use for platforms (see GameScene).
6. Drag into `Assets/Prefabs/Player.prefab`.

### Coin Prefab
1. Create an empty GameObject → name it `Coin`.
2. Add **Sprite Renderer** → yellow/gold sprite (Create → 2D → Sprites → Circle).
3. Add **Circle Collider 2D** → enable **Is Trigger**.
4. Add component: `CoinRush.Game.Coin`.
5. Drag into `Assets/Prefabs/Coin.prefab`.

### Speed Boost Prefab
1. Create an empty GameObject → name it `PowerupSpeed`.
2. Add **Sprite Renderer** → cyan sprite (diamond/star shape).
3. Add **Circle Collider 2D** → **Is Trigger**.
4. Add component: `CoinRush.Game.Powerup`.
5. Drag into `Assets/Prefabs/PowerupSpeed.prefab`.

### Stun Prefab
1. Same as Speed Boost but with a red/orange sprite.
2. Name it `PowerupStun`.
3. Drag into `Assets/Prefabs/PowerupStun.prefab`.

---

## LobbyScene

### Hierarchy
```
LobbyScene
├── NetworkManager (prefab instance)
├── GameManager    (prefab instance)
├── Main Camera
├── Background (optional coloured sprite)
└── Canvas
    ├── TitleText          [Text] "COIN RUSH"
    ├── HostButton         [Button] "Host Game"
    ├── JoinButton         [Button] "Join Game"
    ├── HostPanel          [Panel — hidden at start]
    │   ├── HostIPText     [Text]
    │   ├── PortLabel      [Text] "Port:"
    │   ├── PortInputField [InputField] default text "7777"
    │   └── HostStatusText [Text]
    ├── JoinPanel          [Panel — hidden at start]
    │   ├── IPLabel        [Text] "Host IP:"
    │   ├── IPInputField   [InputField]
    │   ├── JoinPortLabel  [Text] "Port:"
    │   ├── JoinPortInput  [InputField] default "7777"
    │   ├── ConnectButton  [Button] "Connect"
    │   └── JoinStatusText [Text]
    └── ErrorText          [Text] (optional, leave empty)
```

### LobbyManager Component
1. Create an empty GameObject `LobbyManager` under the Canvas root.
2. Add component: `CoinRush.UI.LobbyManager`.
3. Assign **all** UI references in the Inspector.

> **Important**: The `NetworkManager` and `GameManager` prefabs must be present in LobbyScene.
> They are DontDestroyOnLoad so they persist into GameScene and ResultScene.

---

## GameScene

### Hierarchy
```
GameScene
├── Main Camera       ← add CameraController component
├── TilemapRoot
│   ├── Ground Tilemap      ← TilemapRenderer + TilemapCollider2D + CompositeCollider2D
│   └── Decoration Tilemap  ← TilemapRenderer (no collider, visual only)
├── SpawnPoints
│   ├── SpawnPoint1   ← empty GO at left spawn position, e.g. (-5, 0)
│   └── SpawnPoint2   ← empty GO at right spawn position, e.g. (5, 0)
├── PlayerSpawner     ← add PlayerSpawner component
├── CoinSpawner       ← add CoinSpawner component
├── PowerupSpawner    ← add PowerupSpawner component
└── Canvas
    ├── P1ScorePanel
    │   ├── P1Label   [Text]
    │   └── P1Score   [Text] "0"
    ├── P2ScorePanel
    │   ├── P2Label   [Text]
    │   └── P2Score   [Text] "0"
    ├── TimerText     [Text] "01:30"
    ├── StunOverlay   [Image] full-screen semi-transparent red (hidden at start)
    ├── SpeedOverlay  [Image] full-screen semi-transparent cyan (hidden at start)
    ├── NotificationText [Text] (hidden at start)
    └── HUDManager    ← empty GO with HUDManager component
```

### Camera Setup
- Select `Main Camera`.
- Add component: `CoinRush.Game.CameraController`.
- Targets (A/B) are assigned at runtime by PlayerSpawner — leave empty.
- Set `Min Ortho Size` = 4, `Max Ortho Size` = 9.
- Enable `Clamp Position` and set bounds to match your tilemap size.

### Tilemap Quick Setup
1. `GameObject → 2D Object → Tilemap → Rectangular` to create a Tilemap.
2. Select the **Grid** root → right-click → `2D Object → Tilemap` a second time for decoration layer.
3. On the Ground Tilemap:
   - Add **Tilemap Collider 2D** → enable **Used By Composite**.
   - Add **Composite Collider 2D** (auto-adds Rigidbody2D — set it to Static).
4. Create a new **Layer** called `Ground`. Assign it to the Ground Tilemap.
5. Paint a floor + 3–4 floating platforms with the Tile Palette (`Window → 2D → Tile Palette`).
   - If you have no art yet, use the built-in solid-colour tile: create a new Tile asset, assign a coloured Sprite.

### Layer & Collision Matrix
- In `Edit → Project Settings → Physics 2D`:
  - Make `Player` layer collide with `Ground` layer.
  - Make `Player` layer NOT collide with `Player` layer (players pass through each other).
  - Coins and powerups are triggers so they don't need a Physics2D layer.

### PlayerSpawner Component
- Assign `Player Prefab`, `Spawn Point 1`, `Spawn Point 2`, `Camera Controller`.

### CoinSpawner Component
- Assign `Coin Prefab`.

### PowerupSpawner Component
- Assign `Speed Boost Prefab` and `Stun Prefab`.
- Adjust spawn intervals if desired.

### HUDManager Component
- Assign all Text/Panel references.

---

## ResultScene

### Hierarchy
```
ResultScene
├── Main Camera
└── Canvas
    ├── WinnerText        [Text]  (large, centre)
    ├── SubtitleText      [Text]  (smaller, below WinnerText)
    ├── P1ScoreText       [Text]
    ├── P2ScoreText       [Text]
    ├── PlayAgainButton   [Button] "Play Again"
    ├── QuitButton        [Button] "Quit"
    └── ResultsManager    ← empty GO with ResultsManager component
```

### ResultsManager Component
- Assign all Text and Button references.

> **Note**: There is NO NetworkManager or GameManager prefab in ResultScene.
> They persist from LobbyScene via DontDestroyOnLoad.

---

## Build Settings & Firewall

- **Default port**: 7777 TCP.  
- The **host machine** must allow inbound TCP 7777 through the OS firewall.  
  (Windows Defender Firewall → Advanced Settings → Inbound Rules → New Rule → Port → TCP 7777 → Allow)
- For same-LAN play, find the host IP in `cmd → ipconfig` (IPv4 Address under the active adapter).

---

## Quick Test Checklist

- [ ] Open two Unity Editor instances (use `-projectPath` flag or duplicate builds).
- [ ] Machine A: press Play → click **Host Game** → note the IP displayed.
- [ ] Machine B: press Play → click **Join Game** → enter Machine A's IP → click **Connect**.
- [ ] Both transition to GameScene.
- [ ] Collect a coin on Machine A → confirm it disappears on Machine B.
- [ ] Let the timer expire → both machines show ResultScene.
- [ ] Click **Play Again** → both return to LobbyScene.
