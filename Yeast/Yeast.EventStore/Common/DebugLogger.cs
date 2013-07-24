using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Yeast.EventStore.Common
{
	public static class DebugLoggerConfigure
	{
		public static IConfigure DebugLogger(this IConfigure configure)
		{
			(configure as Configure).Logger = new DebugLogger();
			return configure;
		}
	}

	public class DebugLogger : ILogger
	{
		public void Verbose(string format, params object[] pars)
		{
			//Debug.WriteLine(string.Format("Verbose\t" + format, pars));
		}

		public void Information(string format, params object[] pars)
		{
			Debug.WriteLine(string.Format("Information\t" + format, pars));
		}

		public void Warning(string format, params object[] pars)
		{
			Debug.WriteLine(string.Format("Warning\t" + format, pars));
		}

		public void Error(string format, params object[] pars)
		{
			Debug.WriteLine(string.Format("Error\t" + format, pars));
		}
	}
}
