using Argus.Domain.Enums;
using Argus.Domain.Models;

namespace Argus.Infrastructure.Data;

/// <summary>
/// Provides reference data for tradeable instruments.
/// In production, this would come from a database; here we use a static set.
/// </summary>
public sealed class InstrumentRepository
{
    private readonly Dictionary<Guid, Instrument> _instruments;

    public InstrumentRepository()
    {
        _instruments = CreateInstruments().ToDictionary(i => i.Id);
    }

    public IReadOnlyCollection<Instrument> GetAll() => _instruments.Values;

    public Instrument? GetById(Guid id) => _instruments.GetValueOrDefault(id);

    public IReadOnlyCollection<Instrument> GetBySector(Sector sector) =>
        _instruments.Values.Where(i => i.Sector == sector).ToList();

    private static IEnumerable<Instrument> CreateInstruments()
    {
        // Technology (USD)
        yield return new Instrument(Guid.Parse("10000000-0000-0000-0000-000000000001"), "AAPL", "Apple Inc.", Sector.Technology, Currency.USD, 175.00m);
        yield return new Instrument(Guid.Parse("10000000-0000-0000-0000-000000000002"), "MSFT", "Microsoft Corp.", Sector.Technology, Currency.USD, 380.00m);
        yield return new Instrument(Guid.Parse("10000000-0000-0000-0000-000000000003"), "GOOGL", "Alphabet Inc.", Sector.Technology, Currency.USD, 140.00m);
        yield return new Instrument(Guid.Parse("10000000-0000-0000-0000-000000000004"), "NVDA", "NVIDIA Corp.", Sector.Technology, Currency.USD, 480.00m);
        yield return new Instrument(Guid.Parse("10000000-0000-0000-0000-000000000005"), "META", "Meta Platforms", Sector.Technology, Currency.USD, 350.00m);

        // Healthcare (USD)
        yield return new Instrument(Guid.Parse("20000000-0000-0000-0000-000000000001"), "JNJ", "Johnson & Johnson", Sector.Healthcare, Currency.USD, 160.00m);
        yield return new Instrument(Guid.Parse("20000000-0000-0000-0000-000000000002"), "UNH", "UnitedHealth Group", Sector.Healthcare, Currency.USD, 520.00m);
        yield return new Instrument(Guid.Parse("20000000-0000-0000-0000-000000000003"), "PFE", "Pfizer Inc.", Sector.Healthcare, Currency.USD, 28.00m);
        yield return new Instrument(Guid.Parse("20000000-0000-0000-0000-000000000004"), "ABBV", "AbbVie Inc.", Sector.Healthcare, Currency.USD, 155.00m);

        // Finance (USD + GBP)
        yield return new Instrument(Guid.Parse("30000000-0000-0000-0000-000000000001"), "JPM", "JPMorgan Chase", Sector.Finance, Currency.USD, 170.00m);
        yield return new Instrument(Guid.Parse("30000000-0000-0000-0000-000000000002"), "BAC", "Bank of America", Sector.Finance, Currency.USD, 33.00m);
        yield return new Instrument(Guid.Parse("30000000-0000-0000-0000-000000000003"), "GS", "Goldman Sachs", Sector.Finance, Currency.USD, 380.00m);
        yield return new Instrument(Guid.Parse("30000000-0000-0000-0000-000000000004"), "HSBA.L", "HSBC Holdings", Sector.Finance, Currency.GBP, 6.50m);
        yield return new Instrument(Guid.Parse("30000000-0000-0000-0000-000000000005"), "BARC.L", "Barclays PLC", Sector.Finance, Currency.GBP, 1.70m);

        // Energy (USD + EUR)
        yield return new Instrument(Guid.Parse("40000000-0000-0000-0000-000000000001"), "XOM", "Exxon Mobil", Sector.Energy, Currency.USD, 105.00m);
        yield return new Instrument(Guid.Parse("40000000-0000-0000-0000-000000000002"), "CVX", "Chevron Corp.", Sector.Energy, Currency.USD, 150.00m);
        yield return new Instrument(Guid.Parse("40000000-0000-0000-0000-000000000003"), "SHEL.L", "Shell PLC", Sector.Energy, Currency.GBP, 25.00m);
        yield return new Instrument(Guid.Parse("40000000-0000-0000-0000-000000000004"), "TTE.PA", "TotalEnergies", Sector.Energy, Currency.EUR, 62.00m);

        // Consumer (USD + EUR)
        yield return new Instrument(Guid.Parse("50000000-0000-0000-0000-000000000001"), "AMZN", "Amazon.com", Sector.ConsumerDiscretionary, Currency.USD, 155.00m);
        yield return new Instrument(Guid.Parse("50000000-0000-0000-0000-000000000002"), "TSLA", "Tesla Inc.", Sector.ConsumerDiscretionary, Currency.USD, 245.00m);
        yield return new Instrument(Guid.Parse("50000000-0000-0000-0000-000000000003"), "NKE", "Nike Inc.", Sector.ConsumerDiscretionary, Currency.USD, 105.00m);
        yield return new Instrument(Guid.Parse("50000000-0000-0000-0000-000000000004"), "MC.PA", "LVMH", Sector.ConsumerDiscretionary, Currency.EUR, 750.00m);

        // Industrials (USD + EUR)
        yield return new Instrument(Guid.Parse("60000000-0000-0000-0000-000000000001"), "CAT", "Caterpillar Inc.", Sector.Industrials, Currency.USD, 280.00m);
        yield return new Instrument(Guid.Parse("60000000-0000-0000-0000-000000000002"), "BA", "Boeing Co.", Sector.Industrials, Currency.USD, 210.00m);
        yield return new Instrument(Guid.Parse("60000000-0000-0000-0000-000000000003"), "AIR.PA", "Airbus SE", Sector.Industrials, Currency.EUR, 135.00m);

        // Communications (USD)
        yield return new Instrument(Guid.Parse("70000000-0000-0000-0000-000000000001"), "VZ", "Verizon", Sector.Communications, Currency.USD, 38.00m);
        yield return new Instrument(Guid.Parse("70000000-0000-0000-0000-000000000002"), "T", "AT&T Inc.", Sector.Communications, Currency.USD, 17.00m);
        yield return new Instrument(Guid.Parse("70000000-0000-0000-0000-000000000003"), "NFLX", "Netflix Inc.", Sector.Communications, Currency.USD, 480.00m);
    }
}
