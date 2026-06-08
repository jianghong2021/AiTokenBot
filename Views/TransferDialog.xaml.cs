using System.Windows;

namespace AiTokenBot.Views
{
    public partial class TransferDialog : Window
    {
        public decimal Amount { get; private set; }
        public bool IsDeposit { get; }

        public TransferDialog(bool isDeposit, string robotName, decimal currentBalance)
        {
            InitializeComponent();
            IsDeposit = isDeposit;

            if (isDeposit)
            {
                DialogTitle.Text = "转入资金";
                DialogSub.Text = $"从主钱包转入 USDC 到「{robotName}」";
                BtnConfirm.Content = "确认转入";
            }
            else
            {
                DialogTitle.Text = "转出资金";
                DialogSub.Text = $"从「{robotName}」转出 USDC 到主钱包 (当前余额: ${currentBalance:F2})";
                BtnConfirm.Content = "确认转出";
            }
        }

        private void OnConfirmClick(object sender, RoutedEventArgs e)
        {
            if (!decimal.TryParse(TbAmount.Text.Trim(), out var amount) || amount <= 0)
            {
                MessageBox.Show("请输入有效的金额", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Amount = amount;
            DialogResult = true;
            Close();
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
