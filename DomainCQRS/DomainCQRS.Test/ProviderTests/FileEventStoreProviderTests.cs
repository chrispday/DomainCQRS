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
	public class FileEventStoreProviderTests : EventStoreProviderTestsBase
	{
		string BaseDirectory;

		protected override IEventPersister CreateProvider()
		{
			return new FileEventPersister(new DebugLogger(true), BaseDirectory, 1000, 8096);
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
			return configure.FileEventPersister(BaseDirectory);
		}
	}
}
