using System;
using System.Collections.Generic;
using System.Diagnostics;

using System.Text;
using DomainCQRS.Common;
using StructureMap.Configuration.DSL;

namespace DomainCQRS
{
	/// <summary>
	/// Configures Domain CQRS to use the <see cref="DebugLogger"/>
	/// </summary>
	public static class DebugLoggerConfigure
	{
		/// <summary>
		/// Configures Domain CQRS to use the <see cref="DebugLogger"/>
		/// </summary>
		/// <param name="configure">The <see cref="IConfigure"/></param>
		/// <returns>The <see cref="IConfigure"/></returns>
		public static IConfigure DebugLogger(this IConfigure configure) { return configure.DebugLogger(false); }
		/// <summary>
		/// Configures Domain CQRS to use the <see cref="DebugLogger"/>
		/// </summary>
		/// <param name="configure">The <see cref="IConfigure"/></param>
		/// <param name="logVerbose">If Verbose messages should be logged.</param>
		/// <returns>The <see cref="IConfigure"/></returns>
		public static IConfigure DebugLogger(this IConfigure configure, bool logVerbose)
		{
			configure.Registry
				.BuildInstancesOf<ILogger>()
				.TheDefaultIs(Registry.Instance<ILogger>()
					.UsingConcreteType<DebugLogger>()
					.WithProperty("logVerbose").EqualTo(logVerbose))
				.AsSingletons();
			return configure;
		}
	}
}

namespace DomainCQRS.Common
{
	/// <summary>
	/// Logs to the Debug window/console
	/// </summary>
	public class DebugLogger : ILogger
	{
		/// <summary>
		/// If Verbose messages should be logged.
		/// </summary>
		public bool LogVerbose = false;

		/// <summary>
		/// Create a <see cref="DebugLogger"/>
		/// </summary>
		/// <param name="logVerbose">If Verbose messages should be logged.</param>
		public DebugLogger(bool logVerbose)
		{
			LogVerbose = logVerbose;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="format"></param>
		/// <param name="pars"></param>
		public void Verbose(string format, params object[] pars)
		{
			if (LogVerbose)
			{
				Debug.WriteLine(string.Format("Verbose\t" + (format ?? ""), pars));
			}
		}

		public void Information(string format, params object[] pars)
		{
			Debug.WriteLine(string.Format("Information\t" + (format ?? ""), pars));
		}

		public void Warning(string format, params object[] pars)
		{
			Debug.WriteLine(string.Format("Warning\t" + (format ?? ""), pars));
		}

		public void Error(string format, params object[] pars)
		{
			Debug.WriteLine(string.Format("Error\t" + (format ?? ""), pars));
		}
	}
}
