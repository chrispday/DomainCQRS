using System;
using System.Collections.Generic;
using System.Text;
using DomainCQRS.Common;
using StructureMap;
using StructureMap.Configuration.DSL;

namespace DomainCQRS
{
	public interface IConfigure
	{
		IConfigure Synchrounous();

		Registry Registry { get; }
		IBuiltConfigure Build();
	}

	public interface IBuiltConfigure : IDisposable
	{
		IInstanceManager Container { get; }
		IMessageReceiver MessageReceiver { get; }
		IEventPublisher EventPublisher { get; }
		IEventStore EventStore { get; }
	}

	public class Configure : IConfigure, IBuiltConfigure
	{
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

		private IEventStoreProvider _eventStoreProvider;

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

		public static IConfigure With()
		{
			return new Configure(new Registry());
		}

		private IInstanceManager _container;
		public IInstanceManager Container
		{
			get { return _container; }
		}

		public IBuiltConfigure Build()
		{
			_container = Registry.BuildInstanceManager();
			
			_eventStoreProvider = Container.CreateInstance<IEventStoreProvider>().EnsureExists();
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

			if (Synchronous)
			{
				_eventPublisher.MessageReceiver = _messageReceiver;
				_eventPublisher.Synchronous = true;
			}

			return this;
		}

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

		public bool Synchronous { get; set; }
		public IConfigure Synchrounous()
		{
			Synchronous = true;
			return this;
		}
	}
}
