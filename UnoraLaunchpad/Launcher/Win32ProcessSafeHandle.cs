using System;
using Microsoft.Win32.SafeHandles;

namespace UnoraLaunchpad;

internal sealed class Win32ProcessSafeHandle() : SafeHandleZeroOrMinusOneIsInvalid(true)
{
    public Win32ProcessSafeHandle(IntPtr handle)
        : this() =>
        SetHandle(handle);

    #region SafeHandle Methods
    protected override bool ReleaseHandle() => NativeMethods.CloseHandle(handle);
    #endregion
}