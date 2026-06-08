using System.Windows.Controls;
using System.Windows.Media;

namespace AiTokenBot.Views
{
    public partial class HomePage : UserControl
    {
        public HomePage()
        {
            InitializeComponent();
        }
    }

    public class ActivityItem
    {
        public string Type { get; set; } = "";
        public string Description { get; set; } = "";
        public string Time { get; set; } = "";
    }

    public class PositionItem
    {
        public string Token { get; set; } = "";
        public string Amount { get; set; } = "";
        public string AvgPrice { get; set; } = "";
        public string CurrentPrice { get; set; } = "";
        public string PnL { get; set; } = "";
        public string Robot { get; set; } = "";
        public Brush PnLBrush => string.IsNullOrEmpty(PnL) ? Brushes.Gray
            : PnL.StartsWith("+") ? new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
            : PnL.StartsWith("-") ? new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36))
            : Brushes.Gray;
    }
}
