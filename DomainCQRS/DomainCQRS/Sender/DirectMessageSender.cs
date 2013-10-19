using System;
using System.Collections.Generic;
using System.Text;
using DomainCQRS.Common;

namespace DomainCQRS
{
	/// <summary>
	/// Configures Domain CQRS to use the <see cref="DirectMessageSender"/>
	/// </summary>
	public static class DirectMessageSenderConfigure
	{
		public static IConfigure DirectMessageSender(this IConfigure configure)
		{
			configure.Registry
				.BuildInstancesOf<IMessageSender>()
				.TheDefaultIsConcreteType<DirectMessageSender>()
				.AsSingletons();
			return configure;
		}
	}

	/// <summary>
	/// Sends messages directory to the <see cref="IMessageReceiver"/>, effectively making it synnchronous.
	/// </summary>
	public class DirectMessageSender : IMessageSender
	{
		private readonly ILogger _logger;
		public ILogger Logger
		{
			get { return _logger; }
		}
		private readonly IMessageReceiver _receiver;
		public IMessageReceiver Receiver
		{
			get { return _receiver; }
		}

		public DirectMessageSender(ILogger logger, IMessageReceiver receiver)
		{
			if (null == logger)
			{
				throw new ArgumentNullException("logger");
			}
			if (null == receiver)
			{
				throw new ArgumentNullException("receiver");
			}

			_logger = logger;
			_receiver = receiver;
		}

		public IMessageSender Send(object message)
		{
			Receiver.Receive(message);
			return this;
		}
	}
}
