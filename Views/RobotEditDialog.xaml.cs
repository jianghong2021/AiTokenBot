using System.Windows;
using System.Windows.Media;

namespace AiTokenBot.Views
{
    public partial class RobotEditDialog : Window
    {
        public RobotEditModel Result { get; private set; } = new();

        public RobotEditDialog(RobotEditModel? existing = null)
        {
            InitializeComponent();

            if (existing != null)
            {
                DialogTitle.Text = "编辑机器人";
                TbName.Text = existing.Name;
                TbStrategy.Text = existing.Strategy;
                TbPersonality.Text = existing.Personality;
                TbMinProfit.Text = existing.MinProfitPercent;
                TbMaxTrade.Text = existing.MaxTradeAmount;
                TbSlippage.Text = existing.SlippagePercent;
            }
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TbName.Text))
            {
                MessageBox.Show("请输入机器人名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Result = new RobotEditModel
            {
                Name = TbName.Text,
                Strategy = TbStrategy.Text,
                Personality = TbPersonality.Text,
                MinProfitPercent = TbMinProfit.Text,
                MaxTradeAmount = TbMaxTrade.Text,
                SlippagePercent = TbSlippage.Text,
            };

            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class RobotEditModel
    {
        public string Name { get; set; } = "";
        public string Strategy { get; set; } = "";
        public string Personality { get; set; } = "";
        public string MinProfitPercent { get; set; } = "";
        public string MaxTradeAmount { get; set; } = "";
        public string SlippagePercent { get; set; } = "";
    }
}
