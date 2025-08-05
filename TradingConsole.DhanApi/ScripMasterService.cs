// TradingConsole.DhanApi/ScripMasterService.cs

using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TradingConsole.DhanApi.Models;

namespace TradingConsole.DhanApi
{
    public class ScripMasterService
    {
        private List<ScripInfo> _scripMaster = new List<ScripInfo>();
        private readonly HttpClient _httpClient;
        private const string ScripMasterUrl = "https://images.dhan.co/api-data/api-scrip-master-detailed.csv";

        public ScripMasterService()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        }

        public async Task LoadScripMasterAsync()
        {
            try
            {
                Debug.WriteLine("[ScripMaster] Starting to download detailed scrip master CSV...");

                var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HeaderValidated = null,
                    MissingFieldFound = null,
                    BadDataFound = context =>
                    {
                        Debug.WriteLine($"[ScripMaster] Bad data found in CSV. Field: '{context.Field}', RawRecord: '{context.RawRecord}'");
                    }
                };

                using var stream = await _httpClient.GetStreamAsync(ScripMasterUrl);
                using var reader = new StreamReader(stream);
                using var csv = new CsvReader(reader, csvConfig);

                csv.Context.RegisterClassMap<ScripInfoMap>();

                var records = csv.GetRecords<ScripInfo>().ToList();

                var allowedTypes = new HashSet<string> { "EQUITY", "FUTIDX", "FUTSTK", "INDEX", "OPTIDX" };

                _scripMaster = records
                    .Where(r => !string.IsNullOrEmpty(r.InstrumentType) && allowedTypes.Contains(r.InstrumentType.Trim().ToUpper()))
                    .Select(r =>
                    {
                        r.Segment = MapCsvSegmentToApiSegment(r.ExchId, r.Segment, r.InstrumentType);
                        return r;
                    })
                    .ToList();

                Debug.WriteLine($"[ScripMaster] Scrip Master loaded with {_scripMaster.Count} relevant instruments.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScripMaster] FAILED to load scrip master: {ex.Message}");
            }
        }

        public ScripInfo? FindBySecurityId(string securityId)
        {
            return _scripMaster.FirstOrDefault(s => s.SecurityId == securityId);
        }
        public ScripInfo? FindBySecurityIdAndType(string securityId, string instrumentType)
        {
            // Basic validation to ensure the inputs are usable.
            if (string.IsNullOrEmpty(securityId) || string.IsNullOrEmpty(instrumentType))
            {
                return null;
            }

            // Use LINQ's FirstOrDefault to find the first scrip that matches BOTH the securityId and the instrumentType.
            return _scripMaster.FirstOrDefault(s => s.SecurityId == securityId && s.InstrumentType == instrumentType);
        }

        public ScripInfo? FindEquityScripInfo(string tradingSymbol)
        {
            var term = RemoveWhitespace(tradingSymbol).ToUpperInvariant();

            var result = _scripMaster.FirstOrDefault(s =>
                s.ExchId.ToUpperInvariant().Equals("NSE") &&
                s.InstrumentType == "EQUITY" &&
                RemoveWhitespace(s.TradingSymbol).ToUpperInvariant().Equals(term));

            if (result == null)
            {
                result = _scripMaster.FirstOrDefault(s =>
                   s.ExchId.ToUpperInvariant().Equals("NSE") &&
                   s.InstrumentType == "EQUITY" &&
                   RemoveWhitespace(s.SemInstrumentName).ToUpperInvariant().Equals(term));
            }

            return result;
        }

        public ScripInfo? FindIndexScripInfo(string indexName)
        {
            var term = RemoveWhitespace(indexName).ToUpperInvariant();
            return _scripMaster.FirstOrDefault(s => s.InstrumentType == "INDEX" && RemoveWhitespace(s.SemInstrumentName).ToUpperInvariant().Equals(term));
        }

        public ScripInfo? FindNearMonthFutureSecurityId(string underlyingSymbol)
        {
            var normalizedUnderlyingSymbol = RemoveWhitespace(underlyingSymbol).ToUpperInvariant();
            Debug.WriteLine($"[ScripMaster_FindFuture] Searching for NEAR MONTH FUTURE for underlying: '{underlyingSymbol}' (normalized: '{normalizedUnderlyingSymbol}')");

            var futures = _scripMaster
                .Where(s => (s.InstrumentType == "FUTIDX" || s.InstrumentType == "FUTSTK") &&
                            s.ExpiryDate.HasValue && s.ExpiryDate.Value.Date >= DateTime.Today)
                .ToList();

            var matchingStockFutures = futures
                .Where(s => s.ExchId.ToUpperInvariant().Equals("NSE") && s.InstrumentType == "FUTSTK" &&
                            s.UnderlyingSymbol.ToUpperInvariant().Equals(normalizedUnderlyingSymbol))
                .OrderBy(s => s.ExpiryDate)
                .ToList();

            if (matchingStockFutures.Any())
            {
                return matchingStockFutures.FirstOrDefault();
            }

            if (normalizedUnderlyingSymbol == "NIFTY50")
            {
                var niftyFutures = futures
                    .Where(s => s.InstrumentType == "FUTIDX" &&
                                RemoveWhitespace(s.SemInstrumentName).ToUpperInvariant().Contains("NIFTY") &&
                                !RemoveWhitespace(s.SemInstrumentName).ToUpperInvariant().Contains("BANKNIFTY") &&
                                !RemoveWhitespace(s.SemInstrumentName).ToUpperInvariant().Contains("FINNIFTY") &&
                                !Regex.IsMatch(RemoveWhitespace(s.SemInstrumentName).ToUpperInvariant(), @"NIFTY\s*\d+\s*[A-Z]{3}\s*FUT"))
                    .OrderBy(s => s.ExpiryDate);
                if (niftyFutures.Any()) return niftyFutures.FirstOrDefault();
            }
            else if (normalizedUnderlyingSymbol == "NIFTYBANK")
            {
                var bankNiftyFutures = futures
                    .Where(s => s.InstrumentType == "FUTIDX" &&
                                RemoveWhitespace(s.SemInstrumentName).ToUpperInvariant().Contains("BANKNIFTY"))
                    .OrderBy(s => s.ExpiryDate);
                if (bankNiftyFutures.Any()) return bankNiftyFutures.FirstOrDefault();
            }
            else if (normalizedUnderlyingSymbol == "SENSEX")
            {
                var sensexFutures = futures
                    .Where(s => s.InstrumentType == "FUTIDX" &&
                                RemoveWhitespace(s.SemInstrumentName).ToUpperInvariant().Contains("SENSEX") &&
                                RemoveWhitespace(s.SemInstrumentName).ToUpperInvariant().Contains("FUT") &&
                                !Regex.IsMatch(RemoveWhitespace(s.SemInstrumentName).ToUpperInvariant(), @"SENSEX\s*\d+\s*[A-Z]{3}\s*FUT"))
                    .OrderBy(s => s.ExpiryDate);
                if (sensexFutures.Any()) return sensexFutures.FirstOrDefault();
            }
            else
            {
                var genericIndexFutures = futures
                    .Where(s => s.InstrumentType == "FUTIDX" &&
                                s.UnderlyingSymbol.ToUpperInvariant().Equals(normalizedUnderlyingSymbol))
                    .OrderBy(s => s.ExpiryDate);
                if (genericIndexFutures.Any()) return genericIndexFutures.FirstOrDefault();
            }

            Debug.WriteLine($"[ScripMaster_FindFuture] FAIL - No future contract found for underlying: '{underlyingSymbol}'");
            return null;
        }

        // --- UPDATED: Added detailed logging to diagnose lookup failures. ---
        public ScripInfo? FindOptionScripInfo(string underlyingSymbol, DateTime expiryDate, decimal strikePrice, string optionType)
        {
            Debug.WriteLine($"[ScripMaster_FIND_OPT] START: underlying='{underlyingSymbol}', expiry='{expiryDate:yyyy-MM-dd}', strike='{strikePrice}', type='{optionType}'");

            var termUnderlying = RemoveWhitespace(underlyingSymbol).ToUpperInvariant();
            var termOptionType = optionType.Trim().ToUpperInvariant();

            // Step 1: Attempt the fast, reliable search using the UnderlyingSymbol field.
            var result = _scripMaster.FirstOrDefault(s =>
                (s.InstrumentType == "OPTIDX" || s.InstrumentType == "OPTSTK") &&
                s.ExpiryDate.HasValue && s.ExpiryDate.Value.Date == expiryDate.Date &&
                s.StrikePrice == strikePrice &&
                s.OptionType.ToUpperInvariant() == termOptionType &&
                s.UnderlyingSymbol.ToUpperInvariant() == termUnderlying);

            Debug.WriteLine($"[ScripMaster_FIND_OPT] Step 1 (UnderlyingSymbol match) result: {(result == null ? "FAIL" : "SUCCESS")}");

            // Step 2: If the first attempt fails, fall back to the old, more forgiving search method.
            if (result == null)
            {
                var potentialOptions = _scripMaster
                    .Where(s => (s.InstrumentType == "OPTIDX" || s.InstrumentType == "OPTSTK") &&
                                s.ExpiryDate.HasValue && s.ExpiryDate.Value.Date == expiryDate.Date &&
                                s.StrikePrice == strikePrice &&
                                s.OptionType.ToUpperInvariant().Equals(termOptionType))
                    .ToList();

                Debug.WriteLine($"[ScripMaster_FIND_OPT] Step 2 (Fallback) found {potentialOptions.Count} potential options based on expiry, strike, and type.");

                if (potentialOptions.Any())
                {
                    result = potentialOptions.FirstOrDefault(s =>
                        RemoveWhitespace(s.SemInstrumentName).ToUpperInvariant().Contains(termUnderlying));

                    Debug.WriteLine($"[ScripMaster_FIND_OPT] Step 2 (Fallback) result after name search: {(result == null ? "FAIL" : "SUCCESS")}");
                }
            }

            Debug.WriteLine($"[ScripMaster_FIND_OPT] END: Found SecurityId: {(result?.SecurityId ?? "NONE")}");
            return result;
        }

        public int GetLotSizeForSecurity(string securityId) => _scripMaster.FirstOrDefault(s => s.SecurityId == securityId)?.LotSize ?? 0;

        public int GetSegmentIdFromName(string segmentName) => segmentName switch { "NSE_EQ" => 1, "NSE_FNO" => 2, "BSE_EQ" => 3, "BSE_FNO" => 8, "IDX_I" => 0, "I" => 0, _ => -1 };

        private string MapCsvSegmentToApiSegment(string exchId, string csvSegment, string instrumentType) { if (instrumentType == "EQUITY") return exchId.ToUpperInvariant() == "NSE" ? "NSE_EQ" : "BSE_EQ"; if (instrumentType == "FUTIDX" || instrumentType == "FUTSTK" || instrumentType == "OPTIDX" || instrumentType == "OPTSTK") return exchId.ToUpperInvariant() == "NSE" ? "NSE_FNO" : "BSE_FNO"; if (instrumentType == "INDEX") return "IDX_I"; return csvSegment; }

        private string RemoveWhitespace(string input) => new string(input.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }

    public sealed class ScripInfoMap : ClassMap<ScripInfo>
    {
        public ScripInfoMap()
        {
            Map(m => m.ExchId).Name("EXCH_ID");
            Map(m => m.Segment).Name("SEGMENT");
            Map(m => m.SecurityId).Name("SECURITY_ID");
            Map(m => m.InstrumentType).Name("INSTRUMENT");
            Map(m => m.SemInstrumentName).Name("DISPLAY_NAME");
            Map(m => m.TradingSymbol).Name("SYMBOL_NAME");
            Map(m => m.UnderlyingSymbol).Name("UNDERLYING_SYMBOL");
            Map(m => m.LotSize).Name("LOT_SIZE").TypeConverterOption.NumberStyles(NumberStyles.Any).Default(0);
            Map(m => m.StrikePrice).Name("STRIKE_PRICE").TypeConverterOption.NumberStyles(NumberStyles.Any).Default(0m);
            Map(m => m.OptionType).Name("OPTION_TYPE").Default(string.Empty);
            Map(m => m.ExpiryDate).Name("SM_EXPIRY_DATE").TypeConverter<MultiFormatDateConverter>();
        }
    }

    public class MultiFormatDateConverter : DateTimeConverter
    {
        public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
        {
            if (string.IsNullOrWhiteSpace(text) || text == "0") return null;
            var formats = new[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd", "yyyyMMdd" };
            foreach (var format in formats)
            {
                if (DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    return date;
                }
            }
            return null;
        }
    }
}
