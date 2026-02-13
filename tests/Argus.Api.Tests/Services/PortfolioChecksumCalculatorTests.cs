using Argus.Domain.Aggregates;
using Argus.Domain.Enums;
using Argus.Domain.Events;
using Argus.Domain.Services;
using FluentAssertions;
using Xunit;

namespace Argus.Api.Tests.Services;

public sealed class PortfolioChecksumCalculatorTests
{
    private static Position CreatePosition(Guid instrumentId, string symbol, int quantity, decimal price)
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
    public void Compute_EmptyList_ReturnsConsistentHash()
    {
        var hash1 = PortfolioChecksumCalculator.Compute([]);
        var hash2 = PortfolioChecksumCalculator.Compute([]);

        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(64, "SHA-256 produces 64 hex characters");
    }

    [Fact]
    public void Compute_SinglePosition_ReturnsDeterministicHash()
    {
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var p1 = CreatePosition(id, "AAPL", 100, 150.00m);
        var p2 = CreatePosition(id, "AAPL", 100, 150.00m);

        var hash1 = PortfolioChecksumCalculator.Compute([p1]);
        var hash2 = PortfolioChecksumCalculator.Compute([p2]);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void Compute_DifferentOrder_SameHash()
    {
        var id1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var id2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var posA = CreatePosition(id1, "AAPL", 100, 150.00m);
        var posB = CreatePosition(id2, "MSFT", 200, 350.00m);

        var hash1 = PortfolioChecksumCalculator.Compute([posA, posB]);
        var hash2 = PortfolioChecksumCalculator.Compute([posB, posA]);

        hash1.Should().Be(hash2, "order should not affect checksum");
    }

    [Fact]
    public void Compute_DifferentQuantity_DifferentHash()
    {
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var pos100 = CreatePosition(id, "AAPL", 100, 150.00m);
        var pos200 = CreatePosition(id, "AAPL", 200, 150.00m);

        var hash1 = PortfolioChecksumCalculator.Compute([pos100]);
        var hash2 = PortfolioChecksumCalculator.Compute([pos200]);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Compute_DifferentPrice_DifferentHash()
    {
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var pos1 = CreatePosition(id, "AAPL", 100, 150.00m);
        var pos2 = CreatePosition(id, "AAPL", 100, 151.00m);

        var hash1 = PortfolioChecksumCalculator.Compute([pos1]);
        var hash2 = PortfolioChecksumCalculator.Compute([pos2]);

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Compute_ReturnsLowercaseHex()
    {
        var id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var pos = CreatePosition(id, "AAPL", 100, 150.00m);

        var hash = PortfolioChecksumCalculator.Compute([pos]);

        hash.Should().MatchRegex("^[0-9a-f]{64}$");
    }
}
