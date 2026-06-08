using System.Windows.Controls;
using System.Windows.Media;
using AiTokenBot.Data;

namespace AiTokenBot.Views
{
    public partial class TradeHistoryPage : UserControl
    {
        public TradeHistoryPage()
        {
            InitializeComponent();
            LoadTrades();
        }

        private void LoadTrades()
        {
            var trades = DatabaseService.Instance.LoadTradeRecords();
            TradeList.ItemsSource = trades;
        }
    }

    public class TradeRecord
    {
        public int Id { get; set; }
        public string Time { get; set; } = "";
        public string Robot { get; set; } = "";
        public string Pair { get; set; } = "";
        public string Direction { get; set; } = "";
        public string Detail { get; set; } = "";
        public string? PnL { get; set; }
        public string Status { get; set; } = "";

        public Brush DirectionColor => Direction == "买入"
            ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
            : Direction == "卖出"
                ? new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36))
                : new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));

        public Brush? PnLColor => PnL switch
        {
            null => null,
            { } s when s.StartsWith("+") => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
            { } s when s.StartsWith("-") => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
            _ => new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
        };
    }
}
