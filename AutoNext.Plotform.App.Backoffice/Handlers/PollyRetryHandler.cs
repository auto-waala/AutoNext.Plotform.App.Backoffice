using Polly;
using Polly.Retry;

namespace AutoNext.Plotform.App.Backoffice.Handlers
{
    public class PollyRetryHandler : DelegatingHandler
    {
        private readonly ILogger<PollyRetryHandler> _logger;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

        public PollyRetryHandler(ILogger<PollyRetryHandler> logger)
        {
            _logger = logger;

            _retryPolicy = Policy.HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode && (
                           r.StatusCode == System.Net.HttpStatusCode.InternalServerError ||
                           r.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                           r.StatusCode == System.Net.HttpStatusCode.BadGateway ||
                           r.StatusCode == System.Net.HttpStatusCode.GatewayTimeout)).Or<HttpRequestException>()
                            .WaitAndRetryAsync(
                                retryCount: 3,
                                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                                onRetry: (outcome, timespan, retryCount, context) =>
                                {
                                    _logger.LogWarning(
                                        "Retry {RetryCount} after {Delay}s due to: {StatusCode}",
                                        retryCount,
                                        timespan.TotalSeconds,
                                        outcome.Result?.StatusCode);
                                });
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return await _retryPolicy.ExecuteAsync(async () => await base.SendAsync(request, cancellationToken));
        }
    }
}
