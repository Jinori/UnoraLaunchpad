using System;

namespace UnoraLaunchpad;

[Flags]
public enum WaitEventResult : uint
{
    Signaled = 0,
    Abandoned = 128,
    Timeout = 258,
    Failed = uint.MaxValue
}

[Flags]
public enum ProcessAccessFlags : uint
{
    None = 0,
    Terminate = 1,
    CreateThread = 2,
    VmOperation = 8,
    VmRead = 16,
    VmWrite = 32,
    DuplicateHandle = 64,
    CreateProcess = 128,
    SetQuota = 256,
    SetInformation = 512,
    QueryInformation = 1024,
    SuspendResume = 2048,
    QueryLimitedInformation = 4096,
    FullAccess = 2035711
}

[Flags]
public enum ProcessCreationFlags
{
    DebugProcess = 1,
    DebugOnlyThisProcess = 2,
    Suspended = 4,
    DetachedProcess = 8,
    NewConsole = 16,
    NewProcessGroup = 512,
    UnicodeEnvironment = 1024,
    SeparateWowVdm = 2048,
    SharedWowVdm = 4096,
    InheritParentAffinity = SharedWowVdm,
    ProtectedProcess = 262144,
    ExtendedStartupInfoPresent = 524288,
    BreakawayFromJob = 16777216,
    PreserveCodeAuthZLevel = 33554432,
    DefaultErrorMode = 67108864,
    NoWindow = 134217728
}

[Flags]
public enum ThumbnailFlags
{
    RectDestination = 1,
    RectSource = 2,
    Opacity = 4,
    Visible = 8,
    SourceClientAreaOnly = 16,
    All = RectDestination | RectSource | Opacity | Visible | SourceClientAreaOnly
}

[Flags]
public enum WindowStyleFlags : uint
{
    Border = 0x00800000,
    Caption = 0x00C00000,
    Child = 0x40000000,
    ClipChildren = 0x02000000,
    ClipSiblings = 0x04000000,
    Disabled = 0x08000000,
    DialogFrame = 0x00400000,
    Group = 0x00020000,
    HorizontalScroll = 0x00100000,
    VerticalScroll = 0x00200000,
    Minimized = 0x20000000,
    Maximized = 0x01000000,
    MaximizeBox = 0x00010000,
    MinimizeBox = Group,
    Overlapped = 0x00000000,
    OverlappedWindow = Overlapped | Caption | SystemMenu | Sizeable | MinimizeBox | MaximizeBox | Visible,
    Popup = 0x80000000,
    PopupWindow = Popup | Border | SystemMenu | Visible,
    Sizeable = 0x00040000,
    SystemMenu = 0x00080000,
    TabStop = MaximizeBox,
    Visible = 0x10000000
}

[Flags]
public enum WindowFlags
{
    None = 0,
    WndProc = -4,
    InstanceHandle = -6,
    ID = -12,
    Style = -16,
    ExtendedStyle = -20,
    UserData = -21
}

[Flags]
public enum ShowWindowFlags
{
    Hide = 0,
    ActiveNormal = 1,
    ActiveMinimized = 2,
    ActiveMaximized = 3,
    InactiveNormal = 4,
    ActiveShow = 5,
    MinimizeNext = 6,
    InactiveMinimized = 7,
    InactiveShow = 8,
    ActiveRestore = 9,
    Default = 10,
    ForceMinimized = 11
}

[Flags]
internal enum ClientState
{
    Hidden = 1,
    Normal = 2,
    Fullscreen = 4
}