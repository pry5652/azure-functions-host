// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Script.WebHost;
using Microsoft.Azure.WebJobs.Script.WebHost.Authentication;
using Microsoft.Azure.WebJobs.Script.WebHost.Middleware;
using Microsoft.Azure.WebJobs.Script.WebHost.Security.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.WebJobs.Script.Tests;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Script.Tests.Handlers
{
    public class SystemTraceMiddlewareTests
    {
        private readonly TestLoggerProvider _loggerProvider;
        private readonly SystemTraceMiddleware _middleware;

        public SystemTraceMiddlewareTests()
        {
            _loggerProvider = new TestLoggerProvider();
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(_loggerProvider);

            RequestDelegate requestDelegate = async (HttpContext context) =>
            {
                await Task.Delay(25);
            };

            var logger = loggerFactory.CreateLogger<SystemTraceMiddleware>();
            var mockEnvironment = new Mock<IEnvironment>(MockBehavior.Strict);
            var hostNameProvider = new HostNameProvider(mockEnvironment.Object);
            // TODO: construct this service properly
            var scriptHostService = new Mock<WebJobsScriptHostService>(MockBehavior.Strict);
            var scriptHostOptions = new ScriptApplicationHostOptions();
            var applicationHostOptionsMonitor = new TestOptionsMonitor<ScriptApplicationHostOptions>(scriptHostOptions);
            var standbyOptions = new StandbyOptions();
            var standbyOptionsMonitor = new TestOptionsMonitor<StandbyOptions>(standbyOptions);
            var mockWebHostEnvironment = new Mock<IScriptWebHostEnvironment>(MockBehavior.Strict);
            var standbyManagerMock = new Mock<IStandbyManager>(MockBehavior.Strict);
            var environment = new TestEnvironment();
            _middleware = new SystemTraceMiddleware(requestDelegate, hostNameProvider, scriptHostService.Object, applicationHostOptionsMonitor, standbyOptionsMonitor, mockWebHostEnvironment.Object, standbyManagerMock.Object, environment, logger);
        }

        [Fact]
        public async Task SendAsync_WritesExpectedTraces()
        {
            string requestId = Guid.NewGuid().ToString();
            var context = new DefaultHttpContext();
            Uri uri = new Uri("http://functions.com/api/testfunc?code=123");
            var requestFeature = context.Request.HttpContext.Features.Get<IHttpRequestFeature>();
            requestFeature.Method = "GET";
            requestFeature.Scheme = uri.Scheme;
            requestFeature.Path = uri.GetComponents(UriComponents.KeepDelimiter | UriComponents.Path, UriFormat.Unescaped);
            requestFeature.PathBase = string.Empty;
            requestFeature.QueryString = uri.GetComponents(UriComponents.KeepDelimiter | UriComponents.Query, UriFormat.Unescaped);

            var headers = new HeaderDictionary();
            headers.Add(ScriptConstants.AntaresLogIdHeaderName, new StringValues(requestId));
            headers.Add("User-Agent", new StringValues("TestAgent"));
            requestFeature.Headers = headers;

            var principal = new ClaimsPrincipal();
            principal.AddIdentity(new ClaimsIdentity(new List<Claim>
            {
                new Claim(SecurityConstants.AuthLevelClaimType, AuthorizationLevel.Function.ToString())
            }, AuthLevelAuthenticationDefaults.AuthenticationScheme));
            principal.AddIdentity(new ClaimsIdentity(new List<Claim>
            {
                new Claim(SecurityConstants.AuthLevelClaimType, "CustomLevel")
            }, "CustomScheme"));
            context.User = principal;

            await _middleware.Invoke(context);

            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.Equal(2, logs.Length);

            // validate executing trace
            var log = logs[0];
            Assert.Equal(typeof(SystemTraceMiddleware).FullName, log.Category);
            Assert.Equal(LogLevel.Information, log.Level);
            var idx = log.FormattedMessage.IndexOf(':');
            var message = log.FormattedMessage.Substring(0, idx).Trim();
            Assert.Equal("Executing HTTP request", message);
            var details = log.FormattedMessage.Substring(idx + 1).Trim();
            var jo = JObject.Parse(details);
            Assert.Equal(4, jo.Count);
            Assert.Equal(requestId, jo["requestId"]);
            Assert.Equal("GET", jo["method"]);
            Assert.Equal("/api/testfunc", jo["uri"]);
            Assert.Equal("TestAgent", jo["userAgent"]);

            // validate executed trace
            log = logs[1];
            Assert.Equal(typeof(SystemTraceMiddleware).FullName, log.Category);
            Assert.Equal(LogLevel.Information, log.Level);
            idx = log.FormattedMessage.IndexOf(':');
            message = log.FormattedMessage.Substring(0, idx).Trim();
            Assert.Equal("Executed HTTP request", message);
            details = log.FormattedMessage.Substring(idx + 1).Trim();
            jo = JObject.Parse(details);
            Assert.Equal(4, jo.Count);
            Assert.Equal(requestId, jo["requestId"]);
            Assert.Equal(200, jo["status"]);
            var duration = (long)jo["duration"];
            Assert.True(duration > 0);

            string identities = (string)jo["identities"];
            Assert.Equal($"({AuthLevelAuthenticationDefaults.AuthenticationScheme}:Function, CustomScheme:CustomLevel)", identities);
        }

        [Fact]
        public async Task Invoke_HandlesHttpExceptions()
        {
            var ex = new HttpException(StatusCodes.Status502BadGateway);

            using (var server = GetTestServer(_ => throw ex))
            {
                var client = server.CreateClient();
                HttpResponseMessage response = await client.GetAsync(string.Empty);

                Assert.Equal(ex.StatusCode, (int)response.StatusCode);

                var log = _loggerProvider.GetAllLogMessages().Single(p => p.Category.Contains(nameof(SystemTraceMiddleware)));
                Assert.Equal("An unhandled host error has occurred.", log.FormattedMessage);
                Assert.Same(ex, log.Exception);
            }
        }

        [Fact]
        public async Task Invoke_HandlesNonHttpExceptions()
        {
            var ex = new Exception("Kaboom!");

            using (var server = GetTestServer(_ => throw ex))
            {
                var client = server.CreateClient();
                HttpResponseMessage response = await client.GetAsync(string.Empty);

                Assert.Equal(StatusCodes.Status500InternalServerError, (int)response.StatusCode);

                var log = _loggerProvider.GetAllLogMessages().Single(p => p.Category.Contains(nameof(SystemTraceMiddleware)));
                Assert.Equal("An unhandled host error has occurred.", log.FormattedMessage);
                Assert.Same(ex, log.Exception);
            }
        }

        [Fact]
        public async Task Invoke_HandlesFunctionInvocationExceptions()
        {
            var ex = new FunctionInvocationException("Kaboom!");

            using (var server = GetTestServer(_ => throw ex))
            {
                var client = server.CreateClient();
                HttpResponseMessage response = await client.GetAsync(string.Empty);

                Assert.Equal(StatusCodes.Status500InternalServerError, (int)response.StatusCode);
                Assert.Null(_loggerProvider.GetAllLogMessages().SingleOrDefault(p => p.Category.Contains(nameof(SystemTraceMiddleware))));
            }
        }

        [Fact]
        public async Task Invoke_LogsError_AfterResponseWritten()
        {
            var ex = new InvalidOperationException("Kaboom!");

            async Task WriteThenThrow(HttpContext context)
            {
                await context.Response.WriteAsync("Hi.");
                throw ex;
            }

            using (var server = GetTestServer(c => WriteThenThrow(c)))
            {
                var client = server.CreateClient();
                HttpResponseMessage response = await client.GetAsync(string.Empty);

                // Because the response had already been written, this cannot change.
                Assert.Equal(StatusCodes.Status200OK, (int)response.StatusCode);
                Assert.Equal("Hi.", await response.Content.ReadAsStringAsync());

                var logs = _loggerProvider.GetAllLogMessages().Where(p => p.Category.Contains(nameof(SystemTraceMiddleware)));
                Assert.Collection(logs,
                    m =>
                    {
                        Assert.Equal("An unhandled host error has occurred.", m.FormattedMessage);
                        Assert.Same(ex, m.Exception);
                        Assert.Equal("UnhandledHostError", m.EventId.Name);
                    },
                    m =>
                    {
                        Assert.Equal("The response has already started, the status code will not be modified.", m.FormattedMessage);
                        Assert.Equal("ResponseStarted", m.EventId.Name);
                    });
            }
        }

        private TestServer GetTestServer(Func<HttpContext, Task> callback)
        {
            // The custom middleware relies on the host starting the request (thus invoking OnStarting),
            // so we need to create a test host to flow through the entire pipeline.
            var builder = new WebHostBuilder()
                .ConfigureLogging(b =>
                {
                    b.AddProvider(_loggerProvider);
                    b.SetMinimumLevel(LogLevel.Debug);
                })
                .Configure(app =>
                {
                    app.Use(async (httpContext, next) =>
                    {
                        try
                        {
                            await next();
                        }
                        catch (InvalidOperationException)
                        {
                            // The TestServer cannot handle exceptions after the
                            // host has started.
                        }
                    });

                    app.UseMiddleware<SystemTraceMiddleware>();

                    app.Use((context, next) =>
                    {
                        return callback(context);
                    });
                });

            return new TestServer(builder);
        }
    }
}