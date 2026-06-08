using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace AiTokenBot.Services
{
    public class JupiterService
    {
        private readonly HttpClient _http;
        private const string QuoteApi = "https://quote-api.jup.ag/v6";
        private const string PriceApi = "https://api.jup.ag/price/v2";
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

        public JupiterService()
        {
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        }

        // ===== Price =====

        public async Task<Dictionary<string, decimal>> GetPricesAsync(params string[] tokenIds)
        {
            try
            {
                var ids = string.Join(",", tokenIds);
                var resp = await _http.GetAsync($"{PriceApi}?ids={ids}");
                if (!resp.IsSuccessStatusCode) return new();

                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");
                var result = new Dictionary<string, decimal>();
                foreach (var prop in data.EnumerateObject())
                    result[prop.Name] = decimal.Parse(prop.Value.GetProperty("price").GetRawText());
                return result;
            }
            catch { return new(); }
        }

        // ===== Trending =====

        public async Task<List<TrendingToken>> GetTrendingTokensAsync()
        {
            try
            {
                var resp = await _http.GetAsync($"https://tokens.jup.ag/tokens/tradable");
                if (!resp.IsSuccessStatusCode) return new();

                var tokens = await resp.Content.ReadFromJsonAsync<List<JupiterToken>>(JsonOpts);
                if (tokens == null) return new();

                var result = new List<TrendingToken>();
                foreach (var t in tokens.Take(20))
                {
                    result.Add(new TrendingToken
                    {
                        Symbol = t.symbol ?? "", Name = t.name ?? "",
                        Mint = t.address ?? "", Decimals = t.decimals,
                        Volume24h = 0, Price = 0,
                    });
                }
                return result;
            }
            catch { return new(); }
        }

        // ===== Token Info =====

        public async Task<TrendingToken?> GetTokenInfoAsync(string mint)
        {
            try
            {
                var resp = await _http.GetAsync($"{QuoteApi}/tokens?mints={mint}");
                if (!resp.IsSuccessStatusCode) return null;

                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var tokens = doc.RootElement;
                if (tokens.TryGetProperty(mint, out var info))
                {
                    return new TrendingToken
                    {
                        Symbol = info.GetProperty("symbol").GetString() ?? "",
                        Name = info.GetProperty("name").GetString() ?? "",
                        Mint = mint,
                        Decimals = info.GetProperty("decimals").GetInt32(),
                        Price = 0,
                    };
                }
                return null;
            }
            catch { return null; }
        }

        // ===== Swap Quote =====

        public async Task<SwapQuote?> GetSwapQuoteAsync(string inputMint, string outputMint, decimal amount, int slippageBps = 50)
        {
            try
            {
                var url = $"{QuoteApi}/quote?inputMint={inputMint}&outputMint={outputMint}&amount={amount}&slippageBps={slippageBps}";
                var resp = await _http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) return null;

                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                return new SwapQuote
                {
                    InputMint = inputMint,
                    OutputMint = outputMint,
                    InAmount = doc.RootElement.GetProperty("inAmount").GetRawText(),
                    OutAmount = doc.RootElement.GetProperty("outAmount").GetRawText(),
                    PriceImpactPct = doc.RootElement.GetProperty("priceImpactPct").GetRawText(),
                    RoutePlan = doc.RootElement.GetProperty("routePlan").GetArrayLength(),
                };
            }
            catch { return null; }
        }

        // ===== DTOs =====

        private class JupiterToken
        {
            public string? address { get; set; }
            public string? symbol { get; set; }
            public string? name { get; set; }
            public int decimals { get; set; }
        }
    }

    public class TrendingToken
    {
        public string Symbol { get; set; } = "";
        public string Name { get; set; } = "";
        public string Mint { get; set; } = "";
        public int Decimals { get; set; }
        public decimal Price { get; set; }
        public decimal Volume24h { get; set; }
    }

    public class SwapQuote
    {
        public string InputMint { get; set; } = "";
        public string OutputMint { get; set; } = "";
        public string InAmount { get; set; } = "";
        public string OutAmount { get; set; } = "";
        public string PriceImpactPct { get; set; } = "";
        public int RoutePlan { get; set; }
    }
}
