using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;

namespace DomainCQRS
{
	/// <summary>
	/// Contains utility extensions
	/// </summary>
	public static class Extensions
	{
		/// <summary>
		/// Starts a thread.
		/// </summary>
		/// <param name="method">The method to call on the new thread.</param>
		/// <param name="threadName">The name of the thread.</param>
		/// <returns>The started <see cref="Thread"/></returns>
		public static Thread Start(this ThreadStart method, string threadName)
		{
			Thread t = new Thread(method) { Name = threadName };
			t.Start();
			return t;
		}

		/// <summary>
		/// Starts a new thread calling the <see cref="Action[T]"/>
		/// </summary>
		/// <typeparam name="T">Parameter to the <paramref name="method"/></typeparam>
		/// <param name="method">The <see cref="Action[T]"/> to start on the new thread.</param>
		/// <param name="threadName">The name of the thread.</param>
		/// <param name="par">The parameter to pass into the <see cref="Action[T]"/></param>
		/// <returns>The started <see cref="Thread"/></returns>
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

		/// <summary>
		/// Get's the <see cref="MethodInfo"/>
		/// </summary>
		/// <param name="type">The <see cref="Type"/> to search.</param>
		/// <param name="methodName">The name of the method to find.</param>
		/// <param name="types">The <see cref="Type"/>s of the methods parameters</param>
		/// <param name="checkForExplicit">Check for methods that match that have been implemented explicitly using interfaces.</param>
		/// <returns>The <see cref="MethodInfo"/> if found, otherwise null.</returns>
		public static MethodInfo GetMethod(this Type type, string methodName, Type[] types, bool checkForExplicit)
		{
			var method = type.GetMethod(methodName, types);

			if (checkForExplicit
				&& null == method)
			{
				foreach (var iface in type.GetInterfaces())
				{
					method = iface.GetMethod(methodName, types);
					if (null != method)
					{
						break;
					}
				}
			}

			return method;
		}

		/// <summary>
		/// LINQ-a-like Take&lt;>.
		/// </summary>
		/// <typeparam name="T">The type of the items.</typeparam>
		/// <param name="items">The enumerable to take from.</param>
		/// <param name="amount">The maximum number of items to take.</param>
		/// <returns><see cref="IEnumerable[T]"/></returns>
		public static IEnumerable<T> Take<T>(this IEnumerable<T> items, int amount)
		{
			foreach (var item in items)
			{
				yield return item;

				if (0 >= --amount)
				{
					break;
				}
			}
		}

		/// <summary>
		/// LINQ-a-like Func&lt;>.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <typeparam name="R"></typeparam>
		/// <param name="t"></param>
		/// <returns></returns>
		public delegate R Func<T, R>(T t);

		/// <summary>
		/// LINQ-a-like Select&lt;>
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <typeparam name="R"></typeparam>
		/// <param name="items"></param>
		/// <param name="func"></param>
		/// <returns></returns>
		public static IEnumerable<R> Select<T,R>(this IEnumerable<T> items, Func<T,R> func)
		{
			foreach (var item in items)
			{
				yield return func(item);
			}
		}
	}
}
