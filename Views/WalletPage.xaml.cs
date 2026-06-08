using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using QRCoder;
using AiTokenBot.Data;

namespace AiTokenBot.Views
{
    public partial class WalletPage : UserControl
    {
        private List<WalletInfo> _wallets = new();

        public WalletPage()
        {
            InitializeComponent();
            LoadFromDatabase();
        }

        private void LoadFromDatabase()
        {
            _wallets = DatabaseService.Instance.LoadWallets();
            WalletSelector.ItemsSource = _wallets;
            if (_wallets.Count > 0)
                WalletSelector.SelectedIndex = 0;
        }

        private void OnWalletSelected(object sender, SelectionChangedEventArgs e)
        {
            if (WalletSelector.SelectedItem is not WalletInfo wallet)
                return;

            TbAddress.Text = wallet.Address.Length > 30
                ? wallet.Address[..10] + "..." + wallet.Address[^10..]
                : wallet.Address;
            TbDepositAddress.Text = wallet.Address;
            TbImportType.Text = "导入方式: " + wallet.ImportType;
            DetailScroll.Visibility = Visibility.Visible;

            GenerateQrCode(wallet.Address);
        }

        private void GenerateQrCode(string address)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(address, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrData);
            var bytes = qrCode.GetGraphic(10);

            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = new MemoryStream(bytes);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();

            QrImage.Source = image;
        }

        private void OnImportClick(object sender, RoutedEventArgs e)
        {
            var dialog = new ImportWalletDialog();
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                var wallet = new WalletInfo
                {
                    Name = dialog.Result.Name,
                    Address = Guid.NewGuid().ToString("N").ToUpper(),
                    ImportType = dialog.Result.Type,
                };
                DatabaseService.Instance.SaveWallet(wallet);
                _wallets.Add(wallet);

                WalletSelector.ItemsSource = null;
                WalletSelector.ItemsSource = _wallets;
                WalletSelector.SelectedIndex = _wallets.Count - 1;
            }
        }

        private void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            if (WalletSelector.SelectedItem is not WalletInfo selected)
                return;

            if (_wallets.Count <= 1)
            {
                MessageBox.Show("至少保留一个钱包", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"确定要删除钱包「{selected.Name}」吗？钱包内资产不会被丢失，可通过私钥/助记词重新导入。",
                "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                DatabaseService.Instance.DeleteWallet(selected.Id);
                _wallets.Remove(selected);
                WalletSelector.ItemsSource = null;
                WalletSelector.ItemsSource = _wallets;
                WalletSelector.SelectedIndex = 0;
            }
        }

        private void OnCopyAddress(object sender, RoutedEventArgs e)
        {
            var text = TbDepositAddress.Text;
            Clipboard.SetText(text);
            MessageBox.Show("地址已复制到剪贴板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public class WalletInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public string ImportType { get; set; } = "";
    }

    public class TokenItem
    {
        public string Token { get; set; } = "";
        public string Name { get; set; } = "";
        public string Amount { get; set; } = "";
        public string ValueUSD { get; set; } = "";
    }
}
