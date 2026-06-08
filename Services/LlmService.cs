using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AiTokenBot.Data;
using AiTokenBot.Views;

namespace AiTokenBot.Services
{
    public class LlmService
    {
        private static readonly Dictionary<string, string> TokenMints = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SOL"] = "So11111111111111111111111111111111111111112",
            ["USDC"] = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v",
            ["BONK"] = "DezXAZ8z7PnrnRJjz3wXBoRgixCa6xjnB7YaB1pPB263",
            ["JUP"] = "JUPyiwrYJFskUPiHa7hkeR8VUtAeFoSYbKedZNsDvCN",
            ["RAY"] = "4k3Dyjzvzp8eMZWUXbBCjEvwSkkk59S5iCNLY3QrkX6R",
        };

        private readonly JupiterService _jupiter;
        private readonly HttpClient _http;

        public LlmService(JupiterService jupiter)
        {
            _jupiter = jupiter;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        private (string baseUrl, string apiKey, string modelId) GetConfig(RobotModel robot)
        {
            var db = DatabaseService.Instance;
            var platforms = db.LoadPlatforms();
            var allModels = db.GetAllModels();
            var model = allModels.Find(m => m.Id == robot.LlmModelId);
            var platform = platforms.Find(p => p.Id == model?.PlatformId);
            return (
                platform?.BaseUrl ?? "https://api.deepseek.com",
                platform?.ApiKey ?? "",
                model?.ModelId ?? "deepseek-v4-pro"
            );
        }

        // Simple tool definitions as JSON
        private static readonly JsonElement ToolsJson = JsonSerializer.Deserialize<JsonElement>("""
        [
            {"type":"function","function":{"name":"get_price","description":"获取代币当前价格(USD)","parameters":{"type":"object","properties":{"symbol":{"type":"string","description":"代币符号"}},"required":["symbol"]}}},
            {"type":"function","function":{"name":"get_trending","description":"获取Solana热门代币列表","parameters":{"type":"object","properties":{}}}},
            {"type":"function","function":{"name":"get_swap_quote","description":"获取交易报价","parameters":{"type":"object","properties":{"from_symbol":{"type":"string"},"to_symbol":{"type":"string"},"amount":{"type":"number"}},"required":["from_symbol","to_symbol","amount"]}}},
            {"type":"function","function":{"name":"execute_buy","description":"用USDC买入代币","parameters":{"type":"object","properties":{"symbol":{"type":"string"},"amount":{"type":"number","description":"USDC数量"}},"required":["symbol","amount"]}}},
            {"type":"function","function":{"name":"execute_sell","description":"卖出代币换USDC","parameters":{"type":"object","properties":{"symbol":{"type":"string"},"amount":{"type":"number","description":"代币数量"}},"required":["symbol","amount"]}}},
            {"type":"function","function":{"name":"hold","description":"保持不动","parameters":{"type":"object","properties":{"reason":{"type":"string"}},"required":["reason"]}}}
        ]
        """);

        public async Task<List<ToolDecision>> GetDecisionsAsync(RobotModel robot, string marketContext)
        {
            var decisions = new List<ToolDecision>();
            var (baseUrl, apiKey, modelId) = GetConfig(robot);
            var endpoint = baseUrl.TrimEnd('/') + "/v1/chat/completions";

            var messages = new List<object>
            {
                new { role = "system", content = robot.Personality + "\n\n你是Solana自主交易机器人，通过工具函数交互。每次做1个决策。" },
                new { role = "user", content = marketContext },
            };

            int maxRounds = 3;
            while (maxRounds-- > 0)
            {
                var payload = new
                {
                    model = modelId,
                    messages,
                    tools = ToolsJson,
                    tool_choice = "auto",
                    temperature = 0.7,
                };

                var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Authorization", $"Bearer {apiKey}");

                try
                {
                    var response = await _http.SendAsync(request);
                    var json = await response.Content.ReadAsStringAsync();

                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (!response.IsSuccessStatusCode)
                    {
                        var err = root.TryGetProperty("error", out var e) ? e.GetProperty("message").GetString() : response.StatusCode.ToString();
                        decisions.Add(new ToolDecision { ToolName = "error", ToolArgs = err ?? "", Result = "" });
                        break;
                    }

                    var choice = root.GetProperty("choices")[0];
                    var msg = choice.GetProperty("message");

                    // Check for tool calls
                    if (msg.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.GetArrayLength() > 0)
                    {
                        // Add assistant message with tool calls
                        messages.Add(new
                        {
                            role = "assistant",
                            content = (string?)null,
                            tool_calls = JsonSerializer.Deserialize<object>(toolCalls.GetRawText()),
                        });

                        foreach (var tc in toolCalls.EnumerateArray())
                        {
                            var fn = tc.GetProperty("function");
                            var fnName = fn.GetProperty("name").GetString() ?? "";
                            var fnArgs = fn.GetProperty("arguments").GetString() ?? "";
                            var toolId = tc.GetProperty("id").GetString() ?? "";

                            var result = await ExecuteToolCallAsync(fnName, fnArgs);
                            decisions.Add(new ToolDecision { ToolName = fnName, ToolArgs = fnArgs, Result = result });

                            messages.Add(new
                            {
                                role = "tool",
                                tool_call_id = toolId,
                                content = result,
                            });
                        }
                    }
                    else
                    {
                        var text = msg.GetProperty("content").GetString() ?? "";
                        if (!string.IsNullOrWhiteSpace(text))
                            decisions.Add(new ToolDecision { ToolName = "message", ToolArgs = text, Result = "" });
                        break;
                    }
                }
                catch (Exception ex)
                {
                    decisions.Add(new ToolDecision { ToolName = "error", ToolArgs = ex.Message, Result = "" });
                    break;
                }
            }

            return decisions;
        }

        private async Task<string> ExecuteToolCallAsync(string name, string args)
        {
            try
            {
                using var doc = JsonDocument.Parse(args);
                var root = doc.RootElement;

                switch (name)
                {
                    case "get_price":
                        var sym = root.GetProperty("symbol").GetString() ?? "";
                        if (TokenMints.TryGetValue(sym.ToUpper(), out var mint))
                        {
                            var prices = await _jupiter.GetPricesAsync(mint);
                            if (prices.TryGetValue(mint, out var price))
                                return $"当前 {sym} 价格: ${price:F4} USD";
                            return $"未获取到 {sym} 价格";
                        }
                        return $"不支持的代币: {sym}。支持: {string.Join(", ", TokenMints.Keys)}";

                    case "get_trending":
                        var trending = await _jupiter.GetTrendingTokensAsync();
                        var lines = new List<string> { "Solana热门代币:" };
                        foreach (var t in trending.Take(15))
                            lines.Add($"  {t.Symbol,-8} {t.Name}");
                        return string.Join("\n", lines);

                    case "get_swap_quote":
                        var from = root.GetProperty("from_symbol").GetString() ?? "";
                        var to = root.GetProperty("to_symbol").GetString() ?? "";
                        var amt = root.GetProperty("amount").GetDecimal();
                        if (TokenMints.TryGetValue(from.ToUpper(), out var im) && TokenMints.TryGetValue(to.ToUpper(), out var om))
                        {
                            var dec = from.Equals("USDC", StringComparison.OrdinalIgnoreCase) ? 1_000_000m : 1_000_000_000m;
                            var rawAmt = (long)(amt * dec);
                            var quote = await _jupiter.GetSwapQuoteAsync(im, om, rawAmt);
                            if (quote != null)
                                return $"报价 {from}→{to}: 输入{amt}, 输出~{decimal.Parse(quote.OutAmount) / dec:F4}, 价格影响{quote.PriceImpactPct}%";
                            return $"未找到 {from}→{to} 路由";
                        }
                        return "不支持的交易对";

                    case "execute_buy":
                        var buySym = root.GetProperty("symbol").GetString() ?? "";
                        var buyAmt = root.GetProperty("amount").GetDecimal();
                        if (!TokenMints.ContainsKey(buySym.ToUpper()))
                            return $"不支持交易: {buySym}";
                        return $"BUY_SIGNAL: {buyAmt} USDC → {buySym}";

                    case "execute_sell":
                        var sellSym = root.GetProperty("symbol").GetString() ?? "";
                        var sellAmt = root.GetProperty("amount").GetDecimal();
                        if (!TokenMints.ContainsKey(sellSym.ToUpper()))
                            return $"不支持交易: {sellSym}";
                        return $"SELL_SIGNAL: {sellAmt} {sellSym} → USDC";

                    case "hold":
                        var reason = root.TryGetProperty("reason", out var r) ? r.GetString() : "等待机会";
                        return $"保持不动: {reason}";

                    default:
                        return $"未知工具: {name}";
                }
            }
            catch (Exception ex) { return $"工具异常: {ex.Message}"; }
        }
    }

    public class ToolDecision
    {
        public string ToolName { get; set; } = "";
        public string ToolArgs { get; set; } = "";
        public string Result { get; set; } = "";
        public override string ToString() => $"[{ToolName}] {ToolArgs} → {Result}";
    }
}
