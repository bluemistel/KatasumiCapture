using System;
using System.IO;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace ClipboardToTimelinePlugin
{
    public partial class ToolWindowControl : System.Windows.Controls.UserControl
    {
        public ToolWindowControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            DataContextChanged += OnDataContextChanged;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            SyncViewModelToCaptureManager();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SyncViewModelToCaptureManager();
        }

        private void SyncViewModelToCaptureManager()
        {
            if (DataContext is ToolWindowViewModel vm)
                CaptureManager.CurrentViewModel = vm;
        }

        private void BrowseSaveFolder_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ToolWindowViewModel vm || !vm.SaveImageToDirectory)
                return;

            using var dlg = new WinForms.FolderBrowserDialog
            {
                Description = "キャプチャ画像の保存先フォルダを選択",
                UseDescriptionForTitle = true,
            };

            if (Directory.Exists(vm.SaveDirectoryPath))
                dlg.SelectedPath = vm.SaveDirectoryPath;
            else
                dlg.SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            if (dlg.ShowDialog() == WinForms.DialogResult.OK && !string.IsNullOrEmpty(dlg.SelectedPath))
                vm.SaveDirectoryPath = dlg.SelectedPath;
        }
    }
}
