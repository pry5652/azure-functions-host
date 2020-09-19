// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Script.Extensions;
using Microsoft.Azure.WebJobs.Script.WebHost.Diagnostics.Extensions;
using Microsoft.Azure.WebJobs.Script.WebHost.Extensions;
using Microsoft.Azure.WebJobs.Script.WebHost.Security.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.WebJobs.Script.WebHost.Middleware
{
    internal partial class SystemTraceMiddleware
    {
        private readonly ILogger _logger;
        private readonly RequestDelegate _next;
        private readonly HostNameProvider _hostNameProvider;
        private readonly WebJobsScriptHostService _scriptHostService;
        private readonly IOptionsMonitor<ScriptApplicationHostOptions> _applicationHostOptions;
        private readonly IScriptWebHostEnvironment _scriptWebHostEnvironment;
        private readonly IStandbyManager _standbyManager;
        private readonly IEnvironment _environment;
        private readonly bool _checkEnvironment;
        private bool _specialized = false;

        public SystemTraceMiddleware(RequestDelegate next, HostNameProvider hostNameProvider, WebJobsScriptHostService scriptHostService, IOptionsMonitor<ScriptApplicationHostOptions> applicationHostOptions,
            IOptionsMonitor<StandbyOptions> standbyOptions, IScriptWebHostEnvironment scriptWebHostEnvironment, IStandbyManager standbyManager, IEnvironment environment, ILogger<SystemTraceMiddleware> logger)
        {
            _logger = logger;
            _next = next;
            _hostNameProvider = hostNameProvider;
            _scriptHostService = scriptHostService;
            _applicationHostOptions = applicationHostOptions;
            _scriptWebHostEnvironment = scriptWebHostEnvironment;
            _standbyManager = standbyManager;
            _environment = environment;
            _checkEnvironment = _environment.IsLinuxConsumption();

            if (!standbyOptions.CurrentValue.InStandbyMode)
            {
                _specialized = true;
            }
        }

        public async Task Invoke(HttpContext context)
        {
            var requestId = SetRequestId(context.Request);
            var sw = Stopwatch.StartNew();
            string userAgent = context.Request.GetHeaderValueOrDefault("User-Agent");
            _logger.ExecutingHttpRequest(requestId, context.Request.Method, userAgent, context.Request.Path);

            await ProcessRequestAsync(context);

            sw.Stop();
            string identities = GetIdentities(context);
            _logger.ExecutedHttpRequest(requestId, identities, context.Response.StatusCode, sw.ElapsedMilliseconds);
        }

        private async Task ProcessRequestAsync(HttpContext context)
        {
            try
            {
                PrepareRequest(context);

                if (_checkEnvironment && _scriptWebHostEnvironment.DelayRequestsEnabled)
                {
                    await _scriptWebHostEnvironment.DelayCompletionTask;
                }

                if (!_specialized)
                {
                    await SpecializeIfNecessary(context);
                }

                bool shouldProcess = true;
                if ((_scriptHostService.State == ScriptHostState.Offline || !_scriptHostService.CanInvoke()) && !context.Request.IsAdminRequest())
                {
                    shouldProcess = await VerfifyHostAvailabilityAsync(context);
                }

                if (shouldProcess)
                {
                    ApplyFeatures(context);

                    await _next.Invoke(context);
                }

                CompleteRequest(context);
            }
            catch (Exception ex)
            {
                if (!(ex is FunctionInvocationException))
                {
                    // exceptions throw by function code are handled/logged elsewhere
                    // our goal here is to log exceptions coming from our own runtime
                    Logger.UnhandledHostError(_logger, ex);
                }

                // We can't do anything if the response has already started, just abort.
                if (context.Response.HasStarted)
                {
                    Logger.ResponseStarted(_logger);
                    throw;
                }

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;

                if (ex is HttpException httpException)
                {
                    context.Response.StatusCode = httpException.StatusCode;
                }
            }
        }

        private void PrepareRequest(HttpContext context)
        {
            _hostNameProvider.Synchronize(context.Request, _logger);
        }

        private void CompleteRequest(HttpContext context)
        {
            if (context.Items.TryGetValue(ScriptConstants.AzureFunctionsDuplicateHttpHeadersKey, out object value))
            {
                _logger.LogDebug($"Duplicate HTTP header from function invocation removed. Duplicate key(s): {value?.ToString()}.");
            }
        }

        private void ApplyFeatures(HttpContext context)
        {
            // This feature must be registered before any other middleware depending on
            // JobHost/ScriptHost scoped services.
            if (_scriptHostService.Services is IServiceScopeFactory scopedServiceProvider)
            {
                var features = context.Features;
                features.Set<IServiceProvidersFeature>(new RequestServicesFeature(context, scopedServiceProvider));
            }
        }

        private async Task<bool> VerfifyHostAvailabilityAsync(HttpContext context)
        {
            if (_scriptHostService.State != ScriptHostState.Offline)
            {
                bool hostReady = _scriptHostService.CanInvoke();
                if (!hostReady)
                {
                    using (Logger.VerifyingHostAvailabilityScope(_logger, context.TraceIdentifier))
                    {
                        Logger.InitiatingHostAvailabilityCheck(_logger);

                        hostReady = await _scriptHostService.DelayUntilHostReady();
                        if (!hostReady)
                        {
                            Logger.HostUnavailableAfterCheck(_logger);

                            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                            await context.Response.WriteAsync("Function host is not running.");

                            return false;
                        }

                        Logger.HostAvailabilityCheckSucceeded(_logger);
                    }
                }

                return true;
            }
            else
            {
                await context.SetOfflineResponseAsync(_applicationHostOptions.CurrentValue.ScriptPath);
            }

            return false;
        }

        private async Task SpecializeIfNecessary(HttpContext httpContext)
        {
            if (!_scriptWebHostEnvironment.InStandbyMode && _environment.IsContainerReady())
            {
                // We don't want AsyncLocal context (like Activity.Current) to flow
                // here as it will contain request details. Suppressing this context
                // prevents the request context from being captured by the host.
                Task specializeTask;
                using (System.Threading.ExecutionContext.SuppressFlow())
                {
                    specializeTask = _standbyManager.SpecializeHostAsync();
                }
                await specializeTask;
            }
        }

        internal static string SetRequestId(HttpRequest request)
        {
            string requestID = request.GetHeaderValueOrDefault(ScriptConstants.AntaresLogIdHeaderName) ?? Guid.NewGuid().ToString();
            request.HttpContext.Items[ScriptConstants.AzureFunctionsRequestIdKey] = requestID;
            return requestID;
        }

        private static string GetIdentities(HttpContext context)
        {
            var identities = context.User.Identities.Where(p => p.IsAuthenticated);
            if (identities.Any())
            {
                var sbIdentities = new StringBuilder();

                foreach (var identity in identities)
                {
                    if (sbIdentities.Length > 0)
                    {
                        sbIdentities.Append(", ");
                    }

                    var sbIdentity = new StringBuilder(identity.AuthenticationType);
                    var claim = identity.Claims.FirstOrDefault(p => p.Type == SecurityConstants.AuthLevelClaimType);
                    if (claim != null)
                    {
                        sbIdentity.AppendFormat(":{0}", claim.Value);
                    }

                    sbIdentities.Append(sbIdentity);
                }

                sbIdentities.Insert(0, "(");
                sbIdentities.Append(")");

                return sbIdentities.ToString();
            }
            else
            {
                return string.Empty;
            }
        }
    }
}