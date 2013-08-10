using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Yeast.EventStore.Domain;

namespace Yeast.EventStore.Test.Mock
{
	public class MockSaga : AggregateRootBase, IHandlesCommand<MockSagaEvent>, IHandlesEvent<MockSagaEvent2>
	{
		public List<string> Messages = new List<string>();

		public IEnumerable Apply(MockSagaEvent command)
		{
			Messages.Add(command.Message);

			return new object[] { new MockSagaEvent2() { Message = "Saga " + command.Message } };
		}

		public void Apply(MockSagaEvent2 @event)
		{
			Messages.Add(@event.Message);
		}
	}

	public class MockSagaAggregateRoot : AggregateRootBase, IHandlesCommand<MockSagaCommand>, IHandlesEvent<MockSagaEvent>
	{
		public Guid SagaId { get; set; }
		public List<string> Messages = new List<string>();

		public IEnumerable Apply(MockSagaCommand command)
		{
			if (Guid.Empty == SagaId)
			{
				SagaId = Guid.NewGuid();
			}
			Messages.Add(command.Message);

			return new object[] { new MockSagaEvent() { SagaId = SagaId, Message = command.Message } };
		}

		public void Apply(MockSagaEvent @event)
		{
			SagaId = @event.SagaId;
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
	public class MockSagaEvent
	{
		public Guid SagaId { get; set; }
		public string Message { get; set; }

	}

	[Serializable]
	public class MockSagaEvent2
	{
		public string Message { get; set; }
	}
}
