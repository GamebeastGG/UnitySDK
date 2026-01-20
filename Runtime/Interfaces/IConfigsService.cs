// Assets/Gamebeast/Runtime/IConfigsService.cs
namespace Gamebeast.Runtime
{
    public interface IConfigsService
    {
        TValue Get<TValue>(string configName);
        bool IsReady();
    }
}