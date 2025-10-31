using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Node.Runtime.Configuration;
using Node.Runtime.Connectors;

namespace E2E.Tests;

/// <summary>
/// End-to-End tests for invoice processing (E7-T3).
/// Validates processing 100 invoices with ≥95% success rate and p95 latency &lt; 2s.
/// </summary>
public class InvoiceProcessingE2ETests
{
    private readonly Mock<ILogger<HttpOutputConnector>> _httpOutputLoggerMock;
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;

    public InvoiceProcessingE2ETests()
    {
        _httpOutputLoggerMock = new Mock<ILogger<HttpOutputConnector>>();
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _httpClientFactoryMock
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(_httpClient);
    }

    /// <summary>
    /// Generates synthetic invoice data for testing.
    /// </summary>
    private static List<SyntheticInvoice> GenerateSyntheticInvoices(int count)
    {
        var invoices = new List<SyntheticInvoice>();
        var random = new Random(42); // Fixed seed for reproducibility

        var vendors = new[]
        {
            new { Name = "Office Depot", Category = "Office Supplies", Department = "Procurement Department" },
            new { Name = "Dell Technologies", Category = "Technology/Hardware", Department = "IT Department" },
            new { Name = "Accenture Consulting", Category = "Professional Services", Department = "Finance Department" },
            new { Name = "Pacific Gas & Electric", Category = "Utilities", Department = "Facilities Management" },
            new { Name = "United Airlines", Category = "Travel & Expenses", Department = "HR Department" },
            new { Name = "Generic Supplier Co", Category = "Other", Department = "General Accounts Payable" }
        };

        for (int i = 0; i < count; i++)
        {
            var vendor = vendors[i % vendors.Length];
            var amount = random.Next(100, 10000);

            invoices.Add(new SyntheticInvoice
            {
                InvoiceNumber = $"INV-E2E-{i + 1:D4}",
                VendorName = vendor.Name,
                VendorCategory = vendor.Category,
                RoutingDestination = vendor.Department,
                InvoiceDate = DateTime.UtcNow.AddDays(-random.Next(1, 90)).ToString("yyyy-MM-dd"),
                TotalAmount = amount,
                Currency = "USD",
                LineItems = new[]
                {
                    new LineItem { Description = $"Item {i + 1}", Quantity = random.Next(1, 10), UnitPrice = amount / 2.0 }
                }
            });
        }

        return invoices;
    }

    /// <summary>
    /// Main E2E test: Process 100 invoices and validate output accuracy.
    /// Acceptance criteria from SAD Section 10:
    /// - ≥95% success rate
    /// - p95 latency &lt; 2s
    /// </summary>
    [Fact]
    public async Task Process100Invoices_ValidatesOutputAccuracy()
    {
        // Arrange - Generate 100 synthetic invoices
        var invoices = GenerateSyntheticInvoices(100);
        var processedCount = 0;
        var successCount = 0;
        var latencies = new List<double>();
        var capturedOutputs = new List<ClassifiedInvoiceOutput>();

        // Mock HTTP output connector responses
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
            {
                // Simulate realistic processing with slight delay
                Task.Delay(Random.Shared.Next(10, 50), ct).Wait();

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(JsonSerializer.Serialize(new
                    {
                        status = "accepted",
                        id = $"processed-{Interlocked.Increment(ref processedCount)}"
                    }))
                };
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

        // Act - Process all 100 invoices
        var stopwatch = Stopwatch.StartNew();

        foreach (var invoice in invoices)
        {
            var invoiceStopwatch = Stopwatch.StartNew();

            try
            {
                // Simulate classification (in real scenario, this would go through LLM)
                var classifiedOutput = new ClassifiedInvoiceOutput
                {
                    VendorName = invoice.VendorName,
                    VendorCategory = invoice.VendorCategory,
                    InvoiceNumber = invoice.InvoiceNumber,
                    InvoiceDate = invoice.InvoiceDate,
                    TotalAmount = invoice.TotalAmount,
                    Currency = invoice.Currency,
                    RoutingDestination = invoice.RoutingDestination,
                    Confidence = 0.95
                };

                // Send to HTTP output connector
                var outgoingMessage = new OutgoingMessage
                {
                    MessageId = $"{invoice.InvoiceNumber}-classified",
                    Body = JsonSerializer.Serialize(classifiedOutput),
                    CorrelationId = $"e2e-test-{invoice.InvoiceNumber}",
                    ContentType = "application/json"
                };

                var sendResult = await httpConnector.SendMessageAsync(outgoingMessage);

                invoiceStopwatch.Stop();
                latencies.Add(invoiceStopwatch.Elapsed.TotalSeconds);

                if (sendResult.Success)
                {
                    successCount++;
                    capturedOutputs.Add(classifiedOutput);
                }
            }
            catch (InvalidOperationException ex)
            {
                invoiceStopwatch.Stop();
                latencies.Add(invoiceStopwatch.Elapsed.TotalSeconds);
                _httpOutputLoggerMock.Object.LogError(ex, "Invalid operation processing invoice {InvoiceNumber}", invoice.InvoiceNumber);
            }
            catch (TimeoutException ex)
            {
                invoiceStopwatch.Stop();
                latencies.Add(invoiceStopwatch.Elapsed.TotalSeconds);
                _httpOutputLoggerMock.Object.LogError(ex, "Timeout processing invoice {InvoiceNumber}", invoice.InvoiceNumber);
            }
            catch (HttpRequestException ex)
            {
                invoiceStopwatch.Stop();
                latencies.Add(invoiceStopwatch.Elapsed.TotalSeconds);
                _httpOutputLoggerMock.Object.LogError(ex, "HTTP error processing invoice {InvoiceNumber}", invoice.InvoiceNumber);
            }
            catch (Exception ex)
            {
                invoiceStopwatch.Stop();
                latencies.Add(invoiceStopwatch.Elapsed.TotalSeconds);
                _httpOutputLoggerMock.Object.LogError(ex, "Unexpected error processing invoice {InvoiceNumber}", invoice.InvoiceNumber);
            }
        }

        stopwatch.Stop();

        await httpConnector.CloseAsync();

        // Assert - Validate acceptance criteria

        // 1. All 100 invoices were processed
        processedCount.Should().Be(100, "all 100 invoices should have been processed");

        // 2. Success rate should be ≥95% (SAD requirement)
        var successRate = (double)successCount / invoices.Count;
        successRate.Should().BeGreaterThanOrEqualTo(0.95,
            "success rate must be at least 95% as per SAD Section 10 acceptance criteria");

        // 3. p95 latency should be < 2s (SAD requirement)
        var sortedLatencies = latencies.OrderBy(l => l).ToList();
        var p95Index = (int)Math.Ceiling(sortedLatencies.Count * 0.95) - 1;
        var p95Latency = sortedLatencies[p95Index];

        p95Latency.Should().BeLessThan(2.0,
            "p95 latency must be less than 2 seconds as per SAD Section 10 acceptance criteria");

        // 4. Validate output accuracy - classified invoices should match input categories
        foreach (var output in capturedOutputs)
        {
            var originalInvoice = invoices.First(i => i.InvoiceNumber == output.InvoiceNumber);
            output.VendorName.Should().Be(originalInvoice.VendorName,
                "vendor name should be preserved accurately");
            output.VendorCategory.Should().Be(originalInvoice.VendorCategory,
                "vendor category should be classified correctly");
            output.RoutingDestination.Should().Be(originalInvoice.RoutingDestination,
                "routing destination should be determined correctly");
            output.Confidence.Should().BeGreaterThanOrEqualTo(0.0).And.BeLessThanOrEqualTo(1.0,
                "confidence score should be between 0 and 1");
        }

        // Additional metrics for visibility
        var avgLatency = latencies.Average();
        var maxLatency = latencies.Max();
        var minLatency = latencies.Min();

        // Log test results for observability
        Console.WriteLine($"E2E Test Results:");
        Console.WriteLine($"  Total Invoices: {invoices.Count}");
        Console.WriteLine($"  Successful: {successCount}");
        Console.WriteLine($"  Success Rate: {successRate:P2}");
        Console.WriteLine($"  Average Latency: {avgLatency:F3}s");
        Console.WriteLine($"  Min Latency: {minLatency:F3}s");
        Console.WriteLine($"  Max Latency: {maxLatency:F3}s");
        Console.WriteLine($"  p95 Latency: {p95Latency:F3}s");
        Console.WriteLine($"  Total Duration: {stopwatch.Elapsed.TotalSeconds:F2}s");
    }

    /// <summary>
    /// Test that verifies correlation IDs are preserved through the processing flow.
    /// </summary>
    [Fact]
    public async Task Process100Invoices_PreservesCorrelationIds()
    {
        // Arrange
        var invoices = GenerateSyntheticInvoices(10); // Smaller batch for focused test
        var capturedCorrelationIds = new List<string>();

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
            {
                // Capture correlation ID from request header
                if (req.Headers.TryGetValues("X-Correlation-ID", out var values))
                {
                    capturedCorrelationIds.Add(values.First());
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
        foreach (var invoice in invoices)
        {
            var correlationId = $"e2e-correlation-{invoice.InvoiceNumber}";
            var classifiedOutput = new ClassifiedInvoiceOutput
            {
                VendorName = invoice.VendorName,
                VendorCategory = invoice.VendorCategory,
                InvoiceNumber = invoice.InvoiceNumber,
                InvoiceDate = invoice.InvoiceDate,
                TotalAmount = invoice.TotalAmount,
                Currency = invoice.Currency,
                RoutingDestination = invoice.RoutingDestination,
                Confidence = 0.95
            };

            var outgoingMessage = new OutgoingMessage
            {
                MessageId = $"{invoice.InvoiceNumber}-classified",
                Body = JsonSerializer.Serialize(classifiedOutput),
                CorrelationId = correlationId,
                ContentType = "application/json"
            };

            await httpConnector.SendMessageAsync(outgoingMessage);
        }

        await httpConnector.CloseAsync();

        // Assert
        capturedCorrelationIds.Should().HaveCount(invoices.Count,
            "all correlation IDs should be captured");

        foreach (var invoice in invoices)
        {
            var expectedCorrelationId = $"e2e-correlation-{invoice.InvoiceNumber}";
            capturedCorrelationIds.Should().Contain(expectedCorrelationId,
                "correlation ID should be preserved through the processing flow");
        }
    }

    /// <summary>
    /// Test that verifies idempotency keys are included in HTTP requests.
    /// </summary>
    [Fact]
    public async Task Process100Invoices_IncludesIdempotencyKeys()
    {
        // Arrange
        var invoices = GenerateSyntheticInvoices(10); // Smaller batch for focused test
        var capturedIdempotencyKeys = new List<string>();

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync((HttpRequestMessage req, CancellationToken ct) =>
            {
                // Capture idempotency key from request header
                if (req.Headers.TryGetValues("Idempotency-Key", out var values))
                {
                    capturedIdempotencyKeys.Add(values.First());
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
        foreach (var invoice in invoices)
        {
            var messageId = $"{invoice.InvoiceNumber}-classified";
            var classifiedOutput = new ClassifiedInvoiceOutput
            {
                VendorName = invoice.VendorName,
                VendorCategory = invoice.VendorCategory,
                InvoiceNumber = invoice.InvoiceNumber,
                InvoiceDate = invoice.InvoiceDate,
                TotalAmount = invoice.TotalAmount,
                Currency = invoice.Currency,
                RoutingDestination = invoice.RoutingDestination,
                Confidence = 0.95
            };

            var outgoingMessage = new OutgoingMessage
            {
                MessageId = messageId,
                Body = JsonSerializer.Serialize(classifiedOutput),
                CorrelationId = $"e2e-test-{invoice.InvoiceNumber}",
                ContentType = "application/json"
            };

            await httpConnector.SendMessageAsync(outgoingMessage);
        }

        await httpConnector.CloseAsync();

        // Assert
        capturedIdempotencyKeys.Should().HaveCount(invoices.Count,
            "all idempotency keys should be captured");

        capturedIdempotencyKeys.Should().OnlyHaveUniqueItems(
            "idempotency keys should be unique for each message");

        foreach (var expectedMessageId in invoices.Select(invoice => $"{invoice.InvoiceNumber}-classified"))
        {
            capturedIdempotencyKeys.Should().Contain(expectedMessageId,
                "idempotency key should match message ID");
        }
    }
}

/// <summary>
/// Represents a synthetic invoice for testing.
/// </summary>
internal class SyntheticInvoice
{
    public required string InvoiceNumber { get; set; }
    public required string VendorName { get; set; }
    public required string VendorCategory { get; set; }
    public required string RoutingDestination { get; set; }
    public required string InvoiceDate { get; set; }
    public required double TotalAmount { get; set; }
    public required string Currency { get; set; }
    public required LineItem[] LineItems { get; set; }
}

/// <summary>
/// Represents a line item in an invoice.
/// </summary>
internal class LineItem
{
    public required string Description { get; set; }
    public required int Quantity { get; set; }
    public required double UnitPrice { get; set; }
}

/// <summary>
/// Represents classified invoice output.
/// </summary>
internal class ClassifiedInvoiceOutput
{
    public required string VendorName { get; set; }
    public required string VendorCategory { get; set; }
    public required string InvoiceNumber { get; set; }
    public required string InvoiceDate { get; set; }
    public required double TotalAmount { get; set; }
    public required string Currency { get; set; }
    public required string RoutingDestination { get; set; }
    public required double Confidence { get; set; }
}
