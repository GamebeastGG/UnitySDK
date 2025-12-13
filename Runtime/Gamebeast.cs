using Gamebeast.Internal;
using Gamebeast.Runtime.Internal.Services;

namespace Gamebeast.Runtime
{
    public static class GamebeastSdk
    {
        public static void Init(string apiKey)
        {
            GamebeastRuntime.Instance.Init(apiKey);
        }

        public static IMarkersService Markers =>
            GamebeastRuntime.Instance.GetService<MarkersService>();
    }
}