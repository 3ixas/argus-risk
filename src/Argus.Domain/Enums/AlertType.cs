namespace Argus.Domain.Enums;

public enum AlertType
{
    StaleData,
    ReconciliationBreak,
    HighLatency,
    ConsumerLag,
    CircuitBreakerOpen
}
