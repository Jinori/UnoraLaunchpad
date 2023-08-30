using System;
using System.IO;
using System.Runtime.InteropServices;

namespace UnoraLaunchpad;

public sealed class SuspendedProcess : IDisposable
{
    private readonly Win32ProcessSafeHandle ProcessHandle;
    private readonly bool ResumeOnDispose;
    private readonly Win32ThreadSafeHandle ThreadHandle;
    private bool IsDisposed;

    private SuspendedProcess(Win32ProcessInformation processInformation, bool resumeOnDispose)
    {
        IsSuspended = true; // Suspended by default

        ProcessHandle = new Win32ProcessSafeHandle(processInformation.ProcessHandle);
        ThreadHandle = new Win32ThreadSafeHandle(processInformation.ThreadHandle);

        ProcessId = processInformation.ProcessId;
        ThreadId = processInformation.ThreadId;

        ResumeOnDispose = resumeOnDispose;
    }

    ~SuspendedProcess() => Dispose(false);

    public void Resume()
    {
        CheckIfDisposed();
        ResumeProcess();
    }

    private void ResumeProcess()
    {
        while (NativeMethods.ResumeThread(ThreadHandle) > 1)
        {
            // ResumeThread returns the PREVIOUS suspension count:
            // -> If it is ZERO, the thread was not suspended.
            // -> If it is ONE, the thread was suspended and now has been resumed.
            // -> If it is GREATER THAN ONE, the thread is still suspended.
        }

        IsSuspended = false;
    }

    public static SuspendedProcess Start(string applicationPath, string commandLine = null, bool resumeOnDispose = true)
    {
        // Create the startup info and set the Size parameter to the size of the structure
        var startupInfo = new Win32StartupInfo
        {
            Size = Marshal.SizeOf(typeof(Win32StartupInfo))
        };

        // Attempt to create the process in a suspended state
        var didCreate = NativeMethods.CreateProcess(
            applicationPath,
            commandLine,
            IntPtr.Zero, // NULL
            IntPtr.Zero, // NULL
            false,
            Win32ProcessCreationFlags.Suspended,
            IntPtr.Zero, // NULL
            null,
            ref startupInfo,
            out var processInformation);

        // Check if the process was created
        if (!didCreate)
            throw new IOException("Unable to create process");

        return new SuspendedProcess(processInformation, resumeOnDispose);
    }

    #region Properties
    public int ProcessId { get; private set; }

    public int ThreadId { get; private set; }

    public bool IsSuspended { get; private set; }
    #endregion

    #region IDisposable Methods
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool isDisposing)
    {
        if (IsDisposed)
            return;

        if (isDisposing)
        {
            // Dispose of managed resources here
        }

        // Dispose of unmanaged resources here
        if (ResumeOnDispose)
            ResumeProcess();

        ThreadHandle.Dispose();
        ProcessHandle.Dispose();

        IsDisposed = true;
    }

    private void CheckIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(GetType().Name);
    }
    #endregion
}