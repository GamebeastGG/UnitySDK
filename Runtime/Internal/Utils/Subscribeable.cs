using System;
using System.Collections.Generic;

namespace Gamebeast.Internal.Utils
{
    /// <summary>
    /// Minimal event source with IDisposable subscriptions. A throwing subscriber
    /// is logged and does not prevent other subscribers from running.
    /// </summary>
    internal sealed class Subscribeable<T>
    {
        private readonly List<Subscription> _subscriptions = new List<Subscription>();

        private sealed class Subscription : IDisposable
        {
            internal readonly Action<T> Callback;
            private Subscribeable<T> _parent;

            public Subscription(Subscribeable<T> parent, Action<T> callback)
            {
                _parent = parent;
                Callback = callback;
            }

            public void Dispose()
            {
                _parent?._subscriptions.Remove(this);
                _parent = null;
            }
        }

        public IDisposable Subscribe(Action<T> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));

            var subscription = new Subscription(this, callback);
            _subscriptions.Add(subscription);
            return subscription;
        }

        public void Trigger(T arg)
        {
            // Copy so subscribers can dispose (or add) subscriptions while firing.
            var snapshot = _subscriptions.ToArray();
            foreach (var subscription in snapshot)
            {
                try
                {
                    subscription.Callback(arg);
                }
                catch (Exception ex)
                {
                    GBLog.Error($"Subscriber threw: {ex}");
                }
            }
        }
    }
}
