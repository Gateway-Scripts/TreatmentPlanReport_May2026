using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TreatmentPlanReport.Helpers
{
    public class EventHelper
    {
        // Dictionary to hold event subscribers with event name and event data type
        private readonly Dictionary<string, List<Delegate>> _subscribers = new Dictionary<string, List<Delegate>>();

        /// <summary>
        /// Subscribes a handler to an event.
        /// </summary>
        /// <typeparam name="T">The type of the event data.</typeparam>
        /// <param name="eventName">The name of the event to subscribe to.</param>
        /// <param name="handler">The handler to invoke when the event is published.</param>
        public void Subscribe<T>(string eventName, Action<T> handler)
        {
            if (!_subscribers.ContainsKey(eventName))
            {
                _subscribers[eventName] = new List<Delegate>();
            }
            _subscribers[eventName].Add(handler);
        }

        /// <summary>
        /// Unsubscribes a handler from an event.
        /// </summary>
        /// <typeparam name="T">The type of the event data.</typeparam>
        /// <param name="eventName">The name of the event to unsubscribe from.</param>
        /// <param name="handler">The handler to remove.</param>
        public void Unsubscribe<T>(string eventName, Action<T> handler)
        {
            if (_subscribers.ContainsKey(eventName))
            {
                _subscribers[eventName].Remove(handler);
                if (_subscribers[eventName].Count == 0)
                {
                    _subscribers.Remove(eventName);
                }
            }
        }

        /// <summary>
        /// Publishes an event to all its subscribers.
        /// </summary>
        /// <typeparam name="T">The type of the event data.</typeparam>
        /// <param name="eventName">The name of the event to publish.</param>
        /// <param name="eventData">The data associated with the event.</param>
        public void Publish<T>(string eventName, T eventData)
        {
            if (_subscribers.ContainsKey(eventName))
            {
                // Create a copy of the subscribers list to prevent modification during iteration
                var subscribersCopy = new List<Delegate>(_subscribers[eventName]);
                foreach (var handler in subscribersCopy)
                {
                    if (handler is Action<T> typedHandler)
                    {
                        typedHandler.Invoke(eventData);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Handler for event '{eventName}' is not of type Action<{typeof(T).Name}>.");
                    }
                }
            }
        }
    }

}
