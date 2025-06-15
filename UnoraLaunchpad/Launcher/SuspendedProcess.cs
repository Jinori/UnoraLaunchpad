using System;
using System.IO;
using System.Runtime.InteropServices;
using UnoraLaunchpad.Launcher; // For Win32* structures and NativeMethods

namespace UnoraLaunchpad; // Assuming this is the correct namespace

/// <summary>
/// Represents a process that is started in a suspended state, allowing for modification (e.g., memory patching)
/// before its main thread is resumed. Implements IDisposable to ensure handles are closed and the process is optionally resumed.
/// </summary>
public sealed class SuspendedProcess : IDisposable
{
    private readonly Win32ProcessSafeHandle ProcessHandle;
    private readonly bool ResumeOnDispose;
    private readonly Win32ThreadSafeHandle ThreadHandle;
    private bool _isDisposed; // Corrected field name

    /// <summary>
    /// Initializes a new instance of the <see cref="SuspendedProcess"/> class.
    /// </summary>
    /// <param name="processInformation">Win32 process information structure.</param>
    /// <param name="resumeOnDispose">If true, the process will be resumed when this instance is disposed.</param>
    private SuspendedProcess(Win32ProcessInformation processInformation, bool resumeOnDispose)
    {
        IsSuspended = true;

        ProcessHandle = new Win32ProcessSafeHandle(processInformation.ProcessHandle);
        ThreadHandle = new Win32ThreadSafeHandle(processInformation.ThreadHandle);

        ProcessId = processInformation.ProcessId;
        ThreadId = processInformation.ThreadId;

        ResumeOnDispose = resumeOnDispose;
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="SuspendedProcess"/> class.
    /// Ensures resources are released if Dispose was not called.
    /// </summary>
    ~SuspendedProcess() => Dispose(false);

    /// <summary>
    /// Resumes the execution of the suspended process's main thread.
    /// Sets <see cref="IsSuspended"/> to false.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the object has been disposed.</exception>
    public void Resume()
    {
        CheckIfDisposed();
        ResumeProcess();
    }

    /// <summary>
    /// Internal method to resume the process thread.
    /// Repeatedly calls ResumeThread until the thread's suspension count is zero or less.
    /// </summary>
    private void ResumeProcess()
    {
        // ResumeThread returns the PREVIOUS suspension count.
        // If > 1, it's still suspended. If 1, it was suspended and is now resumed. If 0, it wasn't suspended.
        while (NativeMethods.ResumeThread(ThreadHandle) > 1)
        {
            // Loop until fully resumed or an error occurs (ResumeThread returns (DWORD)-1 on error)
        }
        IsSuspended = false;
    }

    /// <summary>
    /// Starts a new process in a suspended state.
    /// </summary>
    /// <param name="applicationPath">The path to the application executable.</param>
    /// <param name="commandLine">Optional command line arguments for the process.</param>
    /// <param name="resumeOnDispose">If true, the process will be automatically resumed when the <see cref="SuspendedProcess"/> object is disposed.</param>
    /// <returns>A <see cref="SuspendedProcess"/> instance representing the newly created, suspended process.</returns>
    /// <exception cref="IOException">Thrown if the process creation fails.</exception>
    public static SuspendedProcess Start(string applicationPath, string commandLine = null, bool resumeOnDispose = true)
    {
        var startupInfo = new Win32StartupInfo
        {
            Size = Marshal.SizeOf(typeof(Win32StartupInfo))
        };

        var didCreate = NativeMethods.CreateProcess(
            applicationPath,
            commandLine,
            IntPtr.Zero,
            IntPtr.Zero,
            false,
            Win32ProcessCreationFlags.Suspended,
            IntPtr.Zero,
            null, // current directory
            ref startupInfo,
            out var processInformation);

        if (!didCreate)
            throw new IOException($"Unable to create process '{applicationPath}'. Error code: {Marshal.GetLastWin32Error()}");

        return new SuspendedProcess(processInformation, resumeOnDispose);
    }

    #region Properties
    /// <summary>
    /// Gets the process ID of the suspended process.
    /// </summary>
    public int ProcessId { get; private set; }

    /// <summary>
    /// Gets the main thread ID of the suspended process.
    /// </summary>
    public int ThreadId { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the process is currently suspended.
    /// </summary>
    public bool IsSuspended { get; private set; }
    #endregion

    #region IDisposable Methods
    /// <summary>
    /// Disposes the <see cref="SuspendedProcess"/> instance, releasing handles and optionally resuming the process.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Handles the actual disposal of resources.
    /// </summary>
    /// <param name="isDisposing">True if called from <see cref="Dispose()"/>, false if called from the finalizer.</param>
    private void Dispose(bool isDisposing)
    {
        if (_isDisposed) // Use corrected field name
            return;

        // No specific managed resources to dispose in 'isDisposing' block beyond what handles do.
        // Unmanaged resources (handles) are disposed here.
        // Process is resumed here if ResumeOnDispose is true, regardless of 'isDisposing' (though typically true from Dispose()).
        if (IsSuspended && ResumeOnDispose)
        {
            ResumeProcess();
        }

        ThreadHandle?.Dispose(); // Safe dispose (checks for null and valid handle)
        ProcessHandle?.Dispose();

        _isDisposed = true; // Use corrected field name
    }

    /// <summary>
    /// Checks if the object has been disposed.
    /// </summary>
    /// <exception cref="ObjectDisposedException">If the object is disposed.</exception>
    private void CheckIfDisposed()
    {
        if (_isDisposed) // Use corrected field name
            throw new ObjectDisposedException(GetType().Name);
    }
    #endregion
}