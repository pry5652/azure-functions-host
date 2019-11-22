﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.Azure.WebJobs.Script.Eventing;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Script.Workers.Http
{
    internal class HttpWorkerProcess : WorkerProcess
    {
        private readonly IWorkerProcessFactory _processFactory;
        private readonly ILogger _workerProcessLogger;
        private readonly IScriptEventManager _eventManager;
        private readonly HttpWorkerOptions _httpWorkerOptions;
        private readonly string _scriptRootPath;
        private readonly string _workerId;
        private readonly WorkerProcessArguments _workerProcessArguments;

        internal HttpWorkerProcess(string workerId,
                                       string rootScriptPath,
                                       HttpWorkerOptions httpWorkerOptions,
                                       IScriptEventManager eventManager,
                                       IWorkerProcessFactory processFactory,
                                       IProcessRegistry processRegistry,
                                       ILogger workerProcessLogger,
                                       IWorkerConsoleLogSource consoleLogSource)
            : base(eventManager, processRegistry, workerProcessLogger, consoleLogSource)
        {
            _processFactory = processFactory;
            _eventManager = eventManager;
            _workerProcessLogger = workerProcessLogger;
            _workerId = workerId;
            _scriptRootPath = rootScriptPath;
            _httpWorkerOptions = httpWorkerOptions;
            _workerProcessArguments = _httpWorkerOptions.Arguments;
        }

        public override Process CreateWorkerProcess()
        {
            var workerContext = new HttpWorkerContext()
            {
                RequestId = Guid.NewGuid().ToString(),
                WorkerId = _workerId,
                Arguments = _workerProcessArguments,
                WorkingDirectory = _scriptRootPath,
                Port = _httpWorkerOptions.Port
            };
            workerContext.EnvironmentVariables.Add(HttpWorkerConstants.PortEnvVarName, _httpWorkerOptions.Port.ToString());
            return _processFactory.CreateWorkerProcess(workerContext);
        }

        public override void HandleWorkerProcessExitError(WorkerProcessExitException langExc)
        {
            // The subscriber of WorkerErrorEvent is expected to Dispose() the errored channel
            if (langExc != null && langExc.ExitCode != -1)
            {
                _workerProcessLogger.LogDebug(langExc, $"Language Worker Process exited.", _workerProcessArguments.ExecutablePath);
                _eventManager.Publish(new HttpWorkerErrorEvent(_workerId, langExc));
            }
        }

        public override void HandleWorkerProcessRestart()
        {
            _workerProcessLogger?.LogInformation("Language Worker Process exited and needs to be restarted.");
            _eventManager.Publish(new HttpWorkerRestartEvent(_workerId));
        }
    }
}