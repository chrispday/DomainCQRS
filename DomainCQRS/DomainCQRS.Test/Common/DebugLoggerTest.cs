using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using DomainCQRS.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StoryQ;

namespace DomainCQRS.Test.Common
{
	[TestClass]
	public class DebugLoggerTest
	{
		[TestMethod]
		public void DebugLogging()
		{
			new Story("Debug Logging")
				 .InOrderTo("log to the debug output")
				 .AsA("Programmer")
				 .IWant("to create a logger that outputs to the debug window")

							.WithScenario("Log Error")
								 .Given(ADebugLogger)
								 .When(AnErrorIsLogged)
								 .Then(ItShouldBeInTheDebugOuputAsAnError)
									  .And(ItContainsTheFormattedMessage, errorGuid, new object[] { errorGuid, 1, "error string" })

							.WithScenario("Log Warning")
								 .Given(ADebugLogger)
								 .When(AWarningIsLogged)
								 .Then(ItShouldBeInTheDebugOuputAsAWarning)
									  .And(ItContainsTheFormattedMessage, warningGuid, new object[] { warningGuid, 2, "warning string" })

							.WithScenario("Log Information")
								 .Given(ADebugLogger)
								 .When(InformationIsLogged)
								 .Then(ItShouldBeInTheDebugOuputAsInformation)
									  .And(ItContainsTheFormattedMessage, informationGuid, new object[] { informationGuid, 3, "information string" })

							.WithScenario("Log Verbose On")
								 .Given(ADebugLoggerWithVerboseLoggingOn)
								 .When(VerboseIsLogged)
								 .Then(ItShouldBeInTheDebugOuputAsVerbose)
									  .And(ItContainsTheFormattedMessage, verboseGuid, new object[] { verboseGuid, 4, "verbose string" })

							.WithScenario("Log Verbose Off")
								 .Given(ADebugLoggerWithVerboseLoggingOff)
								 .When(VerboseIsLogged)
								 .Then(ItShouldNotBeInTheDebugOuput)
				 .Execute();
		}

		public class DebugListener : TraceListener
		{
			public List<string> logs = new List<string>();

			public override void Write(string message)
			{
				logs.Add(message);
			}

			public override void WriteLine(string message)
			{
				logs.Add(message);
			}
		}

		DebugLogger logger;
		DebugListener listener;
		private void ADebugLogger()
		{
			if (null == logger)
			{
				logger = new DebugLogger(false);
			}
			if (null == listener)
			{
				Debug.Listeners.Add(listener = new DebugListener());
			}

		}

		Guid errorGuid = Guid.NewGuid();
		private void AnErrorIsLogged()
		{
			logger.Error("{0} {1} {2}", errorGuid, 1, "error string");
		}

		private void ItShouldBeInTheDebugOuputAsAnError()
		{
			Assert.IsTrue(listener.logs.Any(l => l.Contains(errorGuid.ToString()) && l.StartsWith("Error")));
		}

		private void ItContainsTheFormattedMessage(Guid g, object[] o)
		{
			Assert.IsTrue(listener.logs.Any(l => l.Contains(g.ToString()) && o.ToList().TrueForAll(ob => l.Contains(ob.ToString()))));
		}

		Guid warningGuid = Guid.NewGuid();
		private void AWarningIsLogged()
		{
			logger.Warning("{0} {1} {2}", warningGuid, 2, "warning string");
		}

		private void ItShouldBeInTheDebugOuputAsAWarning()
		{
			Assert.IsTrue(listener.logs.Any(l => l.Contains(warningGuid.ToString()) && l.StartsWith("Warning")));
		}

		Guid informationGuid = Guid.NewGuid();
		private void InformationIsLogged()
		{
			logger.Information("{0} {1} {2}", informationGuid, 3, "information string");
		}

		private void ItShouldBeInTheDebugOuputAsInformation()
		{
			Assert.IsTrue(listener.logs.Any(l => l.Contains(informationGuid.ToString()) && l.StartsWith("Information")));
		}

		private void ADebugLoggerWithVerboseLoggingOn()
		{
			logger = new DebugLogger(true);
		}

		Guid verboseGuid = Guid.NewGuid();
		private void VerboseIsLogged()
		{
			logger.Verbose("{0} {1} {2}", verboseGuid, 4, "verbose string");
		}

		private void ItShouldBeInTheDebugOuputAsVerbose()
		{
			Assert.IsTrue(listener.logs.Any(l => l.Contains(verboseGuid.ToString()) && l.StartsWith("Verbose")));
		}

		private void ADebugLoggerWithVerboseLoggingOff()
		{
			logger = new DebugLogger(false);
			listener.logs.Clear();
		}

		private void ItShouldNotBeInTheDebugOuput()
		{
			Assert.IsFalse(listener.logs.Any(l => l.Contains(verboseGuid.ToString())));
		}
	}
}
