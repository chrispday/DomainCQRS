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
	public class PartitionedFileEventStoreProviderTests : EventStoreProviderTestsBase
	{
		string BaseDirectory;

		protected override IEventPersister CreateProvider()
		{
			System.Diagnostics.Debug.WriteLine("Create Provider " + BaseDirectory);
			return new PartitionedFileEventPersister(new DebugLogger(true), BaseDirectory, 8, 1000, 8096);
		}

		protected override bool ExpectConcurrencyExceptionExceptionOnSaveOutOfOrder
		{
			get { return true; }
		}

		[TestInitialize]
		public void Init()
		{
			BaseDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
		}

		[TestCleanup]
		public void Cleanup()
		{
			if (Directory.Exists(BaseDirectory))
			{
				try
				{
					Directory.Delete(BaseDirectory, true);
				}
				catch { }
			}
		}

		protected override IConfigure RegisterProvider(IConfigure configure)
		{
			System.Diagnostics.Debug.WriteLine("Register Provider " + BaseDirectory);
			return configure.PartitionedFileEventPersister(8, BaseDirectory);
		}
	}
}
