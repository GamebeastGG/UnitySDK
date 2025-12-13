// Assets/Gamebeast/Runtime/IMarkersService.cs
namespace Gamebeast.Runtime
{
    public interface IMarkersService
    {
        void SendMarker<TValue>(string markerName, TValue value);
        // Add other public marker operations when needed
    }
}