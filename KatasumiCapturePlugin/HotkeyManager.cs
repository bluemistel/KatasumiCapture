using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace ClipboardToTimelinePlugin
{
    public static class HotkeyManager
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        public static void Start()
        {
            _hookID = SetHook(_proc);
        }

        public static void Stop()
        {
            UnhookWindowsHookEx(_hookID);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule? curModule = curProcess.MainModule)
            {
                if (curModule != null)
                {
                    return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                        GetModuleHandle(curModule.ModuleName), 0);
                }
                return IntPtr.Zero;
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Key key = KeyInterop.KeyFromVirtualKey(vkCode);

                var vm = CaptureManager.CurrentViewModel;
                if (vm == null || vm.HotkeysSuppressed)
                    return CallNextHookEx(_hookID, nCode, wParam, lParam);

                Key captureKey = vm.GetCaptureHotkeyKey();
                if (key == captureKey)
                {
                    if (IsCurrentProcessForeground())
                    {
                        bool isShiftDown = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
                        
                        // We found a match, execute but don't swallow the key completely or do we?
                        if (isShiftDown)
                        {
                            CaptureManager.ExecuteRepeatCapture();
                        }
                        else
                        {
                            CaptureManager.ExecuteCapture();
                        }
                        
                        // Swallow the key press so YMM4 doesn't also process it as 'p' typing 
                        // IF we want that behavior. Usually it's better to swallow it to prevent accidental typing.
                        // return (IntPtr)1; 
                        
                        // But let's only swallow if they are NOT typing in a textbox. 
                        // Detecting textbox focus globally is hard, so we just let it pass but still trigger.
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private static bool IsCurrentProcessForeground()
        {
            IntPtr fgWindow = GetForegroundWindow();
            if (fgWindow == IntPtr.Zero) return false;

            GetWindowThreadProcessId(fgWindow, out uint fgProcessId);
            return fgProcessId == Process.GetCurrentProcess().Id;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    }
}
