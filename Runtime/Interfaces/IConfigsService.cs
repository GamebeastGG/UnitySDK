// Assets/Gamebeast/Runtime/IConfigsService.cs
using System;

namespace Gamebeast.Runtime
{
    public interface IConfigsService
    {

        TValue Get<TValue>(string configName);
        bool IsReady();

        IDisposable OnChanged(string configName, Action callback);
        IDisposable OnReady(Action callback);

    }
}