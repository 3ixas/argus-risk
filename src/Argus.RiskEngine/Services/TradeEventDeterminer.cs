using Argus.Domain.Aggregates;
using Argus.Domain.Enums;
using Argus.Domain.Events;
using Argus.Domain.Models;
using Argus.Domain.Services;

namespace Argus.RiskEngine.Services;

/// <summary>
/// Pure function: determines which domain event to emit for a given trade
/// based on the current position state.
/// </summary>
public static class TradeEventDeterminer
{
    /// <summary>
    /// Returns the domain event to append to the position's event stream.
    /// </summary>
    public static object Determine(Position? position, Trade trade)
    {
        if (position is null || !position.IsOpen)
        {
            return new PositionOpened(
                trade.TradeId,
                trade.InstrumentId,
                trade.Symbol,
                trade.Currency,
                trade.Side,
                trade.Quantity,
                trade.Price,
                trade.Timestamp);
        }

        var isSameSide = (position.IsLong && trade.Side == TradeSide.Buy)
                      || (position.IsShort && trade.Side == TradeSide.Sell);

        if (isSameSide)
        {
            return new PositionIncreased(
                trade.TradeId,
                trade.Quantity,
                trade.Price,
                trade.Timestamp);
        }

        var absPosition = Math.Abs(position.Quantity);
        var tradeQty = trade.Quantity;

        if (tradeQty < absPosition)
        {
            var fifo = FifoCostBasisCalculator.Calculate(
                position.CostLots, tradeQty, trade.Price, position.IsLong);

            return new PositionDecreased(
                trade.TradeId,
                tradeQty,
                trade.Price,
                fifo.RealizedPnl,
                trade.Timestamp);
        }

        if (tradeQty == absPosition)
        {
            var fifo = FifoCostBasisCalculator.Calculate(
                position.CostLots, tradeQty, trade.Price, position.IsLong);

            return new PositionClosed(
                trade.TradeId,
                tradeQty,
                trade.Price,
                fifo.RealizedPnl,
                trade.Timestamp);
        }

        // tradeQty > absPosition — reversal
        {
            var fifo = FifoCostBasisCalculator.Calculate(
                position.CostLots, absPosition, trade.Price, position.IsLong);

            var newQty = tradeQty - absPosition;

            return new PositionReversed(
                trade.TradeId,
                absPosition,
                newQty,
                trade.Side,
                trade.Price,
                fifo.RealizedPnl,
                trade.Price,
                trade.Timestamp);
        }
    }
}
