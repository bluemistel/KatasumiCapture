using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ClipboardToTimelinePlugin
{
    public static class CaptureManager
    {
        private static System.Drawing.Rectangle _lastCaptureRect = System.Drawing.Rectangle.Empty;

        // Passed in from the Tool window or Hotkey manager
        public static ToolWindowViewModel? CurrentViewModel { get; set; }

        public static void ExecuteCapture()
        {
            if (System.Windows.Application.Current?.Dispatcher == null) return;
            
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var overlay = new ScreenCaptureOverlay();
                overlay.OnCaptureComplete = (capturedImage, captureRect) =>
                {
                    if (capturedImage != null && captureRect != System.Drawing.Rectangle.Empty)
                    {
                        _lastCaptureRect = captureRect;
                        ProcessCapturedImage(capturedImage);
                    }
                };
                overlay.Show();
            });
        }

        public static void ExecuteRepeatCapture()
        {
            if (_lastCaptureRect == System.Drawing.Rectangle.Empty) return;

            if (System.Windows.Application.Current?.Dispatcher == null) return;
            
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var capturedImage = ScreenCaptureOverlay.CaptureRegion(_lastCaptureRect);
                if (capturedImage != null)
                {
                    ProcessCapturedImage(capturedImage);
                }
            });
        }

        private static void ProcessCapturedImage(BitmapSource bitmap)
        {
            var vm = CurrentViewModel;
            if (vm == null) return;

            if (vm.IsClipboardCopyEnabled)
            {
                System.Windows.Clipboard.SetImage(bitmap);
            }
            else if (vm.IsDirectTimelinePasteEnabled)
            {
                ExecuteDirectPaste(bitmap, vm.SaveImageToDirectory, vm.SaveDirectoryPath);
            }
        }

        private static void ExecuteDirectPaste(BitmapSource bitmap, bool saveImageToDirectory, string? saveDirectoryPath)
        {
            try
            {
                if (saveImageToDirectory && string.IsNullOrWhiteSpace(saveDirectoryPath))
                {
                    // ユーザー指定: 保存ONかつ空欄ならクリップボードコピーのみ
                    System.Windows.Clipboard.SetImage(bitmap);
                    return;
                }

                if (saveImageToDirectory && !string.IsNullOrWhiteSpace(saveDirectoryPath))
                    SaveBitmapToDirectory(bitmap, saveDirectoryPath);

                System.Windows.Clipboard.SetImage(bitmap);
                if (TrySendCtrlV())
                    return;

                // Ctrl+V送信が失敗した場合のみ、既存の反射APIで直接追加を試す
                var tempPath = SaveBitmapToTempFile(bitmap);
                TryAddImageToTimelineWithReflection(tempPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in direct timeline paste: {ex.Message}");
            }
        }

        private static bool TryAddImageToTimelineWithReflection(string imagePath)
        {
            var currentProject = GetCurrentProjectObject();
            if (currentProject == null)
                return false;

            var timelineProperty = currentProject.GetType().GetProperty("Timeline", BindingFlags.Public | BindingFlags.Instance);
            var timeline = timelineProperty?.GetValue(currentProject);
            if (timeline == null)
                return false;

            int currentFrame = ReadIntProperty(timeline, "CurrentFrame", 0);
            int currentLayer = Math.Max(1, ReadIntProperty(timeline, "CurrentLayer", 1));

            var imageItemType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeGetTypes)
                .FirstOrDefault(t => t.Name == "ImageItem");
            if (imageItemType == null)
                return false;

            var imageItem = Activator.CreateInstance(imageItemType);
            if (imageItem == null)
                return false;

            SetIfExists(imageItem, "FilePath", imagePath);
            SetIfExists(imageItem, "Path", imagePath);
            SetIfExists(imageItem, "Frame", currentFrame);
            SetIfExists(imageItem, "Length", 300);
            SetIfExists(imageItem, "Layer", currentLayer);

            var itemsProperty = timeline.GetType().GetProperty("Items", BindingFlags.Public | BindingFlags.Instance);
            var items = itemsProperty?.GetValue(timeline);
            if (items == null)
                return false;

            var addMethod = items.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance, null, new[] { imageItemType }, null)
                ?? items.GetType().GetMethod("Add", BindingFlags.Public | BindingFlags.Instance);
            if (addMethod == null)
                return false;

            addMethod.Invoke(items, new[] { imageItem });
            return true;
        }

        private static object? GetCurrentProjectObject()
        {
            var projectType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeGetTypes)
                .FirstOrDefault(t => t.FullName == "YukkuriMovieMaker.Project.Project");
            if (projectType == null)
                return null;

            var currentProperty = projectType.GetProperty("Current", BindingFlags.Public | BindingFlags.Static);
            return currentProperty?.GetValue(null);
        }

        private static Type[] SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null).Cast<Type>().ToArray();
            }
            catch
            {
                return Array.Empty<Type>();
            }
        }

        private static int ReadIntProperty(object target, string propertyName, int defaultValue)
        {
            var prop = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            var value = prop?.GetValue(target);
            if (value == null)
                return defaultValue;

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return defaultValue;
            }
        }

        private static void SetIfExists(object target, string propertyName, object value)
        {
            var prop = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || !prop.CanWrite)
                return;

            try
            {
                object? converted = value;
                if (value != null && prop.PropertyType != value.GetType())
                    converted = Convert.ChangeType(value, prop.PropertyType);
                prop.SetValue(target, converted);
            }
            catch
            {
                // ignore property mismatch to stay API-tolerant
            }
        }

        private static string SaveBitmapToTempFile(BitmapSource bitmap)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"ymm_capture_{Guid.NewGuid():N}.png");
            SaveBitmapToFile(bitmap, tempPath);
            return tempPath;
        }

        private static void SaveBitmapToDirectory(BitmapSource bitmap, string directoryPath)
        {
            Directory.CreateDirectory(directoryPath);
            string savePath = Path.Combine(directoryPath, $"capture_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
            SaveBitmapToFile(bitmap, savePath);
        }

        private static void SaveBitmapToFile(BitmapSource bitmap, string savePath)
        {
            using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(fileStream);
        }

        private static bool TrySendCtrlV()
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero)
                return false;

            GetWindowThreadProcessId(foreground, out uint processId);
            if (processId != (uint)Environment.ProcessId)
                return false;

            // Shift+P トリガー直後は Shift が押下中のため、Ctrl+Shift+V 化を防ぐ。
            ReleaseShiftKeysIfPressed();
            Thread.Sleep(30);
            keybd_event(VK_CONTROL, 0, 0, 0);
            keybd_event(VK_V, 0, 0, 0);
            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, 0);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
            return true;
        }

        private static void ReleaseShiftKeysIfPressed()
        {
            if (IsKeyDown(VK_LSHIFT))
                keybd_event(VK_LSHIFT, 0, KEYEVENTF_KEYUP, 0);
            if (IsKeyDown(VK_RSHIFT))
                keybd_event(VK_RSHIFT, 0, KEYEVENTF_KEYUP, 0);
            if (IsKeyDown(VK_SHIFT))
                keybd_event(VK_SHIFT, 0, KEYEVENTF_KEYUP, 0);
        }

        private static bool IsKeyDown(int vk)
        {
            return (GetAsyncKeyState(vk) & 0x8000) != 0;
        }

        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;
        private const byte VK_SHIFT = 0x10;
        private const byte VK_LSHIFT = 0xA0;
        private const byte VK_RSHIFT = 0xA1;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
    }
}
