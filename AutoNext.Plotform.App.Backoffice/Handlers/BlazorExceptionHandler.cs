using Microsoft.AspNetCore.Components.Server.Circuits;

namespace AutoNext.Plotform.App.Backoffice.Handlers
{
    public class BlazorExceptionHandler : CircuitHandler
    {
        private readonly ILogger<BlazorExceptionHandler> _logger;

        public BlazorExceptionHandler(ILogger<BlazorExceptionHandler> logger)
        {
            _logger = logger;
        }

        public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken ct)
        {
            _logger.LogInformation("Blazor circuit opened: {CircuitId}", circuit.Id);
            return base.OnCircuitOpenedAsync(circuit, ct);
        }

        public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken ct)
        {
            _logger.LogInformation("Blazor circuit closed: {CircuitId}", circuit.Id);
            return base.OnCircuitClosedAsync(circuit, ct);
        }

        public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken ct)
        {
            _logger.LogDebug("Blazor circuit reconnected: {CircuitId}", circuit.Id);
            return base.OnConnectionUpAsync(circuit, ct);
        }

        public override Task OnConnectionDownAsync(Circuit circuit, CancellationToken ct)
        {
            _logger.LogWarning("Blazor circuit connection dropped: {CircuitId}", circuit.Id);
            return base.OnConnectionDownAsync(circuit, ct);
        }
    }
}
