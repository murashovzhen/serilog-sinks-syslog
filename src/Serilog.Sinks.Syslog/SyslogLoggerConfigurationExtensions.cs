// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Serilog.Configuration;
using Serilog.Formatting;
using Serilog.Formatting.Display;
using Serilog.Formatting.Json;
using Serilog.Sinks.Settings;
using Serilog.Sinks.Syslog;
 
namespace Serilog
{
    /// <summary>
    /// Extends Serilog configuration to write events to a remote syslog service, or to the local syslog
    /// service on Linux systems
    /// </summary>
    public static class SyslogLoggerConfigurationExtensions
    {
        /// <summary>
        /// Adds a sink that writes log events to the local syslog service on a Linux system
        /// </summary>
        /// <param name="loggerSinkConfig">The logger configuration</param>
        /// <param name="facility">The category of the application</param>
        /// <param name="messageFormat">A message template describing the output messages
        /// <seealso cref="https://github.com/serilog/serilog/wiki/Formatting-Output"/>
        /// </param>
          public static LoggerConfiguration LocalSyslog(this LoggerSinkConfiguration loggerSinkConfig,
            Facility facility = Facility.Local0, MessageFormat messageFormat = MessageFormat.JSON, string appName = null)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                throw new ArgumentException("The local syslog sink is only supported on Linux systems");

            var formatter = GetFormatter(SyslogFormat.Local, null, facility, messageFormat);
            var syslogService = new LocalSyslogService(facility, appName);
            syslogService.Open();

            var sink = new SyslogLocalSink(formatter, syslogService);

            return loggerSinkConfig.Sink(sink);
        }

        /// <summary>
        /// Adds a sink that writes log events to a UDP syslog server
        /// </summary>
        /// <param name="loggerSinkConfig">The logger configuration</param>
        /// <param name="host">Hostname of the syslog server</param>
        /// <param name="port">Port the syslog server is listening on</param>
        /// <param name="appName">The name of the application. Defaults to the current process name</param>
        /// <param name="format">The syslog message format to be used</param>
        /// <param name="facility">The category of the application</param>
        /// <param name="messageFormat">A message template describing the output messages
        /// <seealso cref="https://github.com/serilog/serilog/wiki/Formatting-Output"/>
        /// </param>
        public static LoggerConfiguration UdpSyslog(this LoggerSinkConfiguration loggerSinkConfig,
            string host, int port = 514, string appName = null, SyslogFormat format = SyslogFormat.RFC3164,
            Facility facility = Facility.Local0, MessageFormat messageFormat = MessageFormat.JSON)
        {
            if (String.IsNullOrWhiteSpace(host))
                throw new ArgumentException(nameof(host));

            var formatter = GetFormatter(format, appName, facility, messageFormat);
            var endpoint = ResolveIP(host, port);

            var sink = new SyslogUdpSink(endpoint, formatter, BatchConfig.Default);

            return loggerSinkConfig.Sink(sink);
        }

        /// <summary>
        /// Adds a sink that writes log events to a TCP syslog server, optionally over a TLS-secured
        /// channel
        /// </summary>
        /// <param name="loggerSinkConfig">The logger configuration</param>
        /// <param name="config">Defines how to interact with the syslog server</param>
        public static LoggerConfiguration TcpSyslog(this LoggerSinkConfiguration loggerSinkConfig,
            SyslogTcpConfig config)
        {
            if (String.IsNullOrWhiteSpace(config.Host))
                throw new ArgumentException(nameof(config.Host));

            var sink = new SyslogTcpSink(config, BatchConfig.Default);

            return loggerSinkConfig.Sink(sink);
        }

        /// <summary>
        /// Adds a sink that writes log events to a TCP syslog server, optionally over a TLS-secured
        /// </summary>
        /// <param name="loggerSinkConfig">The logger configuration</param>
        /// <param name="host">Hostname of the syslog server</param>
        /// <param name="port">Port the syslog server is listening on</param>
        /// <param name="appName">The name of the application. Defaults to the current process name</param>
        /// <param name="framingType">How to frame/delimit syslog messages for the wire</param>
        /// <param name="format">The syslog message format to be used</param>
        /// <param name="facility">The category of the application</param>
        /// <param name="secureProtocols">
        /// SSL/TLS protocols to be used for a secure channel. Set to None for an unsecured connection
        /// </param>
        /// <param name="certProvider">Optionally used to present the syslog server with a client certificate</param>
        /// <param name="certValidationCallback">
        /// Optional callback used to validate the syslog server's certificate. If null, the system default
        /// will be used
        /// </param>
        /// <param name="messageFormat">A message template describing the output messages
        /// <seealso cref="https://github.com/serilog/serilog/wiki/Formatting-Output"/>
        /// </param>
        public static LoggerConfiguration TcpSyslog(this LoggerSinkConfiguration loggerSinkConfig,
            string host, int port = 1468, string appName = null, FramingType framingType = FramingType.OCTET_COUNTING,
            SyslogFormat format = SyslogFormat.RFC5424, Facility facility = Facility.Local0,
            SslProtocols secureProtocols = SslProtocols.None, ICertificateProvider certProvider = null,
            RemoteCertificateValidationCallback certValidationCallback = null,
            MessageFormat messageFormat = MessageFormat.JSON)
        {
            var formatter = GetFormatter(format, appName, facility, messageFormat);

            var config = new SyslogTcpConfig
            {
                Host = host,
                Port = port,
                Formatter = formatter,
                Framer = new MessageFramer(framingType),
                SecureProtocols = secureProtocols,
                CertProvider = certProvider,
                CertValidationCallback = certValidationCallback
            };

            return TcpSyslog(loggerSinkConfig, config);
        }

        private static ISyslogFormatter GetFormatter(SyslogFormat format, string appName, Facility facility,
            MessageFormat outputTemplate)
        {
            var templateFormatter = outputTemplate == MessageFormat.PlainText? (ITextFormatter) new MessageTemplateTextFormatter("{Message}", null)
                : (ITextFormatter)new CustomJsonFormatter(omitEnclosingObject:false, closingDelimiter:"", renderMessage: true);

            switch (format)
            {
                case SyslogFormat.RFC3164:
                    return new Rfc3164Formatter(facility, appName, templateFormatter);
                case SyslogFormat.RFC5424:
                    return new Rfc5424Formatter(facility, appName, templateFormatter);
                case SyslogFormat.Local:
                    return new LocalFormatter(facility, templateFormatter);
                default:
                    throw new ArgumentException($"Invalid format: {format}");
            }
        }

        private static IPEndPoint ResolveIP(string host, int port)
        {
            if (!IPAddress.TryParse(host, out var addr))
            {
                var ips = Task.Run(async () => await Dns.GetHostAddressesAsync(host)).Result;

                addr = ips.First(x => x.AddressFamily == AddressFamily.InterNetwork);
            }

            return new IPEndPoint(addr, port);
        }

        public class CustomJsonFormatter: JsonFormatter
        {
            public CustomJsonFormatter(bool omitEnclosingObject,
                string closingDelimiter = null,
                bool renderMessage = false,
                IFormatProvider formatProvider = null)
                : base(false, closingDelimiter, renderMessage, formatProvider)
            {
            }

           
          
            protected override void WriteRenderedMessage(string message, ref string delim, TextWriter output)
            {
                WriteJsonProperty("Message", message, ref delim, output);
            }

            protected override void WriteMessageTemplate(string message, ref string delim, TextWriter output)
            {
               
            }
        }
    }
}
