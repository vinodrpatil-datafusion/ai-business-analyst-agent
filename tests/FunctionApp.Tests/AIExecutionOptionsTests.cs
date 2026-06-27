using FunctionApp.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace FunctionApp.Tests;

public class AIExecutionOptionsTests
{
    [Fact]
    public void Bind_Defaults_FromConfiguration()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["AIExecution:MaxContextTokens"] = "8192",
            ["AIExecution:MaxPromptTokens"] = "6000",
            ["AIExecution:MaxOutputTokens"] = "800",
            ["AIExecution:SafetyMargin"] = "500",
            ["AIExecution:EnableAdaptiveBudgeting"] = "true"
        };

        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        var services = new ServiceCollection();
        services.Configure<AIExecutionOptions>(config.GetSection("AIExecution"));
        var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<AIExecutionOptions>>();

        Assert.Equal(8192, opts.Value.MaxContextTokens);
        Assert.Equal(6000, opts.Value.MaxPromptTokens);
        Assert.Equal(800, opts.Value.MaxOutputTokens);
        Assert.Equal(500, opts.Value.SafetyMargin);
        Assert.True(opts.Value.EnableAdaptiveBudgeting);
    }
}
