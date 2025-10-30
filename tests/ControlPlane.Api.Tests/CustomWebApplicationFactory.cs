using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ControlPlane.Api.Services;

namespace ControlPlane.Api.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Use fresh instances for each test
            var agentStoreDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IAgentStore));
            if (agentStoreDescriptor != null)
            {
                services.Remove(agentStoreDescriptor);
            }

            var nodeStoreDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(INodeStore));
            if (nodeStoreDescriptor != null)
            {
                services.Remove(nodeStoreDescriptor);
            }

            var runStoreDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IRunStore));
            if (runStoreDescriptor != null)
            {
                services.Remove(runStoreDescriptor);
            }

            // Add new instances
            services.AddSingleton<IAgentStore, InMemoryAgentStore>();
            services.AddSingleton<INodeStore, InMemoryNodeStore>();
            services.AddSingleton<IRunStore, InMemoryRunStore>();
        });
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public new Task DisposeAsync()
    {
        return base.DisposeAsync().AsTask();
    }
}
