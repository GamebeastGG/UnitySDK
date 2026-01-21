// Assets/Gamebeast/Runtime/IConfigsService.cs
using System;

namespace Gamebeast.Runtime
{
    public interface IConfigsService
    {
        event Action OnReady;

        TValue Get<TValue>(string configName);
        bool IsReady();
    }
}