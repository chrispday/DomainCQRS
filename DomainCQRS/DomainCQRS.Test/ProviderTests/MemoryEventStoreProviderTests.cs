using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DomainCQRS.Common;
using DomainCQRS.Persister;

namespace DomainCQRS.Test
{
	[TestClass]
	public class MemoryEventStoreProviderTests : EventStoreProviderTestsBase
	{
		string BaseDirectory;

		protected override IEventPersister CreateProvider()
		{
			return new MemoryEventPersister(new DebugLogger(true));
		}

		protected override bool ExpectConcurrencyExceptionExceptionOnSaveOutOfOrder
		{
			get { return true; }
		}

		[TestInitialize]
		public void Init()
		{
		}

		[TestCleanup]
		public void Cleanup()
		{
		}

		protected override IConfigure RegisterProvider(IConfigure configure)
		{
			return configure.MemoryEventPersister();
		}
	}
}
