// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Diagnostics;
using System.IO;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using Serilog.Formatting.Json;

namespace Serilog.Sinks.Syslog
{
    /// <summary>
    /// Base class for formatters that output Serilog events in syslog formats
    /// </summary>
    /// <remarks>
    /// We purposely don't use Serilog's ITextFormatter to format syslog messages, so that users of this library
    /// can use their own ITextFormatter instances to control the format of the 'body' part of each message
    /// </remarks>
    public abstract class SyslogFormatterBase : ISyslogFormatter
    {
        private readonly Facility facility;
        private readonly ITextFormatter templateFormatter;
        protected static readonly string Host = Environment.MachineName.WithMaxLength(255);
        protected static readonly string ProcessId = Process.GetCurrentProcess().Id.ToString();
        protected static readonly string ProcessName = Process.GetCurrentProcess().ProcessName;

        protected SyslogFormatterBase(Facility facility, ITextFormatter templateFormatter)
        {
            this.facility = facility;
            this.templateFormatter = templateFormatter;
        }

        public abstract string FormatMessage(LogEvent logEvent);

        public int CalculatePriority(LogEventLevel level)
        {
            var severity = MapLogLevelToSeverity(level);
            return ((int)this.facility * 8) + (int)severity;
        }

        private static Severity MapLogLevelToSeverity(LogEventLevel logEventLevel)
        {
            switch (logEventLevel)
            {
                case LogEventLevel.Debug: return Severity.Debug;
                case LogEventLevel.Error: return Severity.Error;
                case LogEventLevel.Fatal: return Severity.Emergency;
                case LogEventLevel.Information: return Severity.Informational;
                case LogEventLevel.Warning: return Severity.Warning;
                default: return Severity.Notice;
            }
        }

        protected string RenderMessage(LogEvent logEvent)
        {
            if (templateFormatter == null)
            {
                return logEvent.RenderMessage();
            }

            using (var sw = new StringWriter())
            {
                if (templateFormatter is JsonFormatter)
                {
                    sw.Write("@cee:");
                }
                this.templateFormatter.Format(logEvent, sw);
              
                return sw.ToString();
            }

           
        }
    }
}
