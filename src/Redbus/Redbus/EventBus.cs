using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Redbus.Configuration;
using Redbus.Events;
using Redbus.Interfaces;

namespace Redbus
{
    /// <summary>
    /// Implements <see cref="IEventBus"/>.
    /// </summary>
    public class EventBus : IEventBus
    {
        private const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    
        private readonly IEventBusConfiguration _eventBusConfiguration;
        public EventBus(IEventBusConfiguration configuration = null)
        {
            _eventBusConfiguration = configuration ?? EventBusConfiguration.Default;
            _subscriptions = new Dictionary<Type, List<ISubscription>>();
        }

        /// <summary>
        /// Subscribes to the specified event type with the specified action
        /// </summary>
        /// <typeparam name="TEventBase">The type of event</typeparam>
        /// <param name="action">The Action to invoke when an event of this type is published</param>
        /// <returns>A <see cref="SubscriptionToken"/> to be used when calling <see cref="Unsubscribe"/></returns>
        public SubscriptionToken Subscribe<TEventBase>(Action<TEventBase> action) where TEventBase : EventBase
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            lock (SubscriptionsLock)
            {
                if (!_subscriptions.ContainsKey(typeof(TEventBase)))
                    _subscriptions.Add(typeof(TEventBase), new List<ISubscription>());

                var token = new SubscriptionToken(typeof(TEventBase));
                _subscriptions[typeof(TEventBase)].Add(new Subscription<TEventBase>(action, token));
                return token;
            }
        }

        /// <summary>
        /// Subscribes to all event handlers in the specified type
        /// </summary>
        /// <param name="type">The type with our event handlers</param>
        /// <returns>A list of <see cref="SubscriptionToken"/> to be used when calling <see cref="Unsubscribe"/></returns>
        public List<SubscriptionToken> SubscribeAll(Type type, object instance)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (type.GetCustomAttribute<EventHandlerAttribute>() == null)
                throw new ArgumentException(nameof(type));

            lock (SubscriptionsLock)
            {
                var tokens = new List<SubscriptionToken>();

                foreach (var method in type.GetMethods(flags))
                {
                    if (method.GetCustomAttribute<EventHandlerAttribute>() != null)
                    {
                        if (method.GetParameters().Length == 1)
                        {
                            var @event = method.GetParameters()[0];
                            var eventType = @event.ParameterType;
                        
                            if (!_subscriptions.ContainsKey(eventType))
                                _subscriptions.Add(eventType, new List<ISubscription>());

                            var token = new SubscriptionToken(eventType);

                            var subscriptionType = typeof(Subscription<>);
                            var subscriptionGenericType = subscriptionType.MakeGenericType(new Type[] { eventType });

                            var parameters = new [] { Expression.Parameter(eventType, @event.Name) };
                            var @delegate = Expression.Lambda(Expression.Call(Expression.Constant(instance), method, parameters), parameters).Compile();

                            var subscription = Activator.CreateInstance(subscriptionGenericType, new object[] { @delegate, token }) as ISubscription;
                            
                            _subscriptions[eventType].Add(subscription);

                            tokens.Add(token);
                        }
                        else
                        {
                            throw new InvalidEventHandlerException(method);
                        }
                    }
                }

                return tokens;
            }
        }

        /// <summary>
        /// Unsubscribe from the Event type related to the specified <see cref="SubscriptionToken"/>
        /// </summary>
        /// <param name="token">The <see cref="SubscriptionToken"/> received from calling the Subscribe method</param>
        public void Unsubscribe(SubscriptionToken token)
        {
            if (token == null)
                throw new ArgumentNullException(nameof(token));

            lock (SubscriptionsLock)
            {
                if (_subscriptions.ContainsKey(token.EventItemType))
                {
                    var allSubscriptions = _subscriptions[token.EventItemType];
                    var subscriptionToRemove = allSubscriptions.FirstOrDefault(x => x.SubscriptionToken.Token == token.Token);
                    if (subscriptionToRemove != null)
                        _subscriptions[token.EventItemType].Remove(subscriptionToRemove);
                }
            }
        }

        /// <summary>
        /// Publishes the specified event to any subscribers for the <see cref="TEventBase"/> event type
        /// </summary>
        /// <typeparam name="TEventBase">The type of event</typeparam>
        /// <param name="eventItem">Event to publish</param>
        public void Publish<TEventBase>(TEventBase eventItem) where TEventBase : EventBase
        {
            if (eventItem == null)
                throw new ArgumentNullException(nameof(eventItem));

            var allSubscriptions = new List<ISubscription>();
            lock (SubscriptionsLock)
            {
                if (_subscriptions.ContainsKey(typeof(TEventBase)))
                    allSubscriptions = _subscriptions[typeof(TEventBase)].ToList();
            }

            for (var index = 0; index < allSubscriptions.Count; index++)
            {
                var subscription = allSubscriptions[index];
                try
                {
                    subscription.Publish(eventItem);
                }
                catch (Exception)
                {
                    if (_eventBusConfiguration.ThrowSubscriberException)
                        throw;
                }
            }
        }

        /// <summary>
        /// Publishes the specified event to any subscribers for the <see cref="TEventBase"/> event type asychronously
        /// </summary>
        /// <remarks> This is a wrapper call around the synchronous  method as this method is naturally synchronous (CPU Bound) </remarks>
        /// <typeparam name="TEventBase">The type of event</typeparam>
        /// <param name="eventItem">Event to publish</param>
        public void PublishAsync<TEventBase>(TEventBase eventItem) where TEventBase : EventBase
        {
            PublishAsyncInternal(eventItem, null);
        }

        /// <summary>
        /// Publishes the specified event to any subscribers for the <see cref="TEventBase"/> event type asychronously
        /// </summary>
        /// <remarks> This is a wrapper call around the synchronous  method as this method is naturally synchronous (CPU Bound) </remarks>
        /// <typeparam name="TEventBase">The type of event</typeparam>
        /// <param name="eventItem">Event to publish</param>
        /// <param name="callback"><see cref="AsyncCallback"/> that is called on completion</param>
        public void PublishAsync<TEventBase>(TEventBase eventItem, AsyncCallback callback) where TEventBase : EventBase
        {
            PublishAsyncInternal(eventItem, callback);
        }

        #region PRIVATE METHODS
        private void PublishAsyncInternal<TEventBase>(TEventBase eventItem, AsyncCallback callback) where TEventBase : EventBase
        {
            Task<bool> publishTask = new Task<bool>(() =>
            {
                Publish(eventItem);
                return true;
            });
            publishTask.Start();
            if (callback == null)
                return;

            var tcs = new TaskCompletionSource<bool>();
            publishTask.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    tcs.TrySetException(t.Exception.InnerExceptions);
                else if (t.IsCanceled)
                    tcs.TrySetCanceled();
                else
                    tcs.TrySetResult(t.Result);
                callback?.Invoke(tcs.Task);
            }, TaskScheduler.Default);
        }

        #endregion

        private readonly Dictionary<Type, List<ISubscription>> _subscriptions;
        private static readonly object SubscriptionsLock = new object();
    }
}
