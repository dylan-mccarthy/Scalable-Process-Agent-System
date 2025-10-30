using Grpc.Core;
using ControlPlane.Api.Grpc;

namespace ControlPlane.Api.Grpc;

/// <summary>
/// gRPC service implementation for LeaseService
/// </summary>
public class LeaseServiceImpl : LeaseService.LeaseServiceBase
{
    private readonly Services.ILeaseService _leaseService;
    private readonly ILogger<LeaseServiceImpl> _logger;

    public LeaseServiceImpl(Services.ILeaseService leaseService, ILogger<LeaseServiceImpl> logger)
    {
        _leaseService = leaseService;
        _logger = logger;
    }

    public override async Task Pull(PullRequest request, IServerStreamWriter<Lease> responseStream, ServerCallContext context)
    {
        _logger.LogInformation("Pull request received from node {NodeId} for {MaxLeases} leases", 
            request.NodeId, request.MaxLeases);

        try
        {
            if (string.IsNullOrWhiteSpace(request.NodeId))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "NodeId is required"));
            }

            if (request.MaxLeases <= 0)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "MaxLeases must be greater than 0"));
            }

            await foreach (var lease in _leaseService.GetLeasesAsync(request.NodeId, request.MaxLeases, context.CancellationToken))
            {
                await responseStream.WriteAsync(lease);
                _logger.LogDebug("Sent lease {LeaseId} to node {NodeId}", lease.LeaseId, request.NodeId);
            }

            _logger.LogInformation("Pull stream completed for node {NodeId}", request.NodeId);
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Pull request for node {NodeId}", request.NodeId);
            throw new RpcException(new Status(StatusCode.Internal, "Internal server error"));
        }
    }

    public override async Task<AckResponse> Ack(AckRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Ack request received for lease {LeaseId} from node {NodeId}", 
            request.LeaseId, request.NodeId);

        try
        {
            if (string.IsNullOrWhiteSpace(request.LeaseId))
            {
                return new AckResponse
                {
                    Success = false,
                    Message = "LeaseId is required"
                };
            }

            if (string.IsNullOrWhiteSpace(request.NodeId))
            {
                return new AckResponse
                {
                    Success = false,
                    Message = "NodeId is required"
                };
            }

            var success = await _leaseService.AcknowledgeLeaseAsync(
                request.LeaseId, 
                request.NodeId, 
                request.AckTimestampUnixMs);

            return new AckResponse
            {
                Success = success,
                Message = success ? "Lease acknowledged" : "Failed to acknowledge lease"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Ack request for lease {LeaseId}", request.LeaseId);
            return new AckResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public override async Task<CompleteResponse> Complete(CompleteRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Complete request received for run {RunId} from node {NodeId}", 
            request.RunId, request.NodeId);

        try
        {
            if (string.IsNullOrWhiteSpace(request.LeaseId))
            {
                return new CompleteResponse
                {
                    Success = false,
                    Message = "LeaseId is required"
                };
            }

            if (string.IsNullOrWhiteSpace(request.RunId))
            {
                return new CompleteResponse
                {
                    Success = false,
                    Message = "RunId is required"
                };
            }

            if (string.IsNullOrWhiteSpace(request.NodeId))
            {
                return new CompleteResponse
                {
                    Success = false,
                    Message = "NodeId is required"
                };
            }

            var success = await _leaseService.CompleteRunAsync(
                request.LeaseId,
                request.RunId,
                request.NodeId,
                request.Result,
                request.Timings,
                request.Costs);

            return new CompleteResponse
            {
                Success = success,
                Message = success ? "Run completed successfully" : "Failed to complete run"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Complete request for run {RunId}", request.RunId);
            return new CompleteResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }

    public override async Task<FailResponse> Fail(FailRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Fail request received for run {RunId} from node {NodeId}: {ErrorMessage}", 
            request.RunId, request.NodeId, request.ErrorMessage);

        try
        {
            if (string.IsNullOrWhiteSpace(request.LeaseId))
            {
                return new FailResponse
                {
                    Success = false,
                    Message = "LeaseId is required",
                    ShouldRetry = false
                };
            }

            if (string.IsNullOrWhiteSpace(request.RunId))
            {
                return new FailResponse
                {
                    Success = false,
                    Message = "RunId is required",
                    ShouldRetry = false
                };
            }

            if (string.IsNullOrWhiteSpace(request.NodeId))
            {
                return new FailResponse
                {
                    Success = false,
                    Message = "NodeId is required",
                    ShouldRetry = false
                };
            }

            var (success, shouldRetry) = await _leaseService.FailRunAsync(
                request.LeaseId,
                request.RunId,
                request.NodeId,
                request.ErrorMessage,
                request.ErrorDetails,
                request.Timings,
                request.Retryable);

            return new FailResponse
            {
                Success = success,
                Message = success ? "Run failure processed" : "Failed to process run failure",
                ShouldRetry = shouldRetry
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Fail request for run {RunId}", request.RunId);
            return new FailResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                ShouldRetry = false
            };
        }
    }
}
