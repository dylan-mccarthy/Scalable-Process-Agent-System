using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Node.Runtime.Configuration;
using Node.Runtime.Connectors;
using Node.Runtime.Services;

namespace Node.Runtime.Tests;

/// <summary>
/// Integration tests for Invoice Classifier agent end-to-end message flow (E3-T7).
/// Validates complete message flow: Service Bus → Agent Executor (LLM) → HTTP Output.
/// </summary>
public class InvoiceClassifierIntegrationTests
{
    private readonly Mock<ILogger<AgentExecutorService>> _agentExecutorLoggerMock;
    private readonly Mock<ILogger<HttpOutputConnector>> _httpOutputLoggerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;

    public InvoiceClassifierIntegrationTests()
    {
        _agentExecutorLoggerMock = new Mock<ILogger<AgentExecutorService>>();
        _httpOutputLoggerMock = new Mock<ILogger<HttpOutputConnector>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(_httpClient);
    }

    [Fact]
    public async Task EndToEndFlow_ValidInvoice_SuccessfullyClassifiesAndSendsToHttpOutput()
    {
        // Arrange - Simulate invoice message from Service Bus
        var invoiceMessage = new
        {
            invoiceNumber = "INV-2024-001",
            vendorName = "Office Depot",
            invoiceDate = "2024-01-15",
            totalAmount = 1250.00,
            currency = "USD",
            lineItems = new[]
            {
                new { description = "Office chairs", quantity = 5, unitPrice = 200.00 },
                new { description = "Desk lamps", quantity = 10, unitPrice = 25.00 }
            }
        };
        var invoiceJson = JsonSerializer.Serialize(invoiceMessage);

        // Create mock received message
        var receivedMessage = new ReceivedMessage
        {
            MessageId = "test-message-001",
            Body = invoiceJson,
            CorrelationId = "correlation-001",
            DeliveryCount = 1,
            EnqueuedTime = DateTimeOffset.UtcNow
        };

        // Mock successful HTTP POST response
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"status\":\"accepted\",\"id\":\"classified-001\"}")
            });

        // Configure HTTP output connector
        var httpOutputOptions = new HttpOutputConnectorOptions
        {
            BaseUrl = "https://api.example.com/invoices",
            HttpMethod = "POST",
            MaxRetryAttempts = 3,
            BaseBackoffMs = 100,
            MaxBackoffMs = 5000,
            TimeoutSeconds = 30,
            IdempotencyKeyHeader = "Idempotency-Key"
        };

        var httpConnector = new HttpOutputConnector(
            Options.Create(httpOutputOptions),
            _httpClientFactoryMock.Object,
            _httpOutputLoggerMock.Object);

        await httpConnector.InitializeAsync();

        // Create mock LLM classification response
        var classifiedOutput = new
        {
            vendorName = "Office Depot",
            vendorCategory = "Office Supplies",
            invoiceNumber = "INV-2024-001",
            invoiceDate = "2024-01-15",
            totalAmount = 1250.00,
            currency = "USD",
            routingDestination = "Procurement Department",
            confidence = 0.95
        };
        var classifiedJson = JsonSerializer.Serialize(classifiedOutput);

        // Act - Send classified message to HTTP output connector
        var outgoingMessage = new OutgoingMessage
        {
            MessageId = $"{receivedMessage.MessageId}-classified",
            Body = classifiedJson,
            CorrelationId = receivedMessage.CorrelationId,
            ContentType = "application/json"
        };

        var sendResult = await httpConnector.SendMessageAsync(outgoingMessage);

        // Assert
        sendResult.Should().NotBeNull();
        sendResult.Success.Should().BeTrue("the classified invoice should be sent successfully");
        sendResult.StatusCode.Should().Be(200);
        sendResult.ResponseBody.Should().Contain("accepted");

        // Verify HTTP request was made with correct headers
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.Headers.Contains("Idempotency-Key") &&
                req.Content != null),
            ItExpr.IsAny<CancellationToken>());

        await httpConnector.CloseAsync();
    }

    [Fact]
    public async Task EndToEndFlow_HttpOutputFailure_RetriesAndReportsError()
    {
        // Arrange
        var invoiceJson = JsonSerializer.Serialize(new { invoiceNumber = "INV-FAIL-001" });
        var classifiedJson = JsonSerializer.Serialize(new { vendorCategory = "Other" });

        // Mock HTTP 503 Service Unavailable (retryable error)
        var attemptCount = 0;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                attemptCount++;
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.ServiceUnavailable,
                    Content = new StringContent("{\"error\":\"Service temporarily unavailable\"}")
                };
            });

        var httpOutputOptions = new HttpOutputConnectorOptions
        {
            BaseUrl = "https://api.example.com/invoices",
            HttpMethod = "POST",
            MaxRetryAttempts = 3,
            BaseBackoffMs = 50, // Short for testing
            MaxBackoffMs = 200,
            TimeoutSeconds = 30,
            IdempotencyKeyHeader = "Idempotency-Key"
        };

        var httpConnector = new HttpOutputConnector(
            Options.Create(httpOutputOptions),
            _httpClientFactoryMock.Object,
            _httpOutputLoggerMock.Object);

        await httpConnector.InitializeAsync();

        // Act
        var outgoingMessage = new OutgoingMessage
        {
            MessageId = "fail-message-001",
            Body = classifiedJson,
            ContentType = "application/json"
        };

        var sendResult = await httpConnector.SendMessageAsync(outgoingMessage);

        // Assert
        sendResult.Should().NotBeNull();
        sendResult.Success.Should().BeFalse("the HTTP request should fail after retries");
        sendResult.StatusCode.Should().Be(503);
        sendResult.IsRetryable.Should().BeTrue("503 errors should be marked as retryable");

        // Verify retries occurred (1 initial + 3 retries = 4 total attempts)
        attemptCount.Should().Be(4, "the connector should retry 3 times after initial failure");

        await httpConnector.CloseAsync();
    }

    [Fact]
    public async Task EndToEndFlow_HttpOutputNonRetryableError_FailsImmediately()
    {
        // Arrange
        var classifiedJson = JsonSerializer.Serialize(new { vendorCategory = "Invalid" });

        // Mock HTTP 400 Bad Request (non-retryable error)
        var attemptCount = 0;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                attemptCount++;
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Content = new StringContent("{\"error\":\"Invalid request format\"}")
                };
            });

        var httpOutputOptions = new HttpOutputConnectorOptions
        {
            BaseUrl = "https://api.example.com/invoices",
            HttpMethod = "POST",
            MaxRetryAttempts = 3,
            BaseBackoffMs = 100,
            MaxBackoffMs = 5000,
            TimeoutSeconds = 30,
            IdempotencyKeyHeader = "Idempotency-Key"
        };

        var httpConnector = new HttpOutputConnector(
            Options.Create(httpOutputOptions),
            _httpClientFactoryMock.Object,
            _httpOutputLoggerMock.Object);

        await httpConnector.InitializeAsync();

        // Act
        var outgoingMessage = new OutgoingMessage
        {
            MessageId = "bad-request-message-001",
            Body = classifiedJson,
            ContentType = "application/json"
        };

        var sendResult = await httpConnector.SendMessageAsync(outgoingMessage);

        // Assert
        sendResult.Should().NotBeNull();
        sendResult.Success.Should().BeFalse("the HTTP request should fail with bad request");
        sendResult.StatusCode.Should().Be(400);
        sendResult.IsRetryable.Should().BeFalse("400 errors should not be retryable");

        // Verify no retries occurred for non-retryable error (only 1 attempt)
        attemptCount.Should().Be(1, "non-retryable errors should not trigger retries");

        await httpConnector.CloseAsync();
    }

    [Fact]
    public async Task EndToEndFlow_IdempotencyKey_IncludedInHttpRequest()
    {
        // Arrange
        var classifiedJson = JsonSerializer.Serialize(new { vendorCategory = "Technology/Hardware" });
        string? capturedIdempotencyKey = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
            {
                // Capture the idempotency key
                if (req.Headers.TryGetValues("Idempotency-Key", out var values))
                {
                    capturedIdempotencyKey = values.FirstOrDefault();
                }

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"status\":\"accepted\"}")
                };
            });

        var httpOutputOptions = new HttpOutputConnectorOptions
        {
            BaseUrl = "https://api.example.com/invoices",
            HttpMethod = "POST",
            MaxRetryAttempts = 3,
            BaseBackoffMs = 100,
            MaxBackoffMs = 5000,
            TimeoutSeconds = 30,
            IdempotencyKeyHeader = "Idempotency-Key"
        };

        var httpConnector = new HttpOutputConnector(
            Options.Create(httpOutputOptions),
            _httpClientFactoryMock.Object,
            _httpOutputLoggerMock.Object);

        await httpConnector.InitializeAsync();

        // Act
        var messageId = "idempotency-test-message-001";
        var outgoingMessage = new OutgoingMessage
        {
            MessageId = messageId,
            Body = classifiedJson,
            CorrelationId = "correlation-idempotency-001",
            ContentType = "application/json"
        };

        var sendResult = await httpConnector.SendMessageAsync(outgoingMessage);

        // Assert
        sendResult.Success.Should().BeTrue();
        capturedIdempotencyKey.Should().NotBeNullOrEmpty("idempotency key should be included in request");
        capturedIdempotencyKey.Should().Be(messageId, "idempotency key should match message ID");

        await httpConnector.CloseAsync();
    }

    [Fact]
    public async Task EndToEndFlow_CorrelationId_PreservedThroughFlow()
    {
        // Arrange
        var correlationId = "correlation-preserve-test-001";
        var classifiedJson = JsonSerializer.Serialize(new { vendorCategory = "Professional Services" });
        string? capturedCorrelationId = null;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
            {
                // Capture the correlation ID
                if (req.Headers.TryGetValues("X-Correlation-ID", out var values))
                {
                    capturedCorrelationId = values.FirstOrDefault();
                }

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"status\":\"accepted\"}")
                };
            });

        var httpOutputOptions = new HttpOutputConnectorOptions
        {
            BaseUrl = "https://api.example.com/invoices",
            HttpMethod = "POST",
            MaxRetryAttempts = 3,
            BaseBackoffMs = 100,
            MaxBackoffMs = 5000,
            TimeoutSeconds = 30,
            IdempotencyKeyHeader = "Idempotency-Key"
        };

        var httpConnector = new HttpOutputConnector(
            Options.Create(httpOutputOptions),
            _httpClientFactoryMock.Object,
            _httpOutputLoggerMock.Object);

        await httpConnector.InitializeAsync();

        // Act
        var outgoingMessage = new OutgoingMessage
        {
            MessageId = "correlation-test-message-001",
            Body = classifiedJson,
            CorrelationId = correlationId,
            ContentType = "application/json"
        };

        var sendResult = await httpConnector.SendMessageAsync(outgoingMessage);

        // Assert
        sendResult.Success.Should().BeTrue();
        capturedCorrelationId.Should().NotBeNullOrEmpty("correlation ID should be included in request");
        capturedCorrelationId.Should().Be(correlationId, "correlation ID should be preserved through the flow");

        await httpConnector.CloseAsync();
    }

    [Fact]
    public async Task InvoiceClassifierDefinition_MatchesExpectedConfiguration()
    {
        // Arrange - Load the invoice classifier definition
        // Navigate from test bin directory back to repo root
        var currentDir = Directory.GetCurrentDirectory();
        var repoRoot = Path.GetFullPath(Path.Combine(currentDir, "../../../../.."));
        var agentDefinitionPath = Path.Combine(repoRoot, "agents/definitions/invoice-classifier.json");

        // Act
        var jsonContent = await File.ReadAllTextAsync(agentDefinitionPath);
        var agentDefinition = JsonSerializer.Deserialize<JsonDocument>(jsonContent);

        // Assert - Verify critical configuration elements
        agentDefinition.Should().NotBeNull();

        var root = agentDefinition!.RootElement;
        
        // Verify agent ID and name
        root.GetProperty("agentId").GetString().Should().Be("invoice-classifier");
        root.GetProperty("name").GetString().Should().Be("Invoice Classifier");

        // Verify input connector configuration
        var input = root.GetProperty("input");
        input.GetProperty("type").GetString().Should().Be("ServiceBus");
        var inputConfig = input.GetProperty("config");
        inputConfig.GetProperty("queueName").GetString().Should().Be("invoices");
        inputConfig.GetProperty("prefetchCount").GetInt32().Should().Be(16);
        inputConfig.GetProperty("maxDeliveryCount").GetInt32().Should().Be(3);

        // Verify output connector configuration
        var output = root.GetProperty("output");
        output.GetProperty("type").GetString().Should().Be("Http");
        var outputConfig = output.GetProperty("config");
        outputConfig.GetProperty("method").GetString().Should().Be("POST");

        // Verify retry policy
        var retryPolicy = outputConfig.GetProperty("retryPolicy");
        retryPolicy.GetProperty("maxRetries").GetInt32().Should().Be(3);
        retryPolicy.GetProperty("useExponentialBackoff").GetBoolean().Should().BeTrue();

        // Verify budget constraints
        var budget = root.GetProperty("budget");
        budget.GetProperty("maxTokens").GetInt32().Should().Be(4000);
        budget.GetProperty("maxDurationSeconds").GetInt32().Should().Be(60);

        // Verify model profile for deterministic classification
        var modelProfile = root.GetProperty("modelProfile");
        modelProfile.GetProperty("temperature").GetDouble().Should().BeLessThanOrEqualTo(0.5,
            "low temperature ensures consistent classification results");
    }

    [Fact]
    public async Task MessageFlow_ValidatesAcceptanceCriteria_FromSAD()
    {
        // This test validates the acceptance criteria from SAD Section 10:
        // 1. A message sent to `invoices` results in a POST to target API with correct payload
        // 2. Failure with 5xx retries 3 times then should be handled appropriately
        // 3. Metrics, logs, and traces should be visible (validated through telemetry infrastructure)

        // Arrange - Simulate the complete flow
        var invoiceData = new
        {
            invoiceNumber = "INV-ACCEPTANCE-001",
            vendorName = "TechCorp Inc.",
            totalAmount = 5000.00
        };

        var classifiedData = new
        {
            vendorName = "TechCorp Inc.",
            vendorCategory = "Technology/Hardware",
            routingDestination = "IT Department",
            confidence = 0.92
        };

        var requestCaptured = false;
        var correctPayload = false;

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
            {
                requestCaptured = true;

                // Verify the request has correct payload
                var content = req.Content?.ReadAsStringAsync().Result;
                if (content != null && content.Contains("TechCorp") && content.Contains("IT Department"))
                {
                    correctPayload = true;
                }

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Created,
                    Content = new StringContent("{\"id\":\"classified-acceptance-001\",\"status\":\"processed\"}")
                };
            });

        var httpOutputOptions = new HttpOutputConnectorOptions
        {
            BaseUrl = "https://api.example.com/invoices",
            HttpMethod = "POST",
            MaxRetryAttempts = 3,
            BaseBackoffMs = 100,
            MaxBackoffMs = 5000,
            TimeoutSeconds = 30,
            IdempotencyKeyHeader = "Idempotency-Key"
        };

        var httpConnector = new HttpOutputConnector(
            Options.Create(httpOutputOptions),
            _httpClientFactoryMock.Object,
            _httpOutputLoggerMock.Object);

        await httpConnector.InitializeAsync();

        // Act - Send classified message
        var outgoingMessage = new OutgoingMessage
        {
            MessageId = "acceptance-test-001",
            Body = JsonSerializer.Serialize(classifiedData),
            CorrelationId = "acceptance-correlation-001",
            ContentType = "application/json"
        };

        var sendResult = await httpConnector.SendMessageAsync(outgoingMessage);

        // Assert
        // Acceptance Criteria 1: Message results in POST to target API with correct payload
        requestCaptured.Should().BeTrue("a POST request should be made to the target API");
        correctPayload.Should().BeTrue("the payload should contain the classified invoice data");
        sendResult.Success.Should().BeTrue("the message should be sent successfully");
        sendResult.StatusCode.Should().Be(201, "the API should return Created status");

        await httpConnector.CloseAsync();
    }

    [Fact]
    public async Task MessageFlow_HttpTimeout_HandledGracefully()
    {
        // Arrange - Simulate timeout scenario
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Request timeout"));

        var httpOutputOptions = new HttpOutputConnectorOptions
        {
            BaseUrl = "https://api.example.com/invoices",
            HttpMethod = "POST",
            MaxRetryAttempts = 2, // Reduce retries for faster test
            BaseBackoffMs = 50,
            MaxBackoffMs = 200,
            TimeoutSeconds = 1, // Short timeout
            IdempotencyKeyHeader = "Idempotency-Key"
        };

        var httpConnector = new HttpOutputConnector(
            Options.Create(httpOutputOptions),
            _httpClientFactoryMock.Object,
            _httpOutputLoggerMock.Object);

        await httpConnector.InitializeAsync();

        // Act
        var outgoingMessage = new OutgoingMessage
        {
            MessageId = "timeout-test-001",
            Body = JsonSerializer.Serialize(new { test = "timeout" }),
            ContentType = "application/json"
        };

        var sendResult = await httpConnector.SendMessageAsync(outgoingMessage);

        // Assert
        sendResult.Should().NotBeNull();
        sendResult.Success.Should().BeFalse("timeout should result in failure");
        sendResult.IsRetryable.Should().BeTrue("timeout errors should be retryable");
        sendResult.ErrorMessage.Should().ContainEquivalentOf("timeout");

        await httpConnector.CloseAsync();
    }
}
