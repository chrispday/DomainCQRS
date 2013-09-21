﻿using System;
using System.Collections.Generic;

using System.Text;

namespace DomainCQRS.Common
{
	public interface ILogger
	{
		void Verbose(string format, params object[] pars);
		void Information(string format, params object[] pars);
		void Warning(string format, params object[] pars);
		void Error(string format, params object[] pars);
	}
}