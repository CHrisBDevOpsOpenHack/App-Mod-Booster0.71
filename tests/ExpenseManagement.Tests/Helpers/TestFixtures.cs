using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ExpenseManagement.Tests.Helpers;

/// <summary>
/// Custom WebApplicationFactory for integration testing
/// </summary>
public class ExpenseManagementWebApplicationFactory : WebApplicationFactory<Program>
{
    public IConfiguration? TestConfiguration { get; set; }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Configure test-specific settings
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Clear default configuration sources
            config.Sources.Clear();

            // Add test configuration
            var testConfig = new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=test-server;Database=test-db;",
                ["GenAISettings:OpenAIEndpoint"] = "", // Default to not configured
                ["GenAISettings:OpenAIModelName"] = "",
                ["ManagedIdentityClientId"] = ""
            };

            // Override with custom test configuration if provided
            if (TestConfiguration != null)
            {
                foreach (var kvp in TestConfiguration.AsEnumerable())
                {
                    if (kvp.Key != null && kvp.Value != null)
                    {
                        testConfig[kvp.Key] = kvp.Value;
                    }
                }
            }

            config.AddInMemoryCollection(testConfig!);
        });

        builder.ConfigureServices(services =>
        {
            // Add any test-specific service overrides here
            // For example, replace real database with in-memory database
        });

        return base.CreateHost(builder);
    }
}

/// <summary>
/// Base class for tests that need test data cleanup
/// </summary>
public abstract class TestBase : IDisposable
{
    protected List<IDisposable> DisposableResources { get; } = new();

    protected void AddDisposable(IDisposable resource)
    {
        DisposableResources.Add(resource);
    }

    public virtual void Dispose()
    {
        foreach (var resource in DisposableResources)
        {
            try
            {
                resource?.Dispose();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        DisposableResources.Clear();
        GC.SuppressFinalize(this);
    }
}
