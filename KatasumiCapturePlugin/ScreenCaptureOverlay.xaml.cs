using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace ClipboardToTimelinePlugin
{
    public partial class ScreenCaptureOverlay : Window
    {
        private System.Windows.Point _startPoint;
        private bool _isDragging = false;
        
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
            if (_isDragging)
            {
                _isDragging = false;
                OverlayCanvas.ReleaseMouseCapture();
                
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

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
