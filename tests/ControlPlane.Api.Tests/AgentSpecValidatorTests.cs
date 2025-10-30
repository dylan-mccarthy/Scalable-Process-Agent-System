using ControlPlane.Api.Models;
using ControlPlane.Api.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ControlPlane.Api.Tests;

public class AgentSpecValidatorTests
{
    private readonly IAgentSpecValidator _validator;

    public AgentSpecValidatorTests()
    {
        var mockLogger = new Mock<ILogger<AgentSpecValidator>>();
        _validator = new AgentSpecValidator(mockLogger.Object);
    }

    [Fact]
    public void Validate_WithNullSpec_ReturnsValid()
    {
        // Act
        var result = _validator.Validate(null);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WithValidSpec_ReturnsValid()
    {
        // Arrange
        var spec = new Agent
        {
            AgentId = "test-agent",
            Name = "Test Agent",
            Instructions = "Test instructions",
            Budget = new AgentBudget
            {
                MaxTokens = 4000,
                MaxDurationSeconds = 60
            }
        };

        // Act
        var result = _validator.Validate(spec);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WithMissingName_ReturnsInvalid()
    {
        // Arrange
        var spec = new Agent
        {
            AgentId = "test-agent",
            Name = "",
            Instructions = "Test instructions"
        };

        // Act
        var result = _validator.Validate(spec);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("name is required"));
    }

    [Fact]
    public void Validate_WithMissingInstructions_ReturnsInvalid()
    {
        // Arrange
        var spec = new Agent
        {
            AgentId = "test-agent",
            Name = "Test Agent",
            Instructions = ""
        };

        // Act
        var result = _validator.Validate(spec);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("instructions are required"));
    }

    [Fact]
    public void Validate_WithWhitespaceName_ReturnsInvalid()
    {
        // Arrange
        var spec = new Agent
        {
            AgentId = "test-agent",
            Name = "   ",
            Instructions = "Test instructions"
        };

        // Act
        var result = _validator.Validate(spec);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("name is required"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_WithInvalidMaxTokens_ReturnsInvalid(int maxTokens)
    {
        // Arrange
        var spec = new Agent
        {
            AgentId = "test-agent",
            Name = "Test Agent",
            Instructions = "Test instructions",
            Budget = new AgentBudget { MaxTokens = maxTokens }
        };

        // Act
        var result = _validator.Validate(spec);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("MaxTokens must be greater than 0"));
    }

    [Fact]
    public void Validate_WithMaxTokensExceedingLimit_ReturnsInvalid()
    {
        // Arrange
        var spec = new Agent
        {
            AgentId = "test-agent",
            Name = "Test Agent",
            Instructions = "Test instructions",
            Budget = new AgentBudget { MaxTokens = 150000 }
        };

        // Act
        var result = _validator.Validate(spec);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("MaxTokens cannot exceed 128000"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(4000)]
    [InlineData(128000)]
    public void Validate_WithValidMaxTokens_ReturnsValid(int maxTokens)
    {
        // Arrange
        var spec = new Agent
        {
            AgentId = "test-agent",
            Name = "Test Agent",
            Instructions = "Test instructions",
            Budget = new AgentBudget { MaxTokens = maxTokens }
        };

        // Act
        var result = _validator.Validate(spec);

        // Assert
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-60)]
    public void Validate_WithInvalidMaxDuration_ReturnsInvalid(int maxDuration)
    {
        // Arrange
        var spec = new Agent
        {
            AgentId = "test-agent",
            Name = "Test Agent",
            Instructions = "Test instructions",
            Budget = new AgentBudget { MaxDurationSeconds = maxDuration }
        };

        // Act
        var result = _validator.Validate(spec);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("MaxDurationSeconds must be greater than 0"));
    }

    [Fact]
    public void Validate_WithMaxDurationExceedingLimit_ReturnsInvalid()
    {
        // Arrange
        var spec = new Agent
        {
            AgentId = "test-agent",
            Name = "Test Agent",
            Instructions = "Test instructions",
            Budget = new AgentBudget { MaxDurationSeconds = 7200 }
        };

        // Act
        var result = _validator.Validate(spec);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("MaxDurationSeconds cannot exceed 3600"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(60)]
    [InlineData(300)]
    [InlineData(3600)]
    public void Validate_WithValidMaxDuration_ReturnsValid(int maxDuration)
    {
        // Arrange
        var spec = new Agent
        {
            AgentId = "test-agent",
            Name = "Test Agent",
            Instructions = "Test instructions",
            Budget = new AgentBudget { MaxDurationSeconds = maxDuration }
        };

        // Act
        var result = _validator.Validate(spec);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithMissingConnectorType_ReturnsInvalid()
    {
        // Arrange
        var spec = new Agent
        {
            AgentId = "test-agent",
            Name = "Test Agent",
            Instructions = "Test instructions",
            Input = new ConnectorConfiguration
            {
                Type = ""
            }
        };

        // Act
        var result = _validator.Validate(spec);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Input connector type is required"));
    }

    [Theory]
    [InlineData("service-bus")]
    [InlineData("http")]
    [InlineData("kafka")]
    [InlineData("storage")]
    [InlineData("sql")]
    [InlineData("SERVICE-BUS")] // Case insensitive
    [InlineData("HTTP")]
    public void Validate_WithValidConnectorTypes_ReturnsValid(string connectorType)
    {
        // Arrange
        var spec = new Agent
        {
            AgentId = "test-agent",
            Name = "Test Agent",
            Instructions = "Test instructions",
            Input = new ConnectorConfiguration { Type = connectorType }
        };

        // Act
        var result = _validator.Validate(spec);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithInvalidConnectorType_ReturnsInvalid()
    {
        // Arrange
        var spec = new Agent
        {
            AgentId = "test-agent",
            Name = "Test Agent",
            Instructions = "Test instructions",
            Output = new ConnectorConfiguration
            {
                Type = "invalid-connector"
            }
        };

        // Act
        var result = _validator.Validate(spec);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Output connector type 'invalid-connector' is not recognized"));
    }

    [Fact]
    public void Validate_WithValidInputAndOutputConnectors_ReturnsValid()
    {
        // Arrange
        var spec = new Agent
        {
            AgentId = "test-agent",
            Name = "Test Agent",
            Instructions = "Test instructions",
            Input = new ConnectorConfiguration { Type = "service-bus" },
            Output = new ConnectorConfiguration { Type = "http" }
        };

        // Act
        var result = _validator.Validate(spec);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithDuplicateTools_ReturnsInvalid()
    {
        // Arrange
        var spec = new Agent
        {
            AgentId = "test-agent",
            Name = "Test Agent",
            Instructions = "Test instructions",
            Tools = new List<string> { "http-post", "calculator", "http-post" }
        };

        // Act
        var result = _validator.Validate(spec);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Duplicate tools found"));
        Assert.Contains(result.Errors, e => e.Contains("http-post"));
    }

    [Fact]
    public void Validate_WithEmptyToolName_ReturnsInvalid()
    {
        // Arrange
        var spec = new Agent
        {
            AgentId = "test-agent",
            Name = "Test Agent",
            Instructions = "Test instructions",
            Tools = new List<string> { "http-post", "", "calculator" }
        };

        // Act
        var result = _validator.Validate(spec);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Tools list contains empty or whitespace-only entries"));
    }

    [Fact]
    public void Validate_WithValidTools_ReturnsValid()
    {
        // Arrange
        var spec = new Agent
        {
            AgentId = "test-agent",
            Name = "Test Agent",
            Instructions = "Test instructions",
            Tools = new List<string> { "http-post", "calculator", "email" }
        };

        // Act
        var result = _validator.Validate(spec);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithMultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var spec = new Agent
        {
            AgentId = "test-agent",
            Name = "",
            Instructions = "",
            Budget = new AgentBudget
            {
                MaxTokens = -1,
                MaxDurationSeconds = 5000
            },
            Input = new ConnectorConfiguration { Type = "invalid-type" },
            Tools = new List<string> { "tool1", "tool1", "" }
        };

        // Act
        var result = _validator.Validate(spec);

        // Assert
        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 6, $"Expected at least 6 errors, got {result.Errors.Count}");
        Assert.Contains(result.Errors, e => e.Contains("name is required"));
        Assert.Contains(result.Errors, e => e.Contains("instructions are required"));
        Assert.Contains(result.Errors, e => e.Contains("MaxTokens must be greater than 0"));
        Assert.Contains(result.Errors, e => e.Contains("MaxDurationSeconds cannot exceed 3600"));
        Assert.Contains(result.Errors, e => e.Contains("connector type 'invalid-type' is not recognized"));
        Assert.Contains(result.Errors, e => e.Contains("Duplicate tools"));
    }

    [Fact]
    public void Validate_WithCompleteValidSpec_ReturnsValid()
    {
        // Arrange - Invoice Classifier example from SAD
        var spec = new Agent
        {
            AgentId = "invoice-classifier",
            Name = "Invoice Classifier",
            Description = "Classifies vendor invoices",
            Instructions = "Classify vendor + route to appropriate API endpoint",
            ModelProfile = new Dictionary<string, object> { { "model", "gpt-4" } },
            Budget = new AgentBudget
            {
                MaxTokens = 4000,
                MaxDurationSeconds = 60
            },
            Tools = new List<string> { "http-post" },
            Input = new ConnectorConfiguration
            {
                Type = "service-bus",
                Config = new Dictionary<string, object>
                {
                    { "queue", "invoices" }
                }
            },
            Output = new ConnectorConfiguration
            {
                Type = "http",
                Config = new Dictionary<string, object>
                {
                    { "baseUrl", "https://api.example.com/invoices" }
                }
            },
            Metadata = new Dictionary<string, string>
            {
                { "team", "finance" },
                { "environment", "production" }
            }
        };

        // Act
        var result = _validator.Validate(spec);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
