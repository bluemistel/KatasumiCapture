using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ClipboardToTimelinePlugin
{
    public partial class ScreenCaptureOverlay : Window
    {
        private const int HotKeyId = 0x4B53; // かたすみ Esc（他ウィンドウと衝突しにくい任意ID）
        private const int WmHotKey = 0x0312;

        private System.Windows.Point _startPoint;
        private bool _isDragging = false;
        private bool _captureFinished;
        private bool _escapeHotKeyRegistered;
        private HwndSource? _hwndSource;
        
        // This will allow us to return the captured bitmap and its physical coordinates
        public Action<BitmapSource?, System.Drawing.Rectangle>? OnCaptureComplete { get; set; }

        public ScreenCaptureOverlay()
        {
            InitializeComponent();

            // Cover all screens
            this.Left = SystemParameters.VirtualScreenLeft;
            this.Top = SystemParameters.VirtualScreenTop;
            this.Width = SystemParameters.VirtualScreenWidth;
            this.Height = SystemParameters.VirtualScreenHeight;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var helper = new WindowInteropHelper(this);
            IntPtr hwnd = helper.Handle;
            if (hwnd == IntPtr.Zero)
                return;

            // フォーカスがオーバーレイに無い場合でも Esc で中断できるようシステムホットキー登録
            if (RegisterHotKey(hwnd, HotKeyId, 0, VkEscape))
                _escapeHotKeyRegistered = true;

            _hwndSource = HwndSource.FromHwnd(hwnd);
            _hwndSource?.AddHook(WndProc);
        }

        protected override void OnClosed(EventArgs e)
        {
            UnregisterEscapeHotKey();
            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(WndProc);
                _hwndSource = null;
            }
            base.OnClosed(e);
        }

        private void UnregisterEscapeHotKey()
        {
            if (!_escapeHotKeyRegistered)
                return;
            var helper = new WindowInteropHelper(this);
            if (helper.Handle != IntPtr.Zero)
                UnregisterHotKey(helper.Handle, HotKeyId);
            _escapeHotKeyRegistered = false;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmHotKey && wParam.ToInt32() == HotKeyId)
            {
                handled = true;
                Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(CancelCapture));
            }
            return IntPtr.Zero;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 入力優先度で再フォーカス（範囲選択開始前から PreviewKeyDown も効きやすくする）
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                Activate();
                Focus();
                Keyboard.Focus(this);
            }));
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;
            e.Handled = true;
            CancelCapture();
        }

        /// <summary>Esc またはユーザー中断。キャプチャは行わず閉じる。</summary>
        private void CancelCapture()
        {
            if (_captureFinished)
                return;
            _captureFinished = true;

            UnregisterEscapeHotKey();

            if (_isDragging)
            {
                _isDragging = false;
                try
                {
                    OverlayCanvas.ReleaseMouseCapture();
                }
                catch
                {
                    // ignore
                }
            }

            SelectionRectangle.Visibility = Visibility.Hidden;
            OnCaptureComplete?.Invoke(null, System.Drawing.Rectangle.Empty);
            Close();
        }

        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                _isDragging = true;
                _startPoint = e.GetPosition(OverlayCanvas);
                SelectionRectangle.Visibility = Visibility.Visible;
                Canvas.SetLeft(SelectionRectangle, _startPoint.X);
                Canvas.SetTop(SelectionRectangle, _startPoint.Y);
                SelectionRectangle.Width = 0;
                SelectionRectangle.Height = 0;
                OverlayCanvas.CaptureMouse();
            }
        }

        private void Canvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isDragging)
            {
                var currentPoint = e.GetPosition(OverlayCanvas);

                var x = Math.Min(currentPoint.X, _startPoint.X);
                var y = Math.Min(currentPoint.Y, _startPoint.Y);
                var width = Math.Abs(currentPoint.X - _startPoint.X);
                var height = Math.Abs(currentPoint.Y - _startPoint.Y);

                Canvas.SetLeft(SelectionRectangle, x);
                Canvas.SetTop(SelectionRectangle, y);
                SelectionRectangle.Width = width;
                SelectionRectangle.Height = height;
            }
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_captureFinished)
                return;

            if (_isDragging)
            {
                _isDragging = false;
                OverlayCanvas.ReleaseMouseCapture();

                _captureFinished = true;

                // Hide window before capture so the red rectangle and darkened screen don't show up in the shot
                this.Hide();

                var x = Canvas.GetLeft(SelectionRectangle);
                var y = Canvas.GetTop(SelectionRectangle);
                var width = SelectionRectangle.Width;
                var height = SelectionRectangle.Height;

                if (width > 0 && height > 0)
                {
                    // Map WPF logical pixels to physical pixels
                    var source = PresentationSource.FromVisual(this);
                    double dpiX = 96.0;
                    double dpiY = 96.0;
                    if (source != null && source.CompositionTarget != null)
                    {
                        dpiX = 96.0 * source.CompositionTarget.TransformToDevice.M11;
                        dpiY = 96.0 * source.CompositionTarget.TransformToDevice.M22;
                    }

                    int physX = (int)((this.Left + x) * dpiX / 96.0);
                    int physY = (int)((this.Top + y) * dpiY / 96.0);
                    int physWidth = (int)(width * dpiX / 96.0);
                    int physHeight = (int)(height * dpiY / 96.0);

                    var captureRect = new System.Drawing.Rectangle(physX, physY, physWidth, physHeight);
                    
                    BitmapSource? capturedImage = CaptureRegion(captureRect);
                    OnCaptureComplete?.Invoke(capturedImage, captureRect);
                }
                else
                {
                    OnCaptureComplete?.Invoke(null, System.Drawing.Rectangle.Empty);
                }

                this.Close();
            }
        }

        public static BitmapSource? CaptureRegion(System.Drawing.Rectangle region)
        {
            if (region.Width <= 0 || region.Height <= 0) return null;

            using (var bmp = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(region.X, region.Y, 0, 0, region.Size, CopyPixelOperation.SourceCopy);
                }
                
                // Convert to WPF BitmapSource securely avoiding memory leaks
                var hBitmap = bmp.GetHbitmap();
                try
                {
                    var source = Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    // Freeze for cross-thread operations if needed
                    source.Freeze();
                    return source;
                }
                finally
                {
                    DeleteObject(hBitmap);
                }
            }
        }

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        private const uint VkEscape = 27;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
