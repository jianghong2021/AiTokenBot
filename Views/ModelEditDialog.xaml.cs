using System.Windows;
using AiTokenBot.Data;

namespace AiTokenBot.Views
{
    public partial class ModelEditDialog : Window
    {
        public ModelEditResult? Result { get; private set; }

        public ModelEditDialog(LlmModel? existing = null)
        {
            InitializeComponent();

            if (existing != null)
            {
                DialogTitle.Text = "编辑模型";
                TbName.Text = existing.Name;
                TbModelId.Text = existing.ModelId;
            }
        }

        private void OnSave(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TbName.Text) || string.IsNullOrWhiteSpace(TbModelId.Text))
            {
                MessageBox.Show("请填写所有字段", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Result = new ModelEditResult
            {
                Name = TbName.Text.Trim(),
                ModelId = TbModelId.Text.Trim(),
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

    public class ModelEditResult
    {
        public string Name { get; set; } = "";
        public string ModelId { get; set; } = "";
    }
}
