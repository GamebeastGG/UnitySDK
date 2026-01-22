using System;

namespace Gamebeast.Runtime.Internal.Utils
{
    public class Subscribeable<T>
    {
        internal Action<T> trigger;
        private class Subscription : IDisposable
        {
            private bool _disposed;
            internal Action<T> _callback;
            private Subscribeable<T> _parent;
            public Subscription(Subscribeable<T> parent, Action<T> callback)
            {
                _callback = callback;
                _parent = parent;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                _parent.trigger -= _callback;
                _parent = null;
            }
        }

        public IDisposable Subscribe(Action<T> callback)
        {
            var newSubscription = new Subscription(this, callback);
            trigger += callback;

            return newSubscription;
        }

        public void Trigger(T arg)
        {
            trigger?.Invoke(arg);
        }
    }
}