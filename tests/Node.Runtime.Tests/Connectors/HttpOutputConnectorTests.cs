using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Node.Runtime.Configuration;
using Node.Runtime.Connectors;
using System.Net;

namespace Node.Runtime.Tests.Connectors;

public sealed class HttpOutputConnectorTests : IDisposable
{
    private readonly Mock<ILogger<HttpOutputConnector>> _loggerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpOutputConnectorOptions _options;
    private readonly HttpClient _httpClient;

    public HttpOutputConnectorTests()
    {
        _loggerMock = new Mock<ILogger<HttpOutputConnector>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

        _options = new HttpOutputConnectorOptions
        {
            BaseUrl = "https://api.example.com",
            HttpMethod = "POST",
            MaxRetryAttempts = 3,
            BaseBackoffMs = 100,
            MaxBackoffMs = 5000,
            UseJitter = true,
            TimeoutSeconds = 30,
            IdempotencyKeyHeader = "Idempotency-Key"
        };

        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(_httpClient);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    [Fact]
    public void Constructor_WithValidOptions_ShouldSucceed()
    {
        // Act
        var connector = new HttpOutputConnector(
            Options.Create(_options),
            _httpClientFactoryMock.Object,
            _loggerMock.Object);

        // Assert
        connector.Should().NotBeNull();
        connector.ConnectorType.Should().Be("Http");
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => new HttpOutputConnector(
            null!,
            _httpClientFactoryMock.Object,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullHttpClientFactory_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => new HttpOutputConnector(
            Options.Create(_options),
            null!,
            _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var act = () => new HttpOutputConnector(
            Options.Create(_options),
            _httpClientFactoryMock.Object,
            null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task InitializeAsync_WithMissingBaseUrl_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invalidOptions = new HttpOutputConnectorOptions
        {
            BaseUrl = ""
        };

        var connector = new HttpOutputConnector(
            Options.Create(invalidOptions),
            _httpClientFactoryMock.Object,
            _loggerMock.Object);

        // Act & Assert
        var act = async () => await connector.InitializeAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*base URL*");
    }

    [Fact]
    public async Task InitializeAsync_WithInvalidBaseUrl_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var invalidOptions = new HttpOutputConnectorOptions
        {
            BaseUrl = "not-a-valid-url"
        };

        var connector = new HttpOutputConnector(
            Options.Create(invalidOptions),
            _httpClientFactoryMock.Object,
            _loggerMock.Object);

        // Act & Assert
        var act = async () => await connector.InitializeAsync();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Invalid base URL*");
    }

    [Fact]
    public async Task InitializeAsync_WithValidOptions_ShouldSucceed()
    {
        // Arrange
        var connector = new HttpOutputConnector(
            Options.Create(_options),
            _httpClientFactoryMock.Object,
            _loggerMock.Object);

        // Act
        await connector.InitializeAsync();

        // Assert
        _httpClientFactoryMock.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_CalledTwice_ShouldLogWarning()
    {
        // Arrange
        var connector = new HttpOutputConnector(
            Options.Create(_options),
            _httpClientFactoryMock.Object,
            _loggerMock.Object);

        // Act
        await connector.InitializeAsync();
        await connector.InitializeAsync();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("already initialized")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendMessageAsync_WithoutInitialization_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var connector = new HttpOutputConnector(
            Options.Create(_options),
            _httpClientFactoryMock.Object,
            _loggerMock.Object);

        var message = new OutgoingMessage
        {
            MessageId = "test-id",
            Body = "{\"test\":\"data\"}"
        };

        // Act & Assert
        var act = async () => await connector.SendMessageAsync(message);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not initialized*");
    }

    [Fact]
    public async Task SendMessageAsync_WithSuccessfulResponse_ShouldReturnSuccess()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK, "Success");

        var connector = new HttpOutputConnector(
            Options.Create(_options),
            _httpClientFactoryMock.Object,
            _loggerMock.Object);

        await connector.InitializeAsync();

        var message = new OutgoingMessage
        {
            MessageId = "test-id",
            Body = "{\"test\":\"data\"}",
            CorrelationId = "corr-123"
        };

        // Act
        var result = await connector.SendMessageAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(200);
        result.ResponseBody.Should().Be("Success");
        result.IsRetryable.Should().BeFalse();

        VerifyHttpRequest(Times.Once());
    }

    [Fact]
    public async Task SendMessageAsync_WithIdempotencyKey_ShouldIncludeHeader()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK, "Success");

        var connector = new HttpOutputConnector(
            Options.Create(_options),
            _httpClientFactoryMock.Object,
            _loggerMock.Object);

        await connector.InitializeAsync();

        var message = new OutgoingMessage
        {
            MessageId = "test-idempotency-key",
            Body = "{\"test\":\"data\"}"
        };

        // Act
        await connector.SendMessageAsync(message);

        // Assert
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Headers.Contains("Idempotency-Key") &&
                req.Headers.GetValues("Idempotency-Key").First() == "test-idempotency-key"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendMessageAsync_WithCorrelationId_ShouldIncludeHeader()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK, "Success");

        var connector = new HttpOutputConnector(
            Options.Create(_options),
            _httpClientFactoryMock.Object,
            _loggerMock.Object);

        await connector.InitializeAsync();

        var message = new OutgoingMessage
        {
            MessageId = "test-id",
            Body = "{\"test\":\"data\"}",
            CorrelationId = "correlation-123"
        };

        // Act
        await connector.SendMessageAsync(message);

        // Assert
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Headers.Contains("X-Correlation-ID") &&
                req.Headers.GetValues("X-Correlation-ID").First() == "correlation-123"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendMessageAsync_WithCustomHeaders_ShouldIncludeHeaders()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK, "Success");

        var connector = new HttpOutputConnector(
            Options.Create(_options),
            _httpClientFactoryMock.Object,
            _loggerMock.Object);

        await connector.InitializeAsync();

        var message = new OutgoingMessage
        {
            MessageId = "test-id",
            Body = "{\"test\":\"data\"}",
            Headers = new Dictionary<string, string>
            {
                { "X-Custom-Header", "custom-value" }
            }
        };

        // Act
        await connector.SendMessageAsync(message);

        // Assert
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Headers.Contains("X-Custom-Header") &&
                req.Headers.GetValues("X-Custom-Header").First() == "custom-value"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendMessageAsync_With4xxClientError_ShouldReturnFailureNonRetryable()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.BadRequest, "Bad Request");

        var connector = new HttpOutputConnector(
            Options.Create(_options),
            _httpClientFactoryMock.Object,
            _loggerMock.Object);

        await connector.InitializeAsync();

        var message = new OutgoingMessage
        {
            MessageId = "test-id",
            Body = "{\"test\":\"data\"}"
        };

        // Act
        var result = await connector.SendMessageAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.ResponseBody.Should().Be("Bad Request");
        result.IsRetryable.Should().BeFalse();

        // Should only be called once (no retries for 4xx)
        VerifyHttpRequest(Times.Once());
    }

    [Fact]
    public async Task SendMessageAsync_With5xxServerError_ShouldRetryAndReturnFailure()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.InternalServerError, "Server Error");

        var connector = new HttpOutputConnector(
            Options.Create(_options),
            _httpClientFactoryMock.Object,
            _loggerMock.Object);

        await connector.InitializeAsync();

        var message = new OutgoingMessage
        {
            MessageId = "test-id",
            Body = "{\"test\":\"data\"}"
        };

        // Act
        var result = await connector.SendMessageAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(500);
        result.IsRetryable.Should().BeTrue();

        // Should retry 3 times + initial attempt = 4 total
        VerifyHttpRequest(Times.Exactly(4));
    }

    [Fact]
    public async Task SendMessageAsync_With429TooManyRequests_ShouldRetry()
    {
        // Arrange
        SetupHttpResponse((HttpStatusCode)429, "Too Many Requests");

        var connector = new HttpOutputConnector(
            Options.Create(_options),
            _httpClientFactoryMock.Object,
            _loggerMock.Object);

        await connector.InitializeAsync();

        var message = new OutgoingMessage
        {
            MessageId = "test-id",
            Body = "{\"test\":\"data\"}"
        };

        // Act
        var result = await connector.SendMessageAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(429);
        result.IsRetryable.Should().BeTrue();

        // Should retry
        VerifyHttpRequest(Times.Exactly(4));
    }

    [Fact]
    public async Task SendMessageAsync_With408Timeout_ShouldRetry()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.RequestTimeout, "Request Timeout");

        var connector = new HttpOutputConnector(
            Options.Create(_options),
            _httpClientFactoryMock.Object,
            _loggerMock.Object);

        await connector.InitializeAsync();

        var message = new OutgoingMessage
        {
            MessageId = "test-id",
            Body = "{\"test\":\"data\"}"
        };

        // Act
        var result = await connector.SendMessageAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.StatusCode.Should().Be(408);
        result.IsRetryable.Should().BeTrue();

        // Should retry
        VerifyHttpRequest(Times.Exactly(4));
    }

    [Fact]
    public async Task SendMessageAsync_WithHttpRequestException_ShouldRetryAndReturnFailure()
    {
        // Arrange
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var connector = new HttpOutputConnector(
            Options.Create(_options),
            _httpClientFactoryMock.Object,
            _loggerMock.Object);

        await connector.InitializeAsync();

        var message = new OutgoingMessage
        {
            MessageId = "test-id",
            Body = "{\"test\":\"data\"}"
        };

        // Act
        var result = await connector.SendMessageAsync(message);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Network error");
        result.IsRetryable.Should().BeTrue();

        // Should retry
        VerifyHttpRequest(Times.Exactly(4));
    }

    [Fact]
    public async Task CloseAsync_WithoutInitialization_ShouldNotThrow()
    {
        // Arrange
        var connector = new HttpOutputConnector(
            Options.Create(_options),
            _httpClientFactoryMock.Object,
            _loggerMock.Object);

        // Act & Assert
        var act = async () => await connector.CloseAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CloseAsync_AfterInitialization_ShouldSucceed()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK, "Success");

        var connector = new HttpOutputConnector(
            Options.Create(_options),
            _httpClientFactoryMock.Object,
            _loggerMock.Object);

        await connector.InitializeAsync();

        // Act & Assert
        var act = async () => await connector.CloseAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void ConnectorType_ShouldReturnHttp()
    {
        // Arrange
        var connector = new HttpOutputConnector(
            Options.Create(_options),
            _httpClientFactoryMock.Object,
            _loggerMock.Object);

        // Act & Assert
        connector.ConnectorType.Should().Be("Http");
    }

    [Fact]
    public void Options_DefaultValues_ShouldMatchSADSpecifications()
    {
        // Arrange & Act
        var options = new HttpOutputConnectorOptions();

        // Assert
        options.MaxRetryAttempts.Should().Be(3, "SAD specifies max 3 retry attempts");
        options.UseJitter.Should().BeTrue("SAD specifies jitter for retries");
        options.HttpMethod.Should().Be("POST", "SAD specifies HTTP POST");
        options.IdempotencyKeyHeader.Should().Be("Idempotency-Key", "Standard idempotency header");
    }

    [Fact]
    public async Task DisposeAsync_ShouldCloseConnector()
    {
        // Arrange
        var connector = new HttpOutputConnector(
            Options.Create(_options),
            _httpClientFactoryMock.Object,
            _loggerMock.Object);

        // Act & Assert
        var act = async () => await connector.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
    }

    private void VerifyHttpRequest(Times times)
    {
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            times,
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }
}
