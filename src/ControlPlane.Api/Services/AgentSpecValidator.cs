using ControlPlane.Api.Models;

namespace ControlPlane.Api.Services;

/// <summary>
/// Validates agent specifications to ensure they are complete and correct.
/// </summary>
public interface IAgentSpecValidator
{
    /// <summary>
    /// Validates an agent specification and returns validation results.
    /// </summary>
    /// <param name="spec">The agent specification to validate.</param>
    /// <returns>A validation result containing any errors found.</returns>
    ValidationResult Validate(Agent? spec);
}

/// <summary>
/// Result of agent specification validation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the validation was successful.
    /// </summary>
    public bool IsValid => !Errors.Any();

    /// <summary>
    /// Gets the list of validation errors.
    /// </summary>
    public List<string> Errors { get; } = new();

    /// <summary>
    /// Adds an error to the validation result.
    /// </summary>
    public void AddError(string error)
    {
        Errors.Add(error);
    }
}

/// <summary>
/// Validates agent specifications for versioning.
/// </summary>
public class AgentSpecValidator : IAgentSpecValidator
{
    private readonly ILogger<AgentSpecValidator> _logger;

    public AgentSpecValidator(ILogger<AgentSpecValidator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public ValidationResult Validate(Agent? spec)
    {
        var result = new ValidationResult();

        // Allow null specs - they represent versions without specification changes
        if (spec == null)
        {
            return result;
        }

        // Validate required fields
        ValidateRequiredFields(spec, result);

        // Validate budget constraints
        ValidateBudget(spec.Budget, result);

        // Validate connector configurations
        ValidateConnectors(spec, result);

        // Validate tools
        ValidateTools(spec.Tools, result);

        if (!result.IsValid)
        {
            _logger.LogWarning("Agent specification validation failed with {ErrorCount} errors", result.Errors.Count);
        }

        return result;
    }

    private static void ValidateRequiredFields(Agent spec, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(spec.Name))
        {
            result.AddError("Agent name is required");
        }

        if (string.IsNullOrWhiteSpace(spec.Instructions))
        {
            result.AddError("Agent instructions are required");
        }
    }

    private static void ValidateBudget(AgentBudget? budget, ValidationResult result)
    {
        if (budget == null)
        {
            return;
        }

        if (budget.MaxTokens.HasValue && budget.MaxTokens.Value <= 0)
        {
            result.AddError("MaxTokens must be greater than 0");
        }

        if (budget.MaxTokens.HasValue && budget.MaxTokens.Value > 128000)
        {
            result.AddError("MaxTokens cannot exceed 128000");
        }

        if (budget.MaxDurationSeconds.HasValue && budget.MaxDurationSeconds.Value <= 0)
        {
            result.AddError("MaxDurationSeconds must be greater than 0");
        }

        if (budget.MaxDurationSeconds.HasValue && budget.MaxDurationSeconds.Value > 3600)
        {
            result.AddError("MaxDurationSeconds cannot exceed 3600 (1 hour)");
        }
    }

    private static void ValidateConnectors(Agent spec, ValidationResult result)
    {
        // Validate input connector
        if (spec.Input != null)
        {
            ValidateConnectorConfiguration(spec.Input, "Input", result);
        }

        // Validate output connector
        if (spec.Output != null)
        {
            ValidateConnectorConfiguration(spec.Output, "Output", result);
        }
    }

    private static void ValidateConnectorConfiguration(ConnectorConfiguration connector, string connectorType, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(connector.Type))
        {
            result.AddError($"{connectorType} connector type is required");
            return;
        }

        // Validate known connector types
        var validConnectorTypes = new[] { "service-bus", "http", "kafka", "storage", "sql" };
        if (!validConnectorTypes.Contains(connector.Type.ToLowerInvariant()))
        {
            result.AddError($"{connectorType} connector type '{connector.Type}' is not recognized. Valid types: {string.Join(", ", validConnectorTypes)}");
        }
    }

    private static void ValidateTools(List<string>? tools, ValidationResult result)
    {
        if (tools == null || !tools.Any())
        {
            return;
        }

        // Check for duplicate tools
        var duplicates = tools.GroupBy(t => t)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Any())
        {
            result.AddError($"Duplicate tools found: {string.Join(", ", duplicates)}");
        }

        // Validate tool names are not empty
        var emptyTools = tools.Where(string.IsNullOrWhiteSpace).ToList();
        if (emptyTools.Any())
        {
            result.AddError("Tools list contains empty or whitespace-only entries");
        }
    }
}
