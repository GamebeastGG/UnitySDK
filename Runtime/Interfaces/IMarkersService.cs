// Assets/Gamebeast/Runtime/IMarkersService.cs
using Gamebeast.Runtime.Internal.Services;

namespace Gamebeast.Runtime
{
    public interface IMarkersService
    {
        void SendMarker<TValue>(string markerName, TValue value);
        void SendPlayerMarker<TValue>(string userId, string markerName, TValue value);
        // Add other public marker operations when needed
    }
}