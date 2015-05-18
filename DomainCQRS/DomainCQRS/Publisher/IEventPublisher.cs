using System;
using System.Collections.Generic;

using System.Text;
using DomainCQRS.Common;

namespace DomainCQRS
{
	/// <summary>
	/// Publishes events from the <see cref="IEventStore"/>.
	/// Implements <see cref="IDisposable"/> so that the publishing resources can be cleaned up.
	/// </summary>
	public interface IEventPublisher : IDisposable
	{
		ILogger Logger { get; }
		IEventStore EventStore { get; }

		/// <summary>
		/// Adds a <typeparamref name="Subscriber"/> for all published events.
		/// </summary>
		/// <typeparam name="Subscriber">The subscriber type.</typeparam>
		/// <param name="subscriptionId">The subscription id used to keep track of what events have been published.
		/// Subscription position is persisted, so the id should be the same after re-starting publishing.</param>
		/// <returns>The <see cref="IEventPublisher"/>.</returns>
		IEventPublisher Subscribe<Subscriber>(Guid subscriptionId);
		/// <summary>
		/// Adds a <typeparamref name="Subscriber"/> for all published events.
		/// </summary>
		/// <typeparam name="Subscriber">The subscriber type.</typeparam>
		/// <param name="subscriptionId">The subscription id used to keep track of what events have been published.
		/// Subscription position is persisted, so the id should be the same after re-starting publishing.</param>
		/// <param name="subscriberReceiveMethodName">The name of the method to receive published events.</param>
		/// <returns>The <see cref="IEventPublisher"/>.</returns>
		IEventPublisher Subscribe<Subscriber>(Guid subscriptionId, string subscriberReceiveMethodName);
		/// <summary>
		/// Adds a <typeparamref name="Subscriber"/> for all published <typeparamref name="Event"/>s.
		/// </summary>
		/// <typeparam name="Subscriber">The subscriber type.</typeparam>
		/// <typeparam name="Event">The type of events the <typeparamref name="Subscriber"/> handles.</typeparam>
		/// <param name="subscriptionId">The subscription id used to keep track of what events have been published.
		/// Subscription position is persisted, so the id should be the same after re-starting publishing.</param>
		/// <returns>The <see cref="IEventPublisher"/>.</returns>
		IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId) where Event : class;
		/// <summary>
		/// Adds a <typeparamref name="Subscriber"/> for all published <typeparamref name="Event"/>s.
		/// </summary>
		/// <typeparam name="Subscriber">The subscriber type.</typeparam>
		/// <typeparam name="Event">The type of events the <typeparamref name="Subscriber"/> handles.</typeparam>
		/// <param name="subscriptionId">The subscription id used to keep track of what events have been published.
		/// Subscription position is persisted, so the id should be the same after re-starting publishing.</param>
		/// <param name="subscriberReceiveMethodName">The name of the method to receive published events.</param>
		/// <returns>The <see cref="IEventPublisher"/>.</returns>
		IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId, string subscriberReceiveMethodName) where Event : class;
		/// <summary>
		/// Adds a <typeparamref name="Subscriber"/> for all published events.
		/// </summary>
		/// <typeparam name="Subscriber">The subscriber type.</typeparam>
		/// <param name="subscriptionId">The subscription id used to keep track of what events have been published.
		/// Subscription position is persisted, so the id should be the same after re-starting publishing.</param>
		/// <param name="subscriber">The <typeparamref name="Subscriber"/> instance that should be used for this subscription.</param>
		/// <returns>The <see cref="IEventPublisher"/>.</returns>
		IEventPublisher Subscribe<Subscriber>(Guid subscriptionId, Subscriber subscriber);
		/// <summary>
		/// Adds a <typeparamref name="Subscriber"/> for all published events.
		/// </summary>
		/// <typeparam name="Subscriber">The subscriber type.</typeparam>
		/// <param name="subscriptionId">The subscription id used to keep track of what events have been published.
		/// Subscription position is persisted, so the id should be the same after re-starting publishing.</param>
		/// <param name="subscriber">The <typeparamref name="Subscriber"/> instance that should be used for this subscription.</param>
		/// <param name="subscriberReceiveMethodName">The name of the method to receive published events.</param>
		/// <returns>The <see cref="IEventPublisher"/>.</returns>
		IEventPublisher Subscribe<Subscriber>(Guid subscriptionId, Subscriber subscriber, string subscriberReceiveMethodName);
		/// <summary>
		/// Adds a <typeparamref name="Subscriber"/> for all published <typeparamref name="Event"/>s.
		/// </summary>
		/// <typeparam name="Subscriber">The subscriber type.</typeparam>
		/// <typeparam name="Event">The type of events the <typeparamref name="Subscriber"/> handles.</typeparam>
		/// <param name="subscriptionId">The subscription id used to keep track of what events have been published.
		/// Subscription position is persisted, so the id should be the same after re-starting publishing.</param>
		/// <param name="subscriber">The <typeparamref name="Subscriber"/> instance that should be used for this subscription.</param>
		/// <returns>The <see cref="IEventPublisher"/>.</returns>
		IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId, Subscriber subscriber) where Event : class;
		/// <summary>
		/// Adds a <typeparamref name="Subscriber"/> for all published <typeparamref name="Event"/>s.
		/// </summary>
		/// <typeparam name="Subscriber">The subscriber type.</typeparam>
		/// <typeparam name="Event">The type of events the <typeparamref name="Subscriber"/> handles.</typeparam>
		/// <param name="subscriptionId">The subscription id used to keep track of what events have been published.
		/// Subscription position is persisted, so the id should be the same after re-starting publishing.</param>
		/// <param name="subscriber">The <typeparamref name="Subscriber"/> instance that should be used for this subscription.</param>
		/// <param name="subscriberReceiveMethodName">The name of the method to receive published events.</param>
		/// <returns>The <see cref="IEventPublisher"/>.</returns>
		IEventPublisher Subscribe<Subscriber, Event>(Guid subscriptionId, Subscriber subscriber, string subscriberReceiveMethodName) where Event : class;

		/// <summary>
		/// Gets the instance of the subscriber for the subscription id.
		/// </summary>
		/// <param name="subscriptionId">The subscription id.</param>
		/// <returns>The instance for the subscription.</returns>
		object GetSubscriber(Guid subscriptionId);
		/// <summary>
		/// Gets the instance of the <typeparamref name="Subscriber"/> for the subscription id.
		/// </summary>
		/// <typeparam name="Subscriber">The subscriber type.</typeparam>
		/// <param name="subscriptionId">The subscription id.</param>
		/// <returns>The instance for the subscription.</returns>
		Subscriber GetSubscriber<Subscriber>(Guid subscriptionId) where Subscriber : class;
	}
}
