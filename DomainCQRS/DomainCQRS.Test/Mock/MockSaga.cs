using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DomainCQRS.Domain;

namespace DomainCQRS.Test.Mock
{
	public class MockSaga : AggregateRootBase, IHandlesCommand<MockSagaEvent>, IHandlesEvent<MockSagaEvent2>
	{
		public static AutoResetEvent Signal = new AutoResetEvent(false);
		public static int EventsHandled = 0;
		public static int SignalOnEventsHandled;

		public List<string> Messages = new List<string>();

		public void Apply(MockSagaEvent2 @event)
		{
			Messages.Add(@event.Message);
		}

		IEnumerable<IEvent> IHandlesCommand<MockSagaEvent>.Apply(MockSagaEvent command)
		{
			Messages.Add(command.Message);

			Interlocked.Increment(ref EventsHandled);
			if (EventsHandled >= SignalOnEventsHandled)
			{
				Signal.Set();
			}

			return new IEvent[] { new MockSagaEvent2() { Message = "Saga " + command.Message } };
		}
	}

	public class MockSagaAggregateRoot : AggregateRootBase, IHandlesCommand<MockSagaCommand>, IHandlesEvent<MockSagaEvent>
	{
		public Guid SagaId { get; set; }
		public List<string> Messages = new List<string>();

		public IEnumerable<IEvent> Apply(MockSagaCommand command)
		{
			if (Guid.Empty == SagaId)
			{
				SagaId = Guid.NewGuid();
			}
			Messages.Add(command.Message);

			return new IEvent[] { new MockSagaEvent() { AggregateRootId = SagaId, Message = command.Message } };
		}

		public void Apply(MockSagaEvent @event)
		{
			SagaId = @event.AggregateRootId;
			Messages.Add(@event.Message);
		}
	}

	[Serializable]
	public class MockSagaCommand : ICommand
	{
		public Guid AggregateRootId { get; set; }
		public string Message { get; set; }
	}

	[Serializable]
	public class MockSagaEvent : ICommand, IEvent
	{
		public Guid AggregateRootId { get; set; }
		public string Message { get; set; }

	}

	[Serializable]
	public class MockSagaEvent2 : IEvent
	{
		public string Message { get; set; }
		public Guid AggregateRootId { get; set; }
	}
}
