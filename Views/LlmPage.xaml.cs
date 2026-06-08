using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using AiTokenBot.Data;

namespace AiTokenBot.Views
{
    public partial class LlmPage : UserControl
    {
        private List<LlmPlatform> _platforms = new();

        public LlmPage()
        {
            InitializeComponent();
            LoadFromDatabase();
        }

        private void LoadFromDatabase()
        {
            _platforms = DatabaseService.Instance.LoadPlatforms();
            PlatformList.ItemsSource = _platforms;
            if (_platforms.Count > 0)
                PlatformList.SelectedIndex = 0;
        }

        private void OnPlatformSelected(object sender, SelectionChangedEventArgs e)
        {
            if (PlatformList.SelectedItem is LlmPlatform platform)
            {
                TbPlatformName.Text = platform.Name;
                TbPlatformUrl.Text = platform.BaseUrl;
                TbPlatformKey.Text = string.IsNullOrEmpty(platform.ApiKey) ? "未设置" : "••••••••";
                ModelList.ItemsSource = platform.Models;
                DetailPanel.Visibility = Visibility.Visible;
            }
            else
            {
                DetailPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void OnAddPlatform(object sender, RoutedEventArgs e)
        {
            var dialog = new PlatformEditDialog();
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                var platform = new LlmPlatform
                {
                    Name = dialog.Result.Name,
                    BaseUrl = dialog.Result.BaseUrl,
                    ApiKey = dialog.Result.ApiKey,
                };
                DatabaseService.Instance.SavePlatform(platform);
                _platforms.Add(platform);
                PlatformList.ItemsSource = null;
                PlatformList.ItemsSource = _platforms;
                PlatformList.SelectedIndex = _platforms.Count - 1;
            }
        }

        private void OnEditPlatform(object sender, RoutedEventArgs e)
        {
            if (PlatformList.SelectedItem is not LlmPlatform selected)
                return;

            var dialog = new PlatformEditDialog(selected);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                selected.Name = dialog.Result.Name;
                selected.BaseUrl = dialog.Result.BaseUrl;
                selected.ApiKey = dialog.Result.ApiKey;
                DatabaseService.Instance.SavePlatform(selected);
                PlatformList.ItemsSource = null;
                PlatformList.ItemsSource = _platforms;
                PlatformList.SelectedItem = selected;
            }
        }

        private void OnDeletePlatform(object sender, RoutedEventArgs e)
        {
            if (PlatformList.SelectedItem is not LlmPlatform selected)
                return;

            var result = MessageBox.Show($"确定要删除平台「{selected.Name}」及其所有模型吗？", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                DatabaseService.Instance.DeletePlatform(selected.Id);
                _platforms.Remove(selected);
                PlatformList.ItemsSource = null;
                PlatformList.ItemsSource = _platforms;
                if (_platforms.Count > 0)
                    PlatformList.SelectedIndex = 0;
            }
        }

        private void OnAddModel(object sender, RoutedEventArgs e)
        {
            if (PlatformList.SelectedItem is not LlmPlatform platform)
                return;

            var dialog = new ModelEditDialog();
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                var model = new LlmModel
                {
                    PlatformId = platform.Id,
                    Name = dialog.Result.Name,
                    ModelId = dialog.Result.ModelId,
                };
                DatabaseService.Instance.SaveModel(model);
                platform.Models.Add(model);
                ModelList.ItemsSource = null;
                ModelList.ItemsSource = platform.Models;
            }
        }

        private void OnEditModel(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not LlmModel model)
                return;

            var dialog = new ModelEditDialog(model);
            dialog.Owner = Window.GetWindow(this);

            if (dialog.ShowDialog() == true && dialog.Result != null)
            {
                model.Name = dialog.Result.Name;
                model.ModelId = dialog.Result.ModelId;
                DatabaseService.Instance.SaveModel(model);
                ModelList.ItemsSource = null;
                ModelList.ItemsSource = ((LlmPlatform)PlatformList.SelectedItem).Models;
            }
        }

        private void OnDeleteModel(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is LlmModel model)
            {
                var result = MessageBox.Show($"确定要删除模型「{model.Name}」吗？", "确认删除",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    DatabaseService.Instance.DeleteModel(model.Id);
                    if (PlatformList.SelectedItem is LlmPlatform platform)
                    {
                        platform.Models.Remove(model);
                        ModelList.ItemsSource = null;
                        ModelList.ItemsSource = platform.Models;
                    }
                }
            }
        }
    }
}
