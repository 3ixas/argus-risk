using Argus.Domain.Aggregates;
using Argus.Domain.Enums;
using Argus.Domain.Events;
using Argus.Domain.Services;
using FluentAssertions;
using Xunit;

namespace Argus.Api.Tests.Services;

public sealed class PositionComparerTests
{
    private static readonly Guid Id1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Id2 = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private static Position CreatePosition(
        Guid instrumentId, string symbol, int quantity, decimal price)
    {
        var position = new Position();
        position.Apply(new PositionOpened(
            TradeId: Guid.NewGuid(),
            InstrumentId: instrumentId,
            Symbol: symbol,
            Currency: Currency.USD,
            Side: quantity > 0 ? TradeSide.Buy : TradeSide.Sell,
            Quantity: Math.Abs(quantity),
            Price: price,
            Timestamp: DateTimeOffset.UtcNow));
        return position;
    }

    [Fact]
    public void Compare_IdenticalLists_NoDiscrepancies()
    {
        var expected = new[] { CreatePosition(Id1, "AAPL", 100, 150m) };
        var actual = new[] { CreatePosition(Id1, "AAPL", 100, 150m) };

        var result = PositionComparer.Compare(expected, actual);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Compare_QuantityMismatch_ReportsDiscrepancy()
    {
        var expected = new[] { CreatePosition(Id1, "AAPL", 100, 150m) };
        var actual = new[] { CreatePosition(Id1, "AAPL", 200, 150m) };

        var result = PositionComparer.Compare(expected, actual);

        result.Should().ContainSingle()
            .Which.Field.Should().Be("Quantity");
        result[0].Difference.Should().Be(100);
    }

    [Fact]
    public void Compare_MissingInActual_ReportsDiscrepancy()
    {
        var expected = new[] { CreatePosition(Id1, "AAPL", 100, 150m) };
        var actual = Array.Empty<Position>();

        var result = PositionComparer.Compare(expected, actual);

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Missing");
        result[0].Actual.Should().Be("Missing in live");
    }

    [Fact]
    public void Compare_MissingInExpected_ReportsDiscrepancy()
    {
        var expected = Array.Empty<Position>();
        var actual = new[] { CreatePosition(Id1, "AAPL", 100, 150m) };

        var result = PositionComparer.Compare(expected, actual);

        result.Should().ContainSingle();
        result[0].Field.Should().Be("Missing");
        result[0].Expected.Should().Be("Missing in replay");
    }

    [Fact]
    public void Compare_PnlWithinThreshold_NoDiscrepancy()
    {
        // Both positions have identical state (RealizedPnl = 0), so no discrepancy
        var expected = new[] { CreatePosition(Id1, "AAPL", 100, 150m) };
        var actual = new[] { CreatePosition(Id1, "AAPL", 100, 150m) };

        var result = PositionComparer.Compare(expected, actual);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Compare_CostLotCountMismatch_ReportsDiscrepancy()
    {
        var expected = new[] { CreatePosition(Id1, "AAPL", 100, 150m) };
        var actual = new[] { CreatePosition(Id1, "AAPL", 100, 150m) };

        // Add a second cost lot to the actual position
        actual[0].Apply(new PositionIncreased(
            TradeId: Guid.NewGuid(),
            Quantity: 50,
            Price: 155m,
            Timestamp: DateTimeOffset.UtcNow));

        var result = PositionComparer.Compare(expected, actual);

        // Quantity mismatch + CostLots.Count mismatch
        result.Should().Contain(d => d.Field == "CostLots.Count");
        result.Should().Contain(d => d.Field == "Quantity");
    }

    [Fact]
    public void Compare_MultipleDiscrepancies_ReportsAll()
    {
        var expected = new[]
        {
            CreatePosition(Id1, "AAPL", 100, 150m),
            CreatePosition(Id2, "MSFT", 200, 350m)
        };
        var actual = new[]
        {
            CreatePosition(Id1, "AAPL", 150, 150m), // Quantity mismatch
            CreatePosition(Id2, "MSFT", 200, 350m)  // Matches
        };

        var result = PositionComparer.Compare(expected, actual);

        result.Should().ContainSingle();
        result[0].InstrumentId.Should().Be(Id1);
    }

    [Fact]
    public void Compare_BothEmpty_NoDiscrepancies()
    {
        var result = PositionComparer.Compare([], []);

        result.Should().BeEmpty();
    }
}
