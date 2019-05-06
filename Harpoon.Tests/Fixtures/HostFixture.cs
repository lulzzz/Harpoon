﻿using Harpoon.Tests.Mocks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Harpoon.Tests.Fixtures
{
    public class HostFixture : IDisposable
    {
        public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
        {
            private readonly ClaimsPrincipal _principal = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim> { new Claim(ClaimTypes.Name, "name") }, "TEST"));

            public TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
                : base(options, logger, encoder, clock)
            {
            }

            protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                var authenticationTicket = new AuthenticationTicket(_principal, new AuthenticationProperties(), "TEST");
                return Task.FromResult(AuthenticateResult.Success(authenticationTicket));
            }
        }

        public class DefaultStartup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddMvcCore().AddJsonFormatters();
                services.AddEntityFrameworkSqlServer().AddDbContext<TestContext2>();

                services.AddHarpoon().UseEfStorage<TestContext2>((p, b) => { }).UseDefaultSenderInBackground();

                var actionProvider = new Mock<IWebHookActionProvider>();
                actionProvider.Setup(p => p.GetAvailableActionsAsync()).ReturnsAsync(new Dictionary<string, WebHookAction> { ["action1"] = new WebHookAction { Id = "action1" } });
                services.AddSingleton(actionProvider.Object);

                services.AddAuthentication(o =>
                {
                    o.DefaultScheme = "TEST";
                }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("TEST", "TEST", o => { });
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseAuthentication();
                app.UseMvc();

                using (var scope = app.ApplicationServices.CreateScope())
                {
                    scope.ServiceProvider.GetRequiredService<TestContext2>().Database.EnsureCreated();
                }
            }
        }

        public readonly HttpClient Client;
        public readonly TestServer Server;

        public HostFixture()
        {
            var hostBuilder = WebHost.CreateDefaultBuilder(null).UseStartup<DefaultStartup>();
            Server = new TestServer(hostBuilder);
            Client = Server.CreateClient();
        }

        public void Dispose()
        {
            if (Server?.Host?.Services != null)
            {
                using (var scope = Server.Host.Services.CreateScope())
                {
                    scope.ServiceProvider.GetRequiredService<TestContext2>().Database.EnsureDeleted();
                }
            }
        }
    }
}