using Argus.Domain.Aggregates;
using Argus.Domain.Models;

namespace Argus.Domain.Services;

/// <summary>
/// Compares two sets of positions field-by-field and reports discrepancies.
/// Expected = replayed from events, Actual = live (inline projection).
/// </summary>
public static class PositionComparer
{
    private const decimal PnlThreshold = 0.01m;

    public static IReadOnlyList<PositionDiscrepancy> Compare(
        IReadOnlyList<Position> expected,
        IReadOnlyList<Position> actual)
    {
        var discrepancies = new List<PositionDiscrepancy>();

        var expectedByInstrument = expected.ToDictionary(p => p.InstrumentId);
        var actualByInstrument = actual.ToDictionary(p => p.InstrumentId);

        // Check positions in expected but missing in actual
        foreach (var (id, exp) in expectedByInstrument)
        {
            if (!actualByInstrument.TryGetValue(id, out var act))
            {
                discrepancies.Add(new PositionDiscrepancy(
                    id, exp.Symbol, "Missing", "Present", "Missing in live", null));
                continue;
            }

            ComparePositionFields(exp, act, discrepancies);
        }

        // Check positions in actual but missing in expected
        foreach (var (id, act) in actualByInstrument)
        {
            if (!expectedByInstrument.ContainsKey(id))
            {
                discrepancies.Add(new PositionDiscrepancy(
                    id, act.Symbol, "Missing", "Missing in replay", "Present", null));
            }
        }

        return discrepancies;
    }

    private static void ComparePositionFields(
        Position expected,
        Position actual,
        List<PositionDiscrepancy> discrepancies)
    {
        if (expected.Quantity != actual.Quantity)
        {
            discrepancies.Add(new PositionDiscrepancy(
                expected.InstrumentId,
                expected.Symbol,
                "Quantity",
                expected.Quantity.ToString(),
                actual.Quantity.ToString(),
                actual.Quantity - expected.Quantity));
        }

        var pnlDiff = Math.Abs(expected.RealizedPnl - actual.RealizedPnl);
        if (pnlDiff > PnlThreshold)
        {
            discrepancies.Add(new PositionDiscrepancy(
                expected.InstrumentId,
                expected.Symbol,
                "RealizedPnl",
                expected.RealizedPnl.ToString("F2"),
                actual.RealizedPnl.ToString("F2"),
                actual.RealizedPnl - expected.RealizedPnl));
        }

        if (expected.IsOpen != actual.IsOpen)
        {
            discrepancies.Add(new PositionDiscrepancy(
                expected.InstrumentId,
                expected.Symbol,
                "IsOpen",
                expected.IsOpen.ToString(),
                actual.IsOpen.ToString(),
                null));
        }

        if (expected.CostLots.Count != actual.CostLots.Count)
        {
            discrepancies.Add(new PositionDiscrepancy(
                expected.InstrumentId,
                expected.Symbol,
                "CostLots.Count",
                expected.CostLots.Count.ToString(),
                actual.CostLots.Count.ToString(),
                actual.CostLots.Count - expected.CostLots.Count));
        }
    }
}
