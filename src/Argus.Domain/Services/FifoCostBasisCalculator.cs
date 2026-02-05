using Argus.Domain.Models;

namespace Argus.Domain.Services;

public sealed record FifoResult(decimal RealizedPnl, List<CostLot> RemainingLots);

public static class FifoCostBasisCalculator
{
    /// <summary>
    /// Calculates realized P&amp;L using FIFO (First In, First Out) order.
    /// Walks the oldest cost lots first, consuming quantity at each lot's price.
    ///
    /// For long positions: P&amp;L per lot = (closePrice - lotPrice) × consumed
    /// For short positions: P&amp;L per lot = (lotPrice - closePrice) × consumed
    /// </summary>
    /// <param name="lots">Current cost lots in FIFO order (oldest first)</param>
    /// <param name="quantityToClose">Absolute quantity being closed (always positive)</param>
    /// <param name="closePrice">Price at which the position is being closed</param>
    /// <param name="isLong">Whether the position being closed is long (true) or short (false)</param>
    /// <returns>Realized P&amp;L and any remaining cost lots</returns>
    public static FifoResult Calculate(
        List<CostLot> lots,
        int quantityToClose,
        decimal closePrice,
        bool isLong)
    {
        var remaining = new List<CostLot>();
        var realizedPnl = 0m;
        var qtyLeft = (decimal)quantityToClose;

        foreach (var lot in lots)
        {
            if (qtyLeft <= 0)
            {
                remaining.Add(lot);
                continue;
            }

            var consumed = Math.Min(qtyLeft, lot.Quantity);
            var pnlPerUnit = isLong
                ? closePrice - lot.PricePerUnit
                : lot.PricePerUnit - closePrice;

            realizedPnl += pnlPerUnit * consumed;
            qtyLeft -= consumed;

            var leftover = lot.Quantity - consumed;
            if (leftover > 0)
                remaining.Add(lot with { Quantity = leftover });
        }

        return new FifoResult(realizedPnl, remaining);
    }
}
