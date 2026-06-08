using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AiTokenBot.Data;

namespace AiTokenBot.Views
{
    public partial class SettingsPage : UserControl
    {
        public SettingsPage()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            var settings = DatabaseService.Instance.LoadSettings();

            if (settings.TryGetValue("rpc_url", out var rpc))
                TbRpcUrl.Text = rpc;
            if (settings.TryGetValue("jupiter_url", out var jup))
                TbJupiterUrl.Text = jup;
            if (settings.TryGetValue("jupiter_api_key", out var key))
                TbJupiterKey.Password = key;

            if (settings.TryGetValue("theme", out var theme))
            {
                var item = CbTheme.Items.Cast<ComboBoxItem>()
                    .FirstOrDefault(i => i.Tag?.ToString() == theme);
                if (item != null) CbTheme.SelectedItem = item;
            }

            if (settings.TryGetValue("notify_trade", out var nt))
                CbNotifyTrade.IsChecked = nt == "true";
            if (settings.TryGetValue("notify_alert", out var na))
                CbNotifyAlert.IsChecked = na == "true";
        }

        private void OnSaveClick(object sender, RoutedEventArgs e)
        {
            var db = DatabaseService.Instance;
            db.SaveSetting("rpc_url", TbRpcUrl.Text);
            db.SaveSetting("jupiter_url", TbJupiterUrl.Text);
            db.SaveSetting("jupiter_api_key", TbJupiterKey.Password);

            if (CbTheme.SelectedItem is ComboBoxItem item)
                db.SaveSetting("theme", item.Tag?.ToString() ?? "dark");

            db.SaveSetting("notify_trade", CbNotifyTrade.IsChecked == true ? "true" : "false");
            db.SaveSetting("notify_alert", CbNotifyAlert.IsChecked == true ? "true" : "false");

            MessageBox.Show("设置已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
