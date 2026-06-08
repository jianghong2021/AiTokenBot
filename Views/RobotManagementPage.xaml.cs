using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AiTokenBot.Data;
using AiTokenBot.Services;

namespace AiTokenBot.Views
{
    public partial class RobotManagementPage : UserControl
    {
        private List<RobotModel> _robots = new();

        public RobotManagementPage()
        {
            InitializeComponent();
            LoadFromDatabase();

            TradingEngine.Instance.OnBotLog += (id, msg) =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (RobotList.SelectedItem is RobotModel r && r.Id == id)
                        TbBotLog.AppendText(msg + "\n");
                }, DispatcherPriority.Background);
            };

            TradingEngine.Instance.OnBotStateChanged += id =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    _robots = DatabaseService.Instance.LoadRobots();
                    RefreshList();
                }, DispatcherPriority.Background);
            };
        }

        private void LoadFromDatabase()
        {
            _robots = DatabaseService.Instance.LoadRobots();
            RefreshList();
        }

        private void RefreshList()
        {
            var selectedIndex = RobotList.SelectedIndex;
            RobotList.ItemsSource = null;
            RobotList.ItemsSource = _robots;

            if (_robots.Count > 0)
            {
                RobotList.SelectedIndex = selectedIndex >= 0 && selectedIndex < _robots.Count ? selectedIndex : 0;
                DetailPanel.Visibility = Visibility.Visible;
            }
            else
            {
                DetailPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void OnRobotSelected(object sender, SelectionChangedEventArgs e)
        {
            if (RobotList.SelectedItem is RobotModel robot)
            {
                DetailPanel.DataContext = robot;
                DetailPanel.Visibility = Visibility.Visible;
                PopulateLlmCombo(robot);
                TbBotLog.Text = TradingEngine.Instance.GetLog(robot.Id);
            }
            else
            {
                DetailPanel.Visibility = Visibility.Collapsed;
            }
        }

        private bool _llmComboUpdating;

        private void PopulateLlmCombo(RobotModel robot)
        {
            _llmComboUpdating = true;
            var allModels = DatabaseService.Instance.GetAllModels();
            CbLlmModel.ItemsSource = allModels;

            if (robot.LlmModelId > 0)
            {
                var selected = allModels.Find(m => m.Id == robot.LlmModelId);
                CbLlmModel.SelectedItem = selected;
                if (selected == null && allModels.Count > 0)
                {
                    CbLlmModel.SelectedIndex = 0;
                    robot.LlmModelId = allModels[0].Id;
                    DatabaseService.Instance.SaveRobot(robot);
                }
            }
            else if (allModels.Count > 0)
            {
                CbLlmModel.SelectedIndex = 0;
                robot.LlmModelId = allModels[0].Id;
                DatabaseService.Instance.SaveRobot(robot);
            }

            _llmComboUpdating = false;
        }

        private void OnLlmModelChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_llmComboUpdating) return;

            if (CbLlmModel.SelectedItem is LlmModel model &&
                RobotList.SelectedItem is RobotModel robot)
            {
                robot.LlmModelId = model.Id;
                DatabaseService.Instance.SaveRobot(robot);
            }
        }

        private void OnToggleRunningClick(object sender, RoutedEventArgs e)
        {
            if (RobotList.SelectedItem is not RobotModel robot)
                return;

            var engine = TradingEngine.Instance;
            if (engine.IsRunning(robot.Id))
            {
                engine.Stop(robot.Id);
            }
            else
            {
                var robots = DatabaseService.Instance.LoadRobots();
                var fresh = robots.Find(r => r.Id == robot.Id);
                if (fresh != null)
                {
                    engine.Start(fresh);
                    TbBotLog.Text = "";
                }
            }

            // Reload from DB to reflect running state
            _robots = DatabaseService.Instance.LoadRobots();
            RefreshList();
        }

        private void OnDepositClick(object sender, RoutedEventArgs e)
        {
            if (RobotList.SelectedItem is not RobotModel robot)
                return;

            var dialog = new TransferDialog(true, robot.Name, robot.Balance);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                robot.Balance += dialog.Amount;
                DatabaseService.Instance.SaveRobot(robot);
                RefreshList();
            }
        }

        private void OnWithdrawClick(object sender, RoutedEventArgs e)
        {
            if (RobotList.SelectedItem is not RobotModel robot)
                return;

            var dialog = new TransferDialog(false, robot.Name, robot.Balance);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                if (dialog.Amount > robot.Balance)
                {
                    MessageBox.Show("余额不足", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                robot.Balance -= dialog.Amount;
                DatabaseService.Instance.SaveRobot(robot);
                RefreshList();
            }
        }

        private void OnAddRobot(object sender, RoutedEventArgs e)
        {
            var dialog = new RobotEditDialog();
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                var r = dialog.Result;
                var robot = new RobotModel
                {
                    Name = r.Name,
                    Strategy = r.Strategy,
                    Personality = r.Personality,
                    MinProfitPercent = r.MinProfitPercent,
                    MaxTradeAmount = r.MaxTradeAmount,
                    SlippagePercent = r.SlippagePercent,
                    IsRunning = false,
                    Balance = 0,
                    TodayPnL = "$ 0.00",
                    TotalPnL = "$ 0.00",
                };
                DatabaseService.Instance.SaveRobot(robot);
                _robots.Add(robot);
                RefreshList();
                RobotList.SelectedIndex = _robots.Count - 1;
            }
        }

        private void OnEditRobot(object sender, RoutedEventArgs e)
        {
            if (RobotList.SelectedItem is not RobotModel selected)
                return;

            var dialog = new RobotEditDialog(new RobotEditModel
            {
                Name = selected.Name,
                Strategy = selected.Strategy,
                Personality = selected.Personality,
                MinProfitPercent = selected.MinProfitPercent,
                MaxTradeAmount = selected.MaxTradeAmount,
                SlippagePercent = selected.SlippagePercent,
            });
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true)
            {
                var r = dialog.Result;
                selected.Name = r.Name;
                selected.Strategy = r.Strategy;
                selected.Personality = r.Personality;
                selected.MinProfitPercent = r.MinProfitPercent;
                selected.MaxTradeAmount = r.MaxTradeAmount;
                selected.SlippagePercent = r.SlippagePercent;
                DatabaseService.Instance.SaveRobot(selected);
                RefreshList();
            }
        }

        private void OnDeleteRobot(object sender, RoutedEventArgs e)
        {
            if (RobotList.SelectedItem is not RobotModel selected)
                return;

            var result = MessageBox.Show(
                $"确定要删除机器人「{selected.Name}」吗？此操作不可恢复。",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                DatabaseService.Instance.DeleteRobot(selected.Id);
                _robots.Remove(selected);
                RefreshList();
            }
        }
    }

    public class RobotModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Strategy { get; set; } = "";
        public string Personality { get; set; } = "";
        public string MinProfitPercent { get; set; } = "";
        public string MaxTradeAmount { get; set; } = "";
        public string SlippagePercent { get; set; } = "";
        public bool IsRunning { get; set; }
        public decimal Balance { get; set; }
        public string TodayPnL { get; set; } = "$ 0.00";
        public string TotalPnL { get; set; } = "$ 0.00";
        public int LlmModelId { get; set; }
        public List<PositionItem> Positions { get; set; } = new();

        public string BalanceDisplay => $"$ {Balance:F2}";
        public string StatusText => IsRunning ? "运行中" : "已暂停";
        public string ToggleText => IsRunning ? "暂停" : "启动";
        public Brush StatusBrush => IsRunning
            ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
            : new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07));
        public Brush PnLBrushes => TodayPnL.StartsWith("+")
            ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
            : TodayPnL.StartsWith("-")
                ? new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36))
                : new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));
    }
}
