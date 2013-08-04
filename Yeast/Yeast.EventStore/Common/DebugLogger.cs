using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Yeast.EventStore.Common;

namespace Yeast.EventStore
{
	public static class DebugLoggerConfigure
	{
		public static IConfigure DebugLogger(this IConfigure configure) { return configure.DebugLogger(false); }
		public static IConfigure DebugLogger(this IConfigure configure, bool logVerbose)
		{
			var c = configure as Configure;
			c.Logger = new DebugLogger() { LogVerbose = logVerbose };
			return configure;
		}
	}
}

namespace Yeast.EventStore.Common
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
