using Argus.Domain.Enums;
using Argus.Domain.Events;
using Argus.Domain.Models;
using Argus.Domain.Services;

namespace Argus.Domain.Aggregates;

/// <summary>
/// Event-sourced aggregate representing a position in a single instrument.
/// Marten rebuilds this by replaying Apply methods against the event stream.
/// Stream ID = InstrumentId (one position per instrument).
/// </summary>
public sealed class Position
{
    public Guid Id { get; private set; }
    public Guid InstrumentId { get; private set; }
    public string Symbol { get; private set; } = string.Empty;
    public Currency Currency { get; private set; }
    public int Quantity { get; private set; }
    public List<CostLot> CostLots { get; private set; } = [];
    public decimal RealizedPnl { get; private set; }
    public bool IsOpen { get; private set; }
    public DateTimeOffset LastUpdated { get; private set; }

    public bool IsLong => Quantity > 0;
    public bool IsShort => Quantity < 0;

    public void Apply(PositionOpened e)
    {
        Id = e.InstrumentId;
        InstrumentId = e.InstrumentId;
        Symbol = e.Symbol;
        Currency = e.Currency;
        Quantity = e.Side == TradeSide.Buy ? e.Quantity : -e.Quantity;
        CostLots = [new CostLot(e.Quantity, e.Price)];
        RealizedPnl = 0m;
        IsOpen = true;
        LastUpdated = e.Timestamp;
    }

    public void Apply(PositionIncreased e)
    {
        Quantity += IsLong ? e.Quantity : -e.Quantity;
        CostLots.Add(new CostLot(e.Quantity, e.Price));
        LastUpdated = e.Timestamp;
    }

    public void Apply(PositionDecreased e)
    {
        Quantity += IsLong ? -e.QuantityClosed : e.QuantityClosed;
        RealizedPnl += e.RealizedPnl;

        var fifoResult = FifoCostBasisCalculator.Calculate(
            CostLots, e.QuantityClosed, e.Price, IsLong);
        CostLots = fifoResult.RemainingLots;

        LastUpdated = e.Timestamp;
    }

    public void Apply(PositionClosed e)
    {
        Quantity = 0;
        RealizedPnl += e.RealizedPnl;
        CostLots = [];
        IsOpen = false;
        LastUpdated = e.Timestamp;
    }

    public void Apply(PositionReversed e)
    {
        RealizedPnl += e.RealizedPnl;
        Quantity = e.NewSide == TradeSide.Buy ? e.NewQuantity : -e.NewQuantity;
        CostLots = [new CostLot(e.NewQuantity, e.NewPositionPrice)];
        IsOpen = true;
        LastUpdated = e.Timestamp;
    }
}
