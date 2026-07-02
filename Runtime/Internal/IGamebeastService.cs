namespace Gamebeast.Internal
{
    /// <summary>
    /// Lifecycle contract for SDK services hosted by <see cref="GamebeastRuntime"/>.
    /// Services are registered against their public interface and driven by the
    /// runtime's MonoBehaviour callbacks.
    /// </summary>
    internal interface IGamebeastService
    {
        /// <summary>Called once during Setup, in registration order.</summary>
        void Initialize(GamebeastContext context);

        /// <summary>Called every frame while the SDK is initialized.</summary>
        void Tick(float deltaTime);

        /// <summary>Called when the app is paused/resumed (mobile backgrounding).</summary>
        void OnApplicationPause(bool paused);

        /// <summary>Called when the app quits. Flush any pending work here.</summary>
        void Shutdown();
    }
}
