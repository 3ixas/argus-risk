using System.Security.Cryptography;
using System.Text;
using Argus.Domain.Aggregates;

namespace Argus.Domain.Services;

/// <summary>
/// Computes a deterministic SHA-256 checksum over a collection of positions.
/// Positions are sorted by InstrumentId to guarantee order independence.
/// Only core event-sourced fields are included: InstrumentId, Quantity, RealizedPnl, CostLots.
/// </summary>
public static class PortfolioChecksumCalculator
{
    public static string Compute(IEnumerable<Position> positions)
    {
        var sorted = positions.OrderBy(p => p.InstrumentId).ToList();

        var sb = new StringBuilder();

        foreach (var p in sorted)
        {
            sb.Append(p.InstrumentId);
            sb.Append('|');
            sb.Append(p.Quantity);
            sb.Append('|');
            sb.Append(p.RealizedPnl.ToString("F8"));
            sb.Append('|');

            foreach (var lot in p.CostLots)
            {
                sb.Append(lot.Quantity.ToString("F8"));
                sb.Append(':');
                sb.Append(lot.PricePerUnit.ToString("F8"));
                sb.Append(',');
            }

            sb.Append(';');
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
