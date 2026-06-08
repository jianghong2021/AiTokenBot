using System.Windows;

namespace AiTokenBot.Views
{
    public partial class ImportWalletDialog : Window
    {
        public WalletImportResult? Result { get; private set; }

        public ImportWalletDialog()
        {
            InitializeComponent();
            SetActiveTab(true);
        }

        private void SetActiveTab(bool isPrivateKey)
        {
            PanelPrivateKey.Visibility = isPrivateKey ? Visibility.Visible : Visibility.Collapsed;
            PanelMnemonic.Visibility = isPrivateKey ? Visibility.Collapsed : Visibility.Visible;
            BtnPrivateKey.FontWeight = isPrivateKey ? FontWeights.SemiBold : FontWeights.Normal;
            BtnMnemonic.FontWeight = isPrivateKey ? FontWeights.Normal : FontWeights.SemiBold;
        }

        private void OnSwitchPrivateKey(object sender, RoutedEventArgs e) => SetActiveTab(true);
        private void OnSwitchMnemonic(object sender, RoutedEventArgs e) => SetActiveTab(false);

        private void OnImportClick(object sender, RoutedEventArgs e)
        {
            bool isPk = PanelPrivateKey.Visibility == Visibility.Visible;
            var name = isPk ? TbPkName.Text.Trim() : TbMnName.Text.Trim();
            var secret = isPk ? TbPrivateKey.Password.Trim() : TbMnemonic.Text.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("请输入钱包名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(secret))
            {
                MessageBox.Show(isPk ? "请输入私钥" : "请输入助记词", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Simulate deriving address from input (mock data)
            var shortAddr = "0x" + name.GetHashCode().ToString("X8");

            Result = new WalletImportResult
            {
                Name = name,
                Address = shortAddr + "...",
                Type = isPk ? "私钥" : "助记词",
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

    public class WalletImportResult
    {
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public string Type { get; set; } = "";
    }
}
