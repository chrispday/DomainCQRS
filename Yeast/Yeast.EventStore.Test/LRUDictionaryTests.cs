using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Yeast.EventStore.Common;

namespace Yeast.EventStore.Test
{
	[TestClass]
	public class LRUDictionaryTests
	{
		[TestMethod]
		public void LRUDictionary_LRU()
		{
			var rand = new Random();

			var dictionary = new LRUDictionary<int, int>(10);

			KeyValueRemovedArgs<int, int> x = null;
			dictionary.Removed += (s, e) => { x = e; };

			dictionary[1] = 1;
			dictionary[2] = 1;
			dictionary[3] = 1;
			dictionary[4] = 1;
			dictionary[5] = 1;
			dictionary[6] = 1;
			dictionary[7] = 1;
			dictionary[8] = 1;
			dictionary[9] = 1;
			dictionary[10] = 1;
			dictionary[11] = 1;

			Assert.AreEqual(2, x.Key);
		}

		[TestMethod]
		public void LRUDictionary_LRU_AfterUpdate()
		{
			var rand = new Random();

			var dictionary = new LRUDictionary<int, int>(10);

			KeyValueRemovedArgs<int, int> x = null;
			dictionary.Removed += (s, e) => { x = e; };

			dictionary[1] = 1;
			dictionary[2] = 1;
			dictionary[3] = 1;
			dictionary[4] = 1;
			dictionary[5] = 1;
			dictionary[6] = 1;
			dictionary[7] = 1;
			dictionary[8] = 1;
			dictionary[9] = 1;
			dictionary[10] = 1;
			dictionary[1] = 1;
			dictionary[11] = 1;

			Assert.AreEqual(3, x.Key);
		}

		[TestMethod]
		public void LRUDictionary_LRU_AfterUpdateInMiddle()
		{
			var rand = new Random();

			var dictionary = new LRUDictionary<int, int>(10);

			KeyValueRemovedArgs<int, int> x = null;
			dictionary.Removed += (s, e) => { x = e; };

			dictionary[1] = 1;
			dictionary[2] = 1;
			dictionary[3] = 1;
			dictionary[4] = 1;
			dictionary[5] = 1;
			dictionary[6] = 1;
			dictionary[7] = 1;
			dictionary[8] = 1;
			dictionary[9] = 1;
			dictionary[10] = 1;
			dictionary[5] = 1;
			dictionary[11] = 1;

			Assert.AreEqual(2, x.Key);
		}

		[TestMethod]
		public void LRUDictionary_LRU_AfterRemoval()
		{
			var rand = new Random();

			var dictionary = new LRUDictionary<int, int>(10);

			KeyValueRemovedArgs<int, int> x = null;
			dictionary.Removed += (s, e) => { x = e; };

			dictionary[1] = 1;
			dictionary[2] = 1;
			dictionary[3] = 1;
			dictionary[4] = 1;
			dictionary[5] = 1;
			dictionary[6] = 1;
			dictionary[7] = 1;
			dictionary[8] = 1;
			dictionary[9] = 1;
			dictionary[10] = 1;
			dictionary.Remove(1);

			Assert.AreEqual(1, x.Key);
	
			dictionary[11] = 1;
			dictionary[12] = 1;

			Assert.AreEqual(3, x.Key);
		}

		[TestMethod]
		public void LRUDictionary_Load()
		{
			var rand = new Random();

			var dictionary = new LRUDictionary<int, DateTime>(100);
			//dictionary.Removed += (s, e) => { Debug.WriteLine("{0} => {1}", e.Key, e.Value); };

			var amount = 1;

			Parallel.ForEach(Enumerable.Range(1, amount), new ParallelOptions() { MaxDegreeOfParallelism = 8 }, i =>
				{
					dictionary[rand.Next(120)] = DateTime.Now;
				});
		}
	}
}
