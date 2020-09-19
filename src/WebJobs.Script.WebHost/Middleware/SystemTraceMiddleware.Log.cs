// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Script.WebHost.Middleware
{
    internal partial class SystemTraceMiddleware
    {
        private static class Logger
        {
            private static readonly Action<ILogger, Exception> _responseStarted =
               LoggerMessage.Define(LogLevel.Debug, new EventId(1, nameof(ResponseStarted)), "The response has already started, the status code will not be modified.");

            private static readonly Action<ILogger, Exception> _unhandledHostError =
                LoggerMessage.Define(LogLevel.Error, new EventId(2, nameof(UnhandledHostError)), "An unhandled host error has occurred.");

            private static readonly Func<ILogger, string, IDisposable> _verifyingHostAvailabilityScope =
                LoggerMessage.DefineScope<string>("Verifying host availability (Request = {RequestId})");

            private static readonly Action<ILogger, Exception> _initiatingHostAvailabilityCheck =
               LoggerMessage.Define(LogLevel.Trace, new EventId(3, nameof(InitiatingHostAvailabilityCheck)), "Initiating host availability check.");

            private static readonly Action<ILogger, Exception> _hostUnavailableAfterCheck =
               LoggerMessage.Define(LogLevel.Warning, new EventId(4, nameof(HostUnavailableAfterCheck)), "Host unavailable after check. Returning error.");

            private static readonly Action<ILogger, Exception> _hostAvailabilityCheckSucceeded =
               LoggerMessage.Define(LogLevel.Trace, new EventId(5, nameof(HostAvailabilityCheckSucceeded)), "Host availability check succeeded.");

            public static void ResponseStarted(ILogger logger) => _responseStarted(logger, null);

            public static void UnhandledHostError(ILogger logger, Exception ex) => _unhandledHostError(logger, ex);

            public static IDisposable VerifyingHostAvailabilityScope(ILogger logger, string requestId) => _verifyingHostAvailabilityScope(logger, requestId);

            public static void HostUnavailableAfterCheck(ILogger logger) => _hostUnavailableAfterCheck(logger, null);

            public static void HostAvailabilityCheckSucceeded(ILogger logger) => _hostAvailabilityCheckSucceeded(logger, null);

            public static void InitiatingHostAvailabilityCheck(ILogger logger) => _initiatingHostAvailabilityCheck(logger, null);
        }
    }
}
