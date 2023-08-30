using System;
using Microsoft.Win32.SafeHandles;

namespace UnoraLaunchpad;

internal sealed class Win32ProcessSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public Win32ProcessSafeHandle(IntPtr handle)
        : this() =>
        SetHandle(handle);

    public Win32ProcessSafeHandle()
        : base(true) { }

    #region SafeHandle Methods
    protected override bool ReleaseHandle() => NativeMethods.CloseHandle(handle);
    #endregion
}