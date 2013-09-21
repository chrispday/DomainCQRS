using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DomainCQRS
{
	public static class Extensions
	{
		public static Thread Start(this ThreadStart method, string threadName)
		{
			Thread t = new Thread(method) { Name = threadName };
			t.Start();
			return t;
		}

		public static Thread Start<T>(this Action<T> method, string threadName, T par)
		{
			Thread t = new Thread(Start<T>) { Name = threadName };
			t.Start(new object[] { method, par });
			return t;
		}

		private static void Start<T>(object par)
		{
			object[] pars = (object[]) par;
			((Action<T>)pars[0])((T)pars[1]);
		}
	}
}
