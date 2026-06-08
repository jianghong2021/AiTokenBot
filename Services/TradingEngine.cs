using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AiTokenBot.Data;
using AiTokenBot.Views;

namespace AiTokenBot.Services
{
    public class TradingEngine
    {
        private static TradingEngine? _instance;
        public static TradingEngine Instance => _instance ??= new TradingEngine();

        private readonly JupiterService _jupiter = new();
        private readonly LlmService _llm;
        private readonly ConcurrentDictionary<int, CancellationTokenSource> _runningBots = new();
        private readonly ConcurrentDictionary<int, string> _botLogs = new();

        public event Action<int, string>? OnBotLog;
        public event Action<int>? OnBotStateChanged;

        private TradingEngine()
        {
            _llm = new LlmService(_jupiter);
        }

        public bool IsRunning(int robotId) => _runningBots.ContainsKey(robotId);

        public string GetLog(int robotId) => _botLogs.TryGetValue(robotId, out var log) ? log : "";

        public void Start(RobotModel robot)
        {
            if (_runningBots.ContainsKey(robot.Id)) return;

            var cts = new CancellationTokenSource();
            _runningBots[robot.Id] = cts;

            var token = cts.Token;
            Task.Run(() => BotLoop(robot, token), token);

            robot.IsRunning = true;
            DatabaseService.Instance.SaveRobot(robot);
            OnBotStateChanged?.Invoke(robot.Id);
        }

        public void Stop(int robotId)
        {
            if (!_runningBots.TryRemove(robotId, out var cts)) return;

            cts.Cancel();

            var robots = DatabaseService.Instance.LoadRobots();
            var robot = robots.Find(r => r.Id == robotId);
            if (robot != null)
            {
                robot.IsRunning = false;
                DatabaseService.Instance.SaveRobot(robot);
            }

            OnBotStateChanged?.Invoke(robotId);
        }

        private async Task BotLoop(RobotModel robot, CancellationToken token)
        {
            Log(robot.Id, $"🚀 机器人「{robot.Name}」启动，策略: {robot.Strategy}");
            var db = DatabaseService.Instance;
            var random = new Random();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Reload fresh data
                    var robots = db.LoadRobots();
                    var current = robots.Find(r => r.Id == robot.Id);
                    if (current == null) break;
                    robot = current;

                    // Build market context
                    var prices = await _jupiter.GetPricesAsync(
                        "So11111111111111111111111111111111111111112",
                        "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v",
                        "DezXAZ8z7PnrnRJjz3wXBoRgixCa6xjnB7YaB1pPB263",
                        "JUPyiwrYJFskUPiHa7hkeR8VUtAeFoSYbKedZNsDvCN");

                    var priceLines = new List<string>();
                    var mintMap = new Dictionary<string, string> { ["So11111111111111111111111111111111111111112"] = "SOL", ["EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v"] = "USDC", ["DezXAZ8z7PnrnRJjz3wXBoRgixCa6xjnB7YaB1pPB263"] = "BONK", ["JUPyiwrYJFskUPiHa7hkeR8VUtAeFoSYbKedZNsDvCN"] = "JUP" };
                    foreach (var kv in prices)
                        priceLines.Add($"  {(mintMap.TryGetValue(kv.Key, out var s) ? s : kv.Key)}: ${kv.Value:F4}");

                    var posLines = new List<string>();
                    foreach (var p in robot.Positions)
                        posLines.Add($"  {p.Token}: {p.Amount} @ {p.CurrentPrice} (PnL: {p.PnL})");

                    var context = $@"=== 当前市场行情 ===
{string.Join("\n", priceLines)}

=== 当前持仓 ===
{(posLines.Count > 0 ? string.Join("\n", posLines) : "  空仓")}

=== 账户状态 ===
  余额: {robot.BalanceDisplay}
  策略: {robot.Strategy}
  最低盈利: {robot.MinProfitPercent}, 最大交易量: {robot.MaxTradeAmount}, 滑点: {robot.SlippagePercent}

=== 决策指令 ===
分析当前市场，决定下一步操作。调用合适的工具函数。";

                    Log(robot.Id, $"📊 分析市场...");

                    var decisions = await _llm.GetDecisionsAsync(robot, context);

                    foreach (var d in decisions)
                    {
                        Log(robot.Id, d.ToString());

                        // Process buy/sell decisions
                        if (d.ToolName == "execute_buy")
                        {
                            ProcessBuyDecision(robot, d.ToolArgs);
                        }
                        else if (d.ToolName == "execute_sell")
                        {
                            ProcessSellDecision(robot, d.ToolArgs);
                        }
                    }

                    // Save state
                    db.SaveRobot(robot);

                    // Wait for next cycle (1-5 minutes random to simulate scan interval)
                    var delay = random.Next(60_000, 300_000);
                    Log(robot.Id, $"⏳ 等待 {delay / 1000}s 后下次扫描...");
                    await Task.Delay(delay, token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Log(robot.Id, $"❌ 异常: {ex.Message}");
                    try { await Task.Delay(30_000, token); } catch { break; }
                }
            }

            Log(robot.Id, $"⏹ 机器人「{robot.Name}」已停止");
        }

        private void ProcessBuyDecision(RobotModel robot, string args)
        {
            try
            {
                using var doc = JsonDocument.Parse(args);
                var symbol = doc.RootElement.GetProperty("symbol").GetString() ?? "";
                var amount = doc.RootElement.GetProperty("amount").GetDecimal();

                if (amount > robot.Balance)
                {
                    Log(robot.Id, $"⚠ 买入失败: 余额不足 (需要 ${amount:F2}, 可用 ${robot.Balance:F2})");
                    return;
                }

                robot.Balance -= amount;

                var existing = robot.Positions.Find(p => p.Token.Equals(symbol, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Amount = (decimal.Parse(existing.Amount.Replace(",", "")) + amount).ToString("F2");
                }
                else
                {
                    robot.Positions.Add(new Views.PositionItem
                    {
                        Token = symbol, Amount = amount.ToString("F4"),
                        AvgPrice = "$ 0.00", CurrentPrice = "$ 0.00", PnL = "$ 0.00",
                    });
                }

                // Log trade
                LogTrade(robot.Name, $"{symbol}/USDC", "买入", $"{amount:F2} USDC → {symbol}", "+ $0.00");

                Log(robot.Id, $"✅ 买入 {amount:F2} USDC → {symbol}, 余额: ${robot.Balance:F2}");
            }
            catch (Exception ex) { Log(robot.Id, $"❌ 解析买入参数失败: {ex.Message}"); }
        }

        private void ProcessSellDecision(RobotModel robot, string args)
        {
            try
            {
                using var doc = JsonDocument.Parse(args);
                var symbol = doc.RootElement.GetProperty("symbol").GetString() ?? "";
                var amount = doc.RootElement.GetProperty("amount").GetDecimal();

                var position = robot.Positions.Find(p => p.Token.Equals(symbol, StringComparison.OrdinalIgnoreCase));
                if (position == null)
                {
                    Log(robot.Id, $"⚠ 卖出失败: 没有 {symbol} 持仓");
                    return;
                }

                var held = decimal.Parse(position.Amount.Replace(",", ""));
                if (amount > held)
                {
                    Log(robot.Id, $"⚠ 卖出失败: 持仓不足 (需要 {amount}, 持有 {held})");
                    return;
                }

                // Simulate profit: add 0.3% to balance
                var revenue = amount * 1.003m;
                robot.Balance += revenue;

                var remaining = held - amount;
                if (remaining <= 0)
                    robot.Positions.Remove(position);
                else
                    position.Amount = remaining.ToString("F4");

                var profit = revenue - amount;
                LogTrade(robot.Name, $"{symbol}/USDC", "卖出", $"{amount:F4} {symbol} → {revenue:F2} USDC", $"+ ${profit:F2}");
                Log(robot.Id, $"✅ 卖出 {amount:F4} {symbol} → {revenue:F2} USDC, 余额: ${robot.Balance:F2}");
            }
            catch (Exception ex) { Log(robot.Id, $"❌ 解析卖出参数失败: {ex.Message}"); }
        }

        private void LogTrade(string robot, string pair, string direction, string detail, string pnl)
        {
            try
            {
                var db = DatabaseService.Instance;
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={AppDomain.CurrentDomain.BaseDirectory}aitokenbot.db");
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO TradeRecords (Time, Robot, Pair, Direction, Detail, PnL, Status) VALUES (@t,@r,@p,@d,@dt,@pl,@s)";
                cmd.Parameters.AddWithValue("@t", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                cmd.Parameters.AddWithValue("@r", robot);
                cmd.Parameters.AddWithValue("@p", pair);
                cmd.Parameters.AddWithValue("@d", direction);
                cmd.Parameters.AddWithValue("@dt", detail);
                cmd.Parameters.AddWithValue("@pl", (object?)pnl ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@s", "已完成");
                cmd.ExecuteNonQuery();
            }
            catch { /* log failure is non-fatal */ }
        }

        private void Log(int robotId, string msg)
        {
            var entry = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            _botLogs.AddOrUpdate(robotId, entry, (_, old) => old + "\n" + entry);
            OnBotLog?.Invoke(robotId, entry);
        }
    }
}
