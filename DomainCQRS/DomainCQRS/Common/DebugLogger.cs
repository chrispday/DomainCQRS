using System;
using System.Collections.Generic;
using System.Diagnostics;

using System.Text;
using DomainCQRS.Common;
using StructureMap.Configuration.DSL;

namespace DomainCQRS
{
	public static class DebugLoggerConfigure
	{
		public static IConfigure DebugLogger(this IConfigure configure) { return configure.DebugLogger(false); }
		public static IConfigure DebugLogger(this IConfigure configure, bool logVerbose)
		{
			configure.Registry
				.BuildInstancesOf<ILogger>()
				.TheDefaultIs(Registry.Instance<ILogger>()
					.UsingConcreteType<DebugLogger>()
					.WithProperty("LogVerbose").EqualTo(logVerbose))
				.AsSingletons();
			return configure;
		}
	}
}

namespace DomainCQRS.Common
{
	public class DebugLogger : ILogger
	{
		public bool LogVerbose = false;

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
