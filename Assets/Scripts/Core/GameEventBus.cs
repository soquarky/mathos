using System;
using System.Collections.Generic;

namespace StevensMathOS
{
    /// <summary>
    /// Centralized event bus for publishing and subscribing to game events.
    /// </summary>
    public class GameEventBus
    {
        private readonly Dictionary<string, Action<object>> _subscribers = new Dictionary<string, Action<object>>();

        /// <summary>
        /// Subscribe to an event by name.
        /// </summary>
        public void Subscribe(string eventName, Action<object> handler)
        {
            if (handler == null) return;
            if (!_subscribers.ContainsKey(eventName)) _subscribers[eventName] = handler;
            else _subscribers[eventName] += handler;
        }

        /// <summary>
        /// Unsubscribe from an event.
        /// </summary>
        public void Unsubscribe(string eventName, Action<object> handler)
        {
            if (_subscribers.ContainsKey(eventName)) _subscribers[eventName] -= handler;
        }

        /// <summary>
        /// Publish an event to all subscribers.
        /// </summary>
        public void Publish(string eventName, object data)
        {
            if (_subscribers.TryGetValue(eventName, out var handler))
            {
                try { handler?.Invoke(data); } catch (Exception ex) { UnityEngine.Debug.LogWarning($"EventBus publish error for {eventName}: {ex}"); }
            }
        }
    }
}
