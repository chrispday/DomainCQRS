using System;
using System.Collections.Generic;
using System.Text;
using DomainCQRS.Common;
using StructureMap;
using StructureMap.Configuration.DSL;

namespace DomainCQRS
{
	/// <summary>
	/// For configuring Domain CQRS
	/// </summary>
	public interface IConfigure
	{
		/// <summary>
		/// Get the StructureMap registry.
		/// </summary>
		Registry Registry { get; }
		/// <summary>
		/// Build up Domain CQRS components as currently configured.
		/// </summary>
		/// <returns>The built Domain CQRS components.</returns>
		IBuiltConfigure Build();
	}

	/// <summary>
	/// For configuring of Domain CQRS components post-build.
	/// </summary>
	public interface IBuiltConfigure : IDisposable
	{
		/// <summary>
		/// Get the StructureMap container.
		/// </summary>
		IInstanceManager Container { get; }
		/// <summary>
		/// Get the <see cref="IMessageReceiver"/>,if built.
		/// </summary>
		IMessageReceiver MessageReceiver { get; }
		/// <summary>
		/// Get the <see cref="IEventPublisher"/>,if built.
		/// </summary>
		IEventPublisher EventPublisher { get; }
		/// <summary>
		/// Get the <see cref="IEventStore"/>,if built.
		/// </summary>
		IEventStore EventStore { get; }
	}

	/// <summary>
	/// Halds the configuration for Domain CQRS components.
	/// </summary>
	public class Configure : IConfigure, IBuiltConfigure
	{
		/// <summary>
		/// Creates a new configuration.
		/// </summary>
		/// <param name="registry">The StructureMap Registry to use for this configuration.</param>
		public Configure(Registry registry)
		{
			if (null == registry)
			{
				throw new ArgumentNullException("registry");
			}

			_registry = registry;
		}

		private readonly Registry _registry;
		public Registry Registry { get { return _registry; } }

		private IEventPersister _eventStoreProvider;

		private IEventStore _eventStore;
		public IEventStore EventStore
		{
			get { return _eventStore; }
		}

		private IMessageReceiver _messageReceiver;
		public IMessageReceiver MessageReceiver
		{
			get { return _messageReceiver; }
		}

		private IEventPublisher _eventPublisher;
		public IEventPublisher EventPublisher
		{
			get { return _eventPublisher; }
		}

		/// <summary>
		/// Starts a new configuration.
		/// </summary>
		/// <returns>The configuration to use.</returns>
		public static IConfigure With()
		{
			return new Configure(new Registry());
		}

		private IInstanceManager _container;
		public IInstanceManager Container
		{
			get { return _container; }
		}

		/// <summary>
		/// Builds up Domain CQRS components as configured.
		/// </summary>
		/// <returns>The built configuration for more configuring.</returns>
		public IBuiltConfigure Build()
		{
			_container = Registry.BuildInstanceManager();
			
			_eventStoreProvider = Container.CreateInstance<IEventPersister>().EnsureExists();
			_eventStore = Container.CreateInstance<IEventStore>();
			try
			{
				_messageReceiver = Container.CreateInstance<IMessageReceiver>();
			}
			catch (Exception ex)
			{
				Container.CreateInstance<ILogger>().Warning(ex.ToString());
			}
			try
			{
				_eventPublisher = Container.CreateInstance<IEventPublisher>();
			}
			catch (Exception ex)
			{
				Container.CreateInstance<ILogger>().Warning(ex.ToString());
			}

			return this;
		}

		/// <summary>
		/// Disposes components
		/// </summary>
		public void Dispose()
		{
			if (null != _eventPublisher)
			{
				_eventPublisher.Dispose();
			}
			if (null != _eventStoreProvider)
			{
				_eventStoreProvider.Dispose();
			}
		}
	}
}
