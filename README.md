# [Gamebeast](https://gamebeast.gg/) Unity SDK
*Documentation: https://docs.gamebeast.gg/*

Client SDK for the Unity engine. This first version ships **Engagement Markers** and **Remote Configurations**; more Gamebeast features will follow in the same module structure.

## What is Gamebeast?
Gamebeast specializes in providing real-time analytics and tools for user engagement, allowing developers to make data-driven decisions to enhance their games.

From the Gamebeast dashboard, developers can automatically detect player pain points and make live updates to their experience. By making adjustments that are seamless to their users, the experience is optimized for engagement - creating a boost in developer revenue.

## Installation
1. Sign up at https://dashboard.gamebeast.gg/ and grab your project API key.
2. Add this repository as a package in your Unity `Packages` folder (or via *Package Manager → Add package from git URL*). Gamebeast will soon be available as a Unity Package.

The SDK depends on `com.unity.nuget.newtonsoft-json`, which Unity resolves automatically.

## Quick start
```csharp
using Gamebeast;

public class Boot : MonoBehaviour
{
    void Start()
    {
        GamebeastSdk.Setup(new GamebeastSettings
        {
            ApiKey = "your-api-key",
            DistinctId = "player-123",  // optional: omit to use an auto-generated, persisted id
            Configurations = new[] { "ConfigA" },  // optional: preload configs and await them via OnReady
        });

        // Markers: batched analytics events, attributed to the local user.
        GamebeastSdk.Markers.SendMarker("level_completed", new { level = 3, time = 42.5f });

        // Configs: remote values published from the dashboard.
        // Paths are "<ConfigName>.<Key>" — the first segment is the configuration alias.
        GamebeastSdk.Configs.OnReady(() =>
        {
            var speed = GamebeastSdk.Configs.Get<float>("ConfigA.PlayerSpeed");
        });

        // React to live config changes:
        GamebeastSdk.Configs.Observe<float>("ConfigA.PlayerSpeed", speed => ApplySpeed(speed));
    }
}
```

## Services
Services are exposed as interfaces on the `GamebeastSdk` entry point (or via `GamebeastSdk.GetService<T>()`).

### Markers — `IMarkersService`
- `SendMarker(markerType, value)` — send an event, attributed to the local user's distinct id.

Markers are batched and flushed automatically (batch full, every 10 seconds, on quit, and when the app is backgrounded). Payloads may contain Unity types like `Vector3`.

### Configs — `IConfigsService`
Values are addressed by dot-path: the first segment is the configuration alias, the rest walks into the document (`"ConfigA.UI.ButtonColor"`, array indexing via `"ConfigA.Levels.0"`). A bare alias returns the whole document.

Configurations load on demand — the first access through any of these methods fetches the configuration automatically. Declaring configurations in `GamebeastSettings.Configurations` additionally preloads them at startup and lets `OnReady` await them.

- `Get<T>(path)` / `Get<T>(path, default)` / `TryGet<T>(path, out value)`
- `IsReady` / `OnReady(callback)` — ready once all configurations declared in `Setup` are available.
- `OnChanged(path, callback)` — fires when the value at the path changes.
- `Observe<T>(path, callback)` — fires as soon as the value is available, then on every change. This is the best fit for lazily loaded configs.
- `RefreshAsync()` — force a refresh of all known configurations now.

Configurations are cached on-device, so values are available instantly on launch (and offline). The cache is stamped with your app version — after a game update, cached configs from the old build are discarded and the SDK waits for fresh values. The SDK re-checks the backend on an interval (configurable via `GamebeastSettings.ConfigRefreshIntervalSeconds`) using hash-based caching, so unchanged configs cost almost nothing.

## Setup options (`GamebeastSettings`)
| Field | Default | Description |
|---|---|---|
| `ApiKey` | — | Required project API key. |
| `DistinctId` | auto | Local user's distinct id. Omit to auto-generate and persist one per device. |
| `Environment` | `Auto` | `Production` / `Development` / `Studio`. `Auto` = Studio in the Editor, Production in builds. |
| `Configurations` | — | Optional. Configs to preload and gate `OnReady` on, e.g. `new[] { "ConfigA" }`; others load on first use. |
| `ConfigRefreshIntervalSeconds` | `60` | Background config refresh cadence; `0` disables. |
| `DebugLogging` | `false` | Verbose SDK console logging. |
| `ApiUrl` | `https://api.gamebeast.gg` | API base URL override. |

## Package layout
```
Runtime/
  GamebeastSdk.cs          Entry point (Setup, service accessors)
  GamebeastSettings.cs     Setup options
  Public/                  Public service interfaces
  Internal/                Runtime host, services, HTTP, models, utils
Editor/                    Editor tooling (heatmap window)
```

Please visit https://docs.gamebeast.gg/ for more details.
