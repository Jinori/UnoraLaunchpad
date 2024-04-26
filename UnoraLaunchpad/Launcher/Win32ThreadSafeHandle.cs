using System;
using Microsoft.Win32.SafeHandles;

namespace UnoraLaunchpad;

internal sealed class Win32ThreadSafeHandle() : SafeHandleZeroOrMinusOneIsInvalid(true)
{
    public Win32ThreadSafeHandle(IntPtr handle)
        : this() =>
        SetHandle(handle);

    #region SafeHandle Methods
    protected override bool ReleaseHandle() => NativeMethods.CloseHandle(handle);
    #endregion
}