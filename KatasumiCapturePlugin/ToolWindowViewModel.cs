using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ClipboardToTimelinePlugin
{
    public class ToolWindowViewModel : INotifyPropertyChanged
    {
        public ToolWindowViewModel()
        {
            HotkeyKeyCandidates = BuildHotkeyKeyCandidates();
            CaptureRegionCommand = new RelayCommand(() => CaptureManager.ExecuteCapture());
            CaptureRepeatCommand = new RelayCommand(() => CaptureManager.ExecuteRepeatCapture());
        }

        /// <summary>ホットキー候補（A〜Z, 0〜9）</summary>
        public IReadOnlyList<string> HotkeyKeyCandidates { get; }

        public ICommand CaptureRegionCommand { get; }
        public ICommand CaptureRepeatCommand { get; }

        private bool _isDirectTimelinePasteEnabled = false;
        public bool IsDirectTimelinePasteEnabled
        {
            get => _isDirectTimelinePasteEnabled;
            set
            {
                if (_isDirectTimelinePasteEnabled != value)
                {
                    _isDirectTimelinePasteEnabled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsClipboardCopyEnabled));
                }
            }
        }

        public bool IsClipboardCopyEnabled
        {
            get => !_isDirectTimelinePasteEnabled;
            set
            {
                if (_isDirectTimelinePasteEnabled == value)
                {
                    _isDirectTimelinePasteEnabled = !value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsDirectTimelinePasteEnabled));
                }
            }
        }

        private bool _saveImageToDirectory = false;
        public bool SaveImageToDirectory
        {
            get => _saveImageToDirectory;
            set
            {
                if (_saveImageToDirectory != value)
                {
                    _saveImageToDirectory = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _saveDirectoryPath = string.Empty;
        public string SaveDirectoryPath
        {
            get => _saveDirectoryPath;
            set
            {
                if (_saveDirectoryPath != value)
                {
                    _saveDirectoryPath = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>ショートカット（低レベルフック）を無効にする（既定ON・誤操作防止）</summary>
        private bool _hotkeysSuppressed = true;
        public bool HotkeysSuppressed
        {
            get => _hotkeysSuppressed;
            set
            {
                if (_hotkeysSuppressed != value)
                {
                    _hotkeysSuppressed = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>キャプチャ開始用の基本キー（1文字）。ComboBox 用。</summary>
        private string _hotkeyKeyLetter = "P";
        public string HotkeyKeyLetter
        {
            get => _hotkeyKeyLetter;
            set
            {
                var v = (value ?? string.Empty).Trim();
                if (v.Length > 0)
                    v = v.Substring(0, 1).ToUpperInvariant();
                if (string.IsNullOrEmpty(v))
                    v = "P";
                if (_hotkeyKeyLetter != v)
                {
                    _hotkeyKeyLetter = v;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>HotkeyManager が参照する WPF の Key</summary>
        public System.Windows.Input.Key GetCaptureHotkeyKey()
        {
            if (string.IsNullOrEmpty(HotkeyKeyLetter))
                return System.Windows.Input.Key.P;
            var c = char.ToUpperInvariant(HotkeyKeyLetter[0]);
            if (c >= 'A' && c <= 'Z')
                return System.Windows.Input.Key.A + (c - 'A');
            if (c >= '0' && c <= '9')
                return System.Windows.Input.Key.D0 + (c - '0');
            return System.Windows.Input.Key.P;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static IReadOnlyList<string> BuildHotkeyKeyCandidates()
        {
            var letters = Enumerable.Range(0, 26).Select(i => ((char)('A' + i)).ToString());
            var digits = Enumerable.Range(0, 10).Select(i => i.ToString());
            return letters.Concat(digits).ToList();
        }
    }
}
