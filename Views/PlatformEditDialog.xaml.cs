using System.Windows;
using AiTokenBot.Data;

namespace AiTokenBot.Views
{
    public partial class PlatformEditDialog : Window
    {
        public PlatformEditResult? Result { get; private set; }

        public PlatformEditDialog(LlmPlatform? existing = null)
        {
            InitializeComponent();

            if (existing != null)
            {
                DialogTitle.Text = "编辑平台";
                TbName.Text = existing.Name;
                TbBaseUrl.Text = existing.BaseUrl;
                TbApiKey.Password = existing.ApiKey;
            }
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TbName.Text))
            {
                MessageBox.Show("请输入平台名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Result = new PlatformEditResult
            {
                Name = TbName.Text.Trim(),
                BaseUrl = TbBaseUrl.Text.Trim(),
                ApiKey = TbApiKey.Password.Trim(),
            };

            DialogResult = true;
            Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class PlatformEditResult
    {
        public string Name { get; set; } = "";
        public string BaseUrl { get; set; } = "";
        public string ApiKey { get; set; } = "";
    }
}
