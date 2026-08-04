using UnityEngine;

namespace Gamebeast.Internal
{
    /// <summary>
    /// Initializes the SDK at startup from a <see cref="GamebeastSettingsAsset"/>
    /// placed at "Resources/GamebeastSettings", so games can integrate without any
    /// bootstrap code. A manual <see cref="GamebeastSdk.Setup(GamebeastSettings)"/>
    /// that ran earlier wins; without the asset this does nothing.
    /// </summary>
    internal static class GamebeastAutoInit
    {
        private const string ResourcePath = "GamebeastSettings";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            if (GamebeastSdk.IsInitialized) return;

            var asset = Resources.Load<GamebeastSettingsAsset>(ResourcePath);
            if (asset == null || !asset.AutoInitialize) return;

            if (string.IsNullOrWhiteSpace(asset.ApiKey))
            {
                GBLog.Warn("GamebeastSettings asset found but its ApiKey is empty; skipping auto-initialization.");
                return;
            }

            GBLog.Info("Auto-initializing from Resources/GamebeastSettings.");
            GamebeastSdk.Setup(asset.ToSettings());
        }
    }
}
