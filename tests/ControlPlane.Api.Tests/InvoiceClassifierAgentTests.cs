using System.Text.Json;
using ControlPlane.Api.Models;
using Xunit;

namespace ControlPlane.Api.Tests;

/// <summary>
/// Tests for the Invoice Classifier agent definition.
/// Validates that the agent definition file is properly structured and contains all required fields.
/// </summary>
public class InvoiceClassifierAgentTests
{
    private const string AgentDefinitionPath = "../../../../../agents/definitions/invoice-classifier.json";
    
    private static readonly JsonSerializerOptions JsonOptions = new() 
    { 
        PropertyNameCaseInsensitive = true 
    };

    private async Task<Agent> LoadAgentDefinitionAsync()
    {
        var jsonContent = await File.ReadAllTextAsync(AgentDefinitionPath);
        return JsonSerializer.Deserialize<Agent>(jsonContent, JsonOptions)!;
    }

    [Fact]
    public void InvoiceClassifierDefinition_FileExists()
    {
        // Assert
        Assert.True(File.Exists(AgentDefinitionPath),
            "the invoice-classifier.json definition file should exist in the agents/definitions directory");
    }

    [Fact]
    public async Task InvoiceClassifierDefinition_IsValidJson()
    {
        // Arrange
        var jsonContent = await File.ReadAllTextAsync(AgentDefinitionPath);

        // Act & Assert
        var exception = Record.Exception(() => JsonSerializer.Deserialize<Agent>(jsonContent, JsonOptions));
        Assert.Null(exception);
    }

    [Fact]
    public async Task InvoiceClassifierDefinition_HasRequiredFields()
    {
        // Arrange & Act
        var agent = await LoadAgentDefinitionAsync();

        // Assert
        Assert.NotNull(agent);
        Assert.Equal("invoice-classifier", agent.AgentId);
        Assert.Equal("Invoice Classifier", agent.Name);
        Assert.False(string.IsNullOrWhiteSpace(agent.Description));
        Assert.False(string.IsNullOrWhiteSpace(agent.Instructions));
    }

    [Fact]
    public async Task InvoiceClassifierDefinition_HasValidModelProfile()
    {
        // Arrange & Act
        var agent = await LoadAgentDefinitionAsync();

        // Assert
        Assert.NotNull(agent);
        Assert.NotNull(agent.ModelProfile);
        Assert.True(agent.ModelProfile.ContainsKey("model"));
        Assert.Equal("gpt-4", agent.ModelProfile["model"].ToString());
        Assert.True(agent.ModelProfile.ContainsKey("temperature"));
        Assert.True(agent.ModelProfile.ContainsKey("maxTokens"));
    }

    [Fact]
    public async Task InvoiceClassifierDefinition_HasValidBudget()
    {
        // Arrange & Act
        var agent = await LoadAgentDefinitionAsync();

        // Assert
        Assert.NotNull(agent);
        Assert.NotNull(agent.Budget);
        Assert.Equal(4000, agent.Budget.MaxTokens);
        Assert.Equal(60, agent.Budget.MaxDurationSeconds);
    }

    [Fact]
    public async Task InvoiceClassifierDefinition_HasServiceBusInput()
    {
        // Arrange & Act
        var agent = await LoadAgentDefinitionAsync();

        // Assert
        Assert.NotNull(agent);
        Assert.NotNull(agent.Input);
        Assert.Equal("ServiceBus", agent.Input.Type);
        Assert.NotNull(agent.Input.Config);
        Assert.True(agent.Input.Config.ContainsKey("queueName"));
        Assert.Equal("invoices", agent.Input.Config["queueName"].ToString());
    }

    [Fact]
    public async Task InvoiceClassifierDefinition_ServiceBusInput_HasCorrectConfiguration()
    {
        // Arrange & Act
        var agent = await LoadAgentDefinitionAsync();

        // Assert
        Assert.NotNull(agent);
        Assert.NotNull(agent.Input);
        Assert.NotNull(agent.Input.Config);
        
        // Verify Service Bus specific configuration
        Assert.True(agent.Input.Config.ContainsKey("connectionString"));
        Assert.True(agent.Input.Config.ContainsKey("prefetchCount"));
        Assert.True(agent.Input.Config.ContainsKey("maxDeliveryCount"));
        Assert.True(agent.Input.Config.ContainsKey("receiveMode"));
        
        // Verify values
        var prefetchCount = ((JsonElement)agent.Input.Config["prefetchCount"]).GetInt32();
        Assert.Equal(16, prefetchCount);
        
        var maxDeliveryCount = ((JsonElement)agent.Input.Config["maxDeliveryCount"]).GetInt32();
        Assert.Equal(3, maxDeliveryCount);
        
        var receiveMode = agent.Input.Config["receiveMode"].ToString();
        Assert.Equal("PeekLock", receiveMode);
    }

    [Fact]
    public async Task InvoiceClassifierDefinition_HasHttpOutput()
    {
        // Arrange & Act
        var agent = await LoadAgentDefinitionAsync();

        // Assert
        Assert.NotNull(agent);
        Assert.NotNull(agent.Output);
        Assert.Equal("Http", agent.Output.Type);
        Assert.NotNull(agent.Output.Config);
        Assert.True(agent.Output.Config.ContainsKey("method"));
        Assert.Equal("POST", agent.Output.Config["method"].ToString());
    }

    [Fact]
    public async Task InvoiceClassifierDefinition_HttpOutput_HasRetryPolicy()
    {
        // Arrange & Act
        var agent = await LoadAgentDefinitionAsync();

        // Assert
        Assert.NotNull(agent);
        Assert.NotNull(agent.Output);
        Assert.NotNull(agent.Output.Config);
        Assert.True(agent.Output.Config.ContainsKey("retryPolicy"));
        
        var retryPolicyJson = (JsonElement)agent.Output.Config["retryPolicy"];
        var maxRetries = retryPolicyJson.GetProperty("maxRetries").GetInt32();
        Assert.Equal(3, maxRetries);
        
        var useExponentialBackoff = retryPolicyJson.GetProperty("useExponentialBackoff").GetBoolean();
        Assert.True(useExponentialBackoff);
    }

    [Fact]
    public async Task InvoiceClassifierDefinition_HttpOutput_HasIdempotencyKey()
    {
        // Arrange & Act
        var agent = await LoadAgentDefinitionAsync();

        // Assert
        Assert.NotNull(agent);
        Assert.NotNull(agent.Output);
        Assert.NotNull(agent.Output.Config);
        Assert.True(agent.Output.Config.ContainsKey("idempotencyKeyFormat"));
        
        var idempotencyKeyFormat = agent.Output.Config["idempotencyKeyFormat"].ToString();
        Assert.Contains("RunId", idempotencyKeyFormat);
        Assert.Contains("MessageId", idempotencyKeyFormat);
    }

    [Fact]
    public async Task InvoiceClassifierDefinition_HasTools()
    {
        // Arrange & Act
        var agent = await LoadAgentDefinitionAsync();

        // Assert
        Assert.NotNull(agent);
        Assert.NotNull(agent.Tools);
        Assert.Contains("http-post", agent.Tools);
    }

    [Fact]
    public async Task InvoiceClassifierDefinition_HasMetadata()
    {
        // Arrange & Act
        var agent = await LoadAgentDefinitionAsync();

        // Assert
        Assert.NotNull(agent);
        Assert.NotNull(agent.Metadata);
        Assert.True(agent.Metadata.ContainsKey("owner"));
        Assert.True(agent.Metadata.ContainsKey("epic"));
        Assert.True(agent.Metadata.ContainsKey("version"));
        Assert.Equal("Platform Engineering", agent.Metadata["owner"]);
        Assert.Equal("E3-T6", agent.Metadata["epic"]);
    }

    [Fact]
    public async Task InvoiceClassifierDefinition_Instructions_ContainsClassificationCategories()
    {
        // Arrange & Act
        var agent = await LoadAgentDefinitionAsync();

        // Assert
        Assert.NotNull(agent);
        Assert.False(string.IsNullOrWhiteSpace(agent.Instructions));
        
        // Verify instructions contain all expected vendor categories
        var expectedCategories = new[]
        {
            "Office Supplies",
            "Technology/Hardware",
            "Professional Services",
            "Utilities",
            "Travel & Expenses",
            "Other"
        };

        foreach (var category in expectedCategories)
        {
            Assert.Contains(category, agent.Instructions);
        }
    }

    [Fact]
    public async Task InvoiceClassifierDefinition_Instructions_ContainsRoutingDestinations()
    {
        // Arrange & Act
        var agent = await LoadAgentDefinitionAsync();

        // Assert
        Assert.NotNull(agent);
        Assert.False(string.IsNullOrWhiteSpace(agent.Instructions));
        
        // Verify instructions contain all expected routing destinations
        var expectedDestinations = new[]
        {
            "Procurement Department",
            "IT Department",
            "Finance Department",
            "Facilities Management",
            "HR Department",
            "General Accounts Payable"
        };

        foreach (var destination in expectedDestinations)
        {
            Assert.Contains(destination, agent.Instructions);
        }
    }

    [Fact]
    public async Task InvoiceClassifierDefinition_Instructions_ContainsOutputFormat()
    {
        // Arrange & Act
        var agent = await LoadAgentDefinitionAsync();

        // Assert
        Assert.NotNull(agent);
        Assert.False(string.IsNullOrWhiteSpace(agent.Instructions));
        
        // Verify instructions specify the expected output format
        Assert.Contains("JSON", agent.Instructions);
        Assert.Contains("vendorName", agent.Instructions);
        Assert.Contains("vendorCategory", agent.Instructions);
        Assert.Contains("routingDestination", agent.Instructions);
        Assert.Contains("confidence", agent.Instructions);
    }

    [Fact]
    public async Task InvoiceClassifierDefinition_ModelProfile_HasLowTemperature()
    {
        // Arrange & Act
        var agent = await LoadAgentDefinitionAsync();

        // Assert
        Assert.NotNull(agent);
        Assert.NotNull(agent.ModelProfile);
        Assert.True(agent.ModelProfile.ContainsKey("temperature"));
        
        var temperature = ((JsonElement)agent.ModelProfile["temperature"]).GetDouble();
        Assert.True(temperature <= 0.5, 
            $"Temperature should be low (≤0.5) for consistent classification results, but was {temperature}");
    }

    [Fact]
    public async Task InvoiceClassifierDefinition_Budget_IsReasonableForInvoiceProcessing()
    {
        // Arrange & Act
        var agent = await LoadAgentDefinitionAsync();

        // Assert
        Assert.NotNull(agent);
        Assert.NotNull(agent.Budget);
        
        // Verify budget is reasonable for invoice processing
        Assert.True(agent.Budget.MaxTokens >= 1000, 
            "Max tokens should be sufficient for processing invoice data");
        Assert.True(agent.Budget.MaxTokens <= 10000, 
            "Max tokens should not be excessive for simple classification");
        
        Assert.True(agent.Budget.MaxDurationSeconds >= 30, 
            "Max duration should allow time for LLM processing");
        Assert.True(agent.Budget.MaxDurationSeconds <= 120, 
            "Max duration should not be too long to prevent timeout issues");
    }
}
