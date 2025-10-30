using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Node.Runtime.Configuration;
using Node.Runtime.Services;
using System.Net;
using System.Text.Json;

namespace Node.Runtime.Tests.Services;

public sealed class NodeRegistrationServiceTests
{
    private readonly Mock<ILogger<NodeRegistrationService>> _loggerMock;
    private readonly NodeRuntimeOptions _options;

    public NodeRegistrationServiceTests()
    {
        _loggerMock = new Mock<ILogger<NodeRegistrationService>>();
        _options = new NodeRuntimeOptions
        {
            NodeId = "test-node",
            Metadata = new Dictionary<string, string>
            {
                ["Region"] = "us-east-1",
                ["Environment"] = "test"
            },
            Capacity = new NodeCapacity
            {
                Slots = 8,
                Cpu = "4",
                Memory = "8Gi"
            }
        };
    }

    [Fact]
    public async Task RegisterNodeAsync_Success_ReturnsTrue()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.PathAndQuery == "/v1/nodes:register"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}")
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5109")
        };

        var service = new NodeRegistrationService(
            httpClient,
            Options.Create(_options),
            _loggerMock.Object);

        // Act
        var result = await service.RegisterNodeAsync();

        // Assert
        result.Should().BeTrue();
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task RegisterNodeAsync_Failure_ReturnsFalse()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("Invalid request")
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5109")
        };

        var service = new NodeRegistrationService(
            httpClient,
            Options.Create(_options),
            _loggerMock.Object);

        // Act
        var result = await service.RegisterNodeAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendHeartbeatAsync_Success_ReturnsTrue()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.PathAndQuery == $"/v1/nodes/{_options.NodeId}:heartbeat"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}")
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5109")
        };

        var service = new NodeRegistrationService(
            httpClient,
            Options.Create(_options),
            _loggerMock.Object);

        // Act
        var result = await service.SendHeartbeatAsync(2, 6);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SendHeartbeatAsync_Failure_ReturnsFalse()
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound,
                Content = new StringContent("Node not found")
            });

        var httpClient = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:5109")
        };

        var service = new NodeRegistrationService(
            httpClient,
            Options.Create(_options),
            _loggerMock.Object);

        // Act
        var result = await service.SendHeartbeatAsync(2, 6);

        // Assert
        result.Should().BeFalse();
    }
}
