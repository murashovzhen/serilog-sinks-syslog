// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License, 
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using Serilog.Core;
using Serilog.Events;

namespace Serilog.Sinks.Syslog
{
    /// <summary>
    /// Sink that writes events to the local syslog service on Linux systems
    /// </summary>
    public class SyslogLocalSink : ILogEventSink, IDisposable
    {
        private readonly ISyslogFormatter _formatter;
        private readonly LocalSyslogService _syslogService;
        private static readonly object Sync = new object();
        private bool _disposed;

        public SyslogLocalSink(ISyslogFormatter formatter, LocalSyslogService syslogService)
        {
            this._formatter = formatter;
            this._syslogService = syslogService;

            this._syslogService.Open();
        }

        /// <summary>
        /// Emit the provided log event to the sink
        /// </summary>
        /// <param name="logEvent">The log event to send to the local syslog service</param>
        public void Emit(LogEvent logEvent)
        {
            if (logEvent == null)
                throw new ArgumentNullException(nameof(logEvent));

            lock (Sync)
            {
                if (this._disposed)
                    throw new ObjectDisposedException("The local syslog socket has been closed");

                var priority = this._formatter.CalculatePriority(logEvent.Level);
                var message = this._formatter.FormatMessage(logEvent);
                this._syslogService.WriteLog(priority, message);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            lock (Sync)
            {
                if (this._disposed)
                    return;

                this._syslogService.Close();
                this._disposed = true;
            }
        }
    }
}
