using MediatR;
using StargateAPI.Business.Data;
using StargateAPI.Controllers;
using System.Text.Json;

namespace StargateAPI.Business.Logging
{
    /// NEW FILE: MediatR pipeline behavior that wraps ALL commands and queries.
    /// Chose IPipelineBehavior over per-handler logging because it automatically covers
    /// every request without modifying existing handlers — follows the Open/Closed Principle.
    /// Logs to the ProcessLog database table per exercise requirement (Task 5).
    public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IRequest<TResponse>
        where TResponse : BaseResponse
    {
        private readonly StargateContext _context;
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

        public LoggingBehavior(StargateContext context, ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var requestType = typeof(TRequest).Name;
            var requestData = JsonSerializer.Serialize(request);

            try
            {
                var response = await next();

                var log = new ProcessLog
                {
                    RequestType = requestType,
                    RequestData = requestData,
                    Success = response.Success,
                    ResponseCode = response.ResponseCode,
                    Timestamp = DateTime.UtcNow
                };
                await _context.ProcessLogs.AddAsync(log, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Request {RequestType} completed. Success: {Success}", requestType, response.Success);

                return response;
            }
            catch (Exception ex)
            {
                var log = new ProcessLog
                {
                    RequestType = requestType,
                    RequestData = requestData,
                    Success = false,
                    ErrorMessage = ex.Message,
                    StackTrace = ex.StackTrace,
                    ResponseCode = ex is BadHttpRequestException ? 400 : 500,
                    Timestamp = DateTime.UtcNow
                };

                // Inner try-catch: logging failures should never break the API itself
                try
                {
                    await _context.ProcessLogs.AddAsync(log, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (Exception logEx)
                {
                    _logger.LogError(logEx, "Failed to persist process log for {RequestType}.", requestType);
                }

                _logger.LogError(ex, "Request {RequestType} failed.", requestType);
                throw;
            }
        }
    }
}
