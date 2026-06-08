using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using AiTokenBot.Views;

namespace AiTokenBot
{
    public partial class MainWindow : Window
    {
        private readonly HomePage _homePage = new();
        private readonly RobotManagementPage _robotPage = new();
        private readonly TradeHistoryPage _tradePage = new();
        private readonly WalletPage _walletPage = new();
        private readonly LlmPage _llmPage = new();
        private readonly SettingsPage _settingsPage = new();

        public MainWindow()
        {
            InitializeComponent();

            var navItems = new List<NavItem>
            {
                new("🏠", "主页", _homePage),
                new("🤖", "机器人管理", _robotPage),
                new("📊", "交易记录", _tradePage),
                new("💼", "钱包", _walletPage),
                new("🧠", "大模型", _llmPage),
                new("⚙️", "设置", _settingsPage),
            };

            NavList.ItemsSource = navItems;
            NavList.SelectedIndex = 0;
        }

        private void OnNavSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NavList.SelectedItem is NavItem nav)
            {
                ContentArea.Content = nav.Page;
            }
        }
    }

    public class NavItem
    {
        public string Icon { get; }
        public string Title { get; }
        public ContentControl Page { get; }

        public NavItem(string icon, string title, ContentControl page)
        {
            Icon = icon;
            Title = title;
            Page = page;
        }
    }
}
