using System;

namespace UnoraLaunchpad;

[Flags]
public enum ProcessAccess
{
    Read = 0x1,
    Write = 0x2,
    ReadWrite = Read | Write
}