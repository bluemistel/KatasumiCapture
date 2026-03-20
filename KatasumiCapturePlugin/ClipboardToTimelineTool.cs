using System;
using YukkuriMovieMaker.Plugin;

namespace ClipboardToTimelinePlugin
{
    public class ClipboardToTimelineTool : IToolPlugin, IDisposable
    {
        public string Name => "かたすみキャプチャ";

        public Type ViewModelType => typeof(ToolWindowViewModel);
        public Type ViewType => typeof(ToolWindowControl);

        public bool HasWindow => true;

        private ToolWindowViewModel? _viewModel;

        public ClipboardToTimelineTool()
        {
            _viewModel = new ToolWindowViewModel();
            CaptureManager.CurrentViewModel = _viewModel;
            HotkeyManager.Start();
        }

        public object CreateContent()
        {
            return new ToolWindowControl { DataContext = _viewModel };
        }

        public void Dispose()
        {
            HotkeyManager.Stop();
        }
    }
}
