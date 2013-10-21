using System;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS.Common
{
	/// <summary>
	/// Generic logger interface.
	/// </summary>
	public interface ILogger
	{
		/// <summary>
		/// Log a verbose message.
		/// </summary>
		/// <param name="format">The format string for the message.</param>
		/// <param name="pars">The parameters for the format string.</param>
		void Verbose(string format, params object[] pars);
		/// <summary>
		/// Log an information message.
		/// </summary>
		/// <param name="format">The format string for the message.</param>
		/// <param name="pars">The parameters for the format string.</param>
		void Information(string format, params object[] pars);
		/// <summary>
		/// Log a warning message.
		/// </summary>
		/// <param name="format">The format string for the message.</param>
		/// <param name="pars">The parameters for the format string.</param>
		void Warning(string format, params object[] pars);
		/// <summary>
		/// Log an error message.
		/// </summary>
		/// <param name="format">The format string for the message.</param>
		/// <param name="pars">The parameters for the format string.</param>
		void Error(string format, params object[] pars);
	}
}
