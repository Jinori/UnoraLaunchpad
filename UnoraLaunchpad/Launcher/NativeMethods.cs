using System;
using System.Runtime.InteropServices;
using System.Web;
using System.Windows;

namespace UnoraLaunchpad;

internal static class NativeMethods
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    
    [DllImport("kernel32", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool CreateProcess(
        string applicationPath,
        string commandLine,
        IntPtr processSecurityAttributes,
        IntPtr threadSecurityAttributes,
        bool inheritHandles,
        Win32ProcessCreationFlags creationFlags,
        IntPtr environment,
        string currentDirectory,
        ref Win32StartupInfo startupInfo,
        out Win32ProcessInformation processInformation
    );

    [DllImport("kernel32")]
    public static extern int GetLastError();

    [DllImport("kernel32", SetLastError = true)]
    public static extern Win32ProcessSafeHandle OpenProcess(
        Win32ProcessAccess desiredAccess,
        bool inheritHandle,
        int processId
    );

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool ReadProcessMemory(
        Win32ProcessSafeHandle processHandle,
        IntPtr baseAddress,
        byte[] buffer,
        int count,
        out int numberOfBytesRead
    );

    [DllImport("kernel32", SetLastError = true)]
    public static extern int ResumeThread(Win32ThreadSafeHandle threadHandle);

    [DllImport("kernel32", SetLastError = true)]
    public static extern int SuspendThread(Win32ThreadSafeHandle threadHandle);

    [DllImport("kernel32.dll")]
    public static extern WaitEventResult WaitForSingleObject(IntPtr hObject, int timeout);

    [DllImport("kernel32", SetLastError = true)]
    public static extern bool WriteProcessMemory(
        Win32ProcessSafeHandle processHandle,
        IntPtr baseAddress,
        byte[] buffer,
        int count,
        out int numberOfBytesWritten
    );

    #region Thumbnail Manipulation
    [DllImport("dwmapi.dll")]
    public static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);

    [DllImport("dwmapi.dll")]
    public static extern int DwmUnregisterThumbnail(IntPtr thumb);
    #endregion

    #region Thread Manipulation
    [DllImport("kernel32")]
    public static extern IntPtr CreateRemoteThread(
        IntPtr hProcess,
        IntPtr lpThreadAttributes,
        IntPtr dwStackSize,
        UIntPtr lpStartAddress,
        IntPtr lpParameter,
        uint dwCreationFlags,
        out IntPtr lpThreadId
    );

    [DllImport("kernel32.dll")]
    public static extern int ResumeThread(IntPtr hThread);
    #endregion

    #region Process Manipulation
    [DllImport(
        "kernel32.dll",
        ExactSpelling = true,
        BestFitMapping = false,
        ThrowOnUnmappableChar = true)]
    public static extern UIntPtr GetProcAddress(IntPtr hModule, string procName);

    [DllImport("kernel32.dll", BestFitMapping = false, ThrowOnUnmappableChar = true)]
    public static extern bool CreateProcess(
        string applicationName,
        string commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        bool inheritHandles,
        ProcessCreationFlags creationFlags,
        IntPtr environment,
        string currentDirectory,
        ref Win32StartupInfo startupInfo,
        out ProcessInfo processInfo
    );

    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(ProcessAccessFlags access, bool inheritHandle, int processId);

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Auto,
        BestFitMapping = false,
        ThrowOnUnmappableChar = true)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);
    #endregion

    #region Memory Manipulation
    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    public static extern IntPtr VirtualAllocEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        IntPtr dwSize,
        uint flAllocationType,
        uint flProtect
    );

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    public static extern bool VirtualFreeEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        UIntPtr dwSize,
        uint dwFreeType
    );

    [DllImport("kernel32.dll", BestFitMapping = false, ThrowOnUnmappableChar = true)]
    public static extern bool WriteProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        string lpBuffer,
        UIntPtr nSize,
        out IntPtr lpNumberOfBytesWritten
    );

    [DllImport("kernel32.dll")]
    public static extern bool WriteProcessMemory(
        IntPtr hProcess,
        IntPtr baseAddress,
        IntPtr buffer,
        IntPtr count,
        out int bytesWritten
    );

    [DllImport("kernel32.dll")]
    public static extern bool ReadProcessMemory(
        IntPtr hProcess,
        IntPtr baseAddress,
        IntPtr buffer,
        IntPtr count,
        out int bytesRead
    );
    #endregion

    #region GetWindow
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hwnd, ref Rect rectangle);

    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr hWnd, ref Rect rectangle);
    #endregion

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
    
    #region SetWindow
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int SetWindowText(IntPtr hWnd, string text);

    [DllImport("User32.dll")]
    public static extern int SetForegroundWindow(int hWnd); // Note: This takes int, consider IntPtr if consistency is needed.
                                                            // For the new GetForegroundWindow, it returns IntPtr.

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);

    // It's also useful to have GetWindowTextLength to correctly size the buffer for GetWindowText
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetWindowTextLength(IntPtr hWnd);
    #endregion

    #region Global Hotkeys
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // Modifiers for RegisterHotKey
    public const uint MOD_NONE = 0x0000;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    #endregion

    #region SendInput related (though InputSimulatorStandard handles this, good for reference or direct use if needed)
    // INPUT structure
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion U;
        public static int Size
        {
            get { return Marshal.SizeOf(typeof(INPUT)); }
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    // dwFlags for KEYBDINPUT
    public const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const uint KEYEVENTF_UNICODE = 0x0004;
    public const uint KEYEVENTF_SCANCODE = 0x0008;

    // Input Types
    public const uint INPUT_MOUSE = 0;
    public const uint INPUT_KEYBOARD = 1;
    public const uint INPUT_HARDWARE = 2;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern short VkKeyScan(char ch);

    #endregion
}