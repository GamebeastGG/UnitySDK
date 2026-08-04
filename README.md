# [Gamebeast](https://gamebeast.gg/) Unity SDK
*Documentation: https://docs.gamebeast.gg/*

Client SDK for the Unity engine. This first version ships **Engagement Markers**, **Remote Configurations**, **Experiments** and **Cohorts**; more Gamebeast features will follow in the same module structure.

## What is Gamebeast?
Gamebeast specializes in providing real-time analytics and tools for user engagement, allowing developers to make data-driven decisions to enhance their games.

From the Gamebeast dashboard, developers can automatically detect player pain points and make live updates to their experience. By making adjustments that are seamless to their users, the experience is optimized for engagement - creating a boost in developer revenue.

## Installation
1. Sign up at https://dashboard.gamebeast.gg/ and grab your project API key.
2. Add this repository as a package in your Unity `Packages` folder (or via *Package Manager → Add package from git URL*). Gamebeast will soon be available as a Unity Package.

The SDK depends on `com.unity.nuget.newtonsoft-json`, which Unity resolves automatically.

## Quick start

**No-code setup:** create a settings asset via *Assets → Create → Gamebeast → Settings*, fill in your API key, and place it at `Resources/GamebeastSettings`. The SDK initializes itself at startup.

**Code setup** (takes precedence; required if you want to supply a `DistinctId` or `UserProperties` at init):
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

## User identity
The SDK identifies the local user by a **distinct id**: the id you supply (in settings or via `Identify`), or an anonymous id generated once and persisted on the device. Configurations are evaluated and experiments assigned per distinct id.

- `GamebeastSdk.Identify("player-123")` — switch identity after login. Configs re-evaluate for the new user immediately; experiment assignments follow. Not persisted — call again on the next launch once login state restores.
- `GamebeastSdk.ResetIdentity()` — revert to the device's anonymous id after logout.
- `GamebeastSdk.DistinctId` — the effective id for this session.

## Services
Services are exposed as interfaces on the `GamebeastSdk` entry point (or via `GamebeastSdk.GetService<T>()`). `GamebeastSdk.Shutdown()` flushes pending data and tears the SDK down; `Setup` may be called again afterwards.

### Markers — `IMarkersService`
- `SendMarker(markerType, value)` — send an event, attributed to the local user's distinct id.

Markers are batched and flushed automatically (batch full, every 10 seconds, on quit, and when the app is backgrounded). Failed sends retry with exponential backoff, and anything still unsent when the app quits or is backgrounded is persisted to disk and delivered on the next launch. Payloads may contain Unity types like `Vector3`.

Sessions last one app run, and rotate automatically when the app returns from the background after `SessionTimeoutSeconds` (default 30 minutes).

### Configs — `IConfigsService`
Values are addressed by dot-path: the first segment is the configuration alias, the rest walks into the document (`"ConfigA.UI.ButtonColor"`, array indexing via `"ConfigA.Levels.0"`). A bare alias returns the whole document.

Configurations load on demand — the first access through any of these methods fetches the configuration automatically. Declaring configurations in `GamebeastSettings.Configurations` additionally preloads them at startup and lets `OnReady` await them.

- `Get<T>(path)` / `Get<T>(path, default)` / `TryGet<T>(path, out value)`
- `IsReady` / `OnReady(callback)` — ready once all configurations declared in `Setup` are available.
- `OnChanged(path, callback)` — fires when the value at the path changes.
- `Observe<T>(path, callback)` — fires as soon as the value is available, then on every change. This is the best fit for lazily loaded configs.
- `RefreshAsync()` — force a refresh of all known configurations now.

Configurations are cached on-device, so values are available instantly on launch (and offline). The cache is stamped with your app version — after a game update, cached configs from the old build are discarded and the SDK waits for fresh values. The SDK re-checks the backend on an interval (configurable via `GamebeastSettings.ConfigRefreshIntervalSeconds`) using hash-based caching, so unchanged configs cost almost nothing.

Configurations are **evaluated for the local user**: if the user is enrolled in an active experiment, the values returned already include the assigned group's changes — no extra code required.

### Experiments — `IExperimentsService`
Enrollment is automatic: fetching a configuration evaluates active experiments for this user on the backend, and any applied assignments surface here. Use this service for display, analytics segmentation, and debugging — config values already reflect assignments.

- `Assignments` — the user's active experiment assignments across all fetched configurations.
- `OnAssignmentsChanged(callback)` — fires with the full list whenever assignments change.

Experiment targeting can filter on user properties. The SDK always sends standard device/app properties (`platform`, `operatingSystem`, `deviceModel`, `deviceType`, `systemLanguage`, `appVersion`); add your own via `GamebeastSettings.UserProperties`.

### Cohorts — `ICohortsService`
Cohorts are user segments computed from analytics on the dashboard (e.g. `"high_spenders"`).

- `IsMemberAsync(cohortName)` — whether the local user is a member. Results are cached briefly on-device; membership is recomputed by the backend, so treat it as eventually consistent.

## Setup options (`GamebeastSettings`)
| Field | Default | Description |
|---|---|---|
| `ApiKey` | — | Required project API key. |
| `DistinctId` | auto | Local user's distinct id. Omit to auto-generate and persist one per device. |
| `Environment` | `Auto` | `Production` / `Development`. `Auto` = Development in the Editor, Production in builds. |
| `CustomEnvironment` | — | Optional. Target a custom environment by its dashboard alias (e.g. `"staging"`); overrides `Environment`. |
| `Configurations` | — | Optional. Configs to preload and gate `OnReady` on, e.g. `new[] { "ConfigA" }`; others load on first use. |
| `UserProperties` | — | Optional. Custom targeting properties for experiments, e.g. `new Dictionary<string, object> { { "vip", true } }`. |
| `ConfigRefreshIntervalSeconds` | `60` | Background config refresh cadence; `0` disables. |
| `SessionTimeoutSeconds` | `1800` | Rotate the session id after this long in the background; `0` keeps one session per run. |
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
