using System;
using System.IO;

namespace UnoraLaunchpad;

public sealed class ProcessMemoryStream : Stream
{
    private const long MODULE_BASE_ADDRESS = 0x400000; // Most processes will map to this base address
    private readonly bool LeaveOpen;
    private readonly ProcessAccess ProcessAccess;
    private readonly Win32ProcessSafeHandle ProcessHandle;
    private readonly byte[] ReadBuffer;
    private readonly byte[] WriteBuffer;

    private bool IsDisposed;

    private long PositionAddress = MODULE_BASE_ADDRESS;

    public ProcessMemoryStream(
        int processId,
        ProcessAccess desiredAccess = ProcessAccess.ReadWrite,
        int bufferSize = 4096,
        bool leaveOpen = false
    )
    {
        if (processId < 0)
            throw new ArgumentOutOfRangeException(nameof(processId));

        if (bufferSize < 1)
            throw new ArgumentOutOfRangeException(nameof(processId));

        var win32Flags = Win32ProcessAccess.VmOperation;

        // If read mode was requested, bitwise OR the flag
        if (desiredAccess.HasFlag(ProcessAccess.Read))
            win32Flags |= Win32ProcessAccess.VmRead;

        // If write mode was requested, bitwise OR the flag
        if (desiredAccess.HasFlag(ProcessAccess.Write))
            win32Flags |= Win32ProcessAccess.VmWrite;

        // Open the process and check if the handle is valid
        ProcessAccess = desiredAccess;
        ProcessHandle = NativeMethods.OpenProcess(win32Flags, false, processId);
        LeaveOpen = leaveOpen;

        // Check if handle is valid
        if (ProcessHandle.IsInvalid)
        {
            var errorCode = NativeMethods.GetLastError();

            throw new IOException("Unable to open process", errorCode);
        }

        // Allocate read and write buffers
        ReadBuffer = new byte[bufferSize];
        WriteBuffer = new byte[bufferSize];
    }

    ~ProcessMemoryStream() => Dispose(false);

    #region Stream Properties
    public override bool CanRead
    {
        get
        {
            CheckIfDisposed();

            return !ProcessHandle.IsClosed && ProcessAccess.HasFlag(ProcessAccess.Read);
        }
    }

    public override bool CanSeek
    {
        get
        {
            CheckIfDisposed();

            return !ProcessHandle.IsClosed;
        }
    }

    public override bool CanWrite
    {
        get
        {
            CheckIfDisposed();

            return !ProcessHandle.IsClosed && ProcessAccess.HasFlag(ProcessAccess.Write);
        }
    }

    public override long Length => throw new NotSupportedException("Process memory stream does not have a specific length");

    public override long Position
    {
        get
        {
            CheckIfDisposed();

            return PositionAddress;
        }
        set
        {
            CheckIfDisposed();

            if (value < 0)
                throw new ArgumentOutOfRangeException($"{nameof(Position)} must be a positive value");

            PositionAddress = value;
        }
    }
    #endregion

    #region Stream Methods
    public override void Close()
    {
        CheckIfDisposed();

        ProcessHandle.Close();
        base.Close();
    }

    public override void Flush() => CheckIfDisposed();

    public override int Read(byte[] buffer, int offset, int count)
    {
        CheckIfDisposed();

        var totalBytesRead = 0;

        while (count > 0)
        {
            // Do not exceed the buffer size for each block read
            var blockSize = Math.Min(count, ReadBuffer.Length);

            // Read the block from process memory
            var didRead = NativeMethods.ReadProcessMemory(
                ProcessHandle,
                (IntPtr)Position,
                ReadBuffer,
                blockSize,
                out var numberOfBytesRead);

            // Check if the read was successful
            if (!didRead || (numberOfBytesRead != blockSize))
                throw new IOException("Unable to read block from process");

            // Copy the block from the read buffer
            Buffer.BlockCopy(
                ReadBuffer,
                0,
                buffer,
                offset,
                blockSize);

            // Increment the offset and stream position by the number of bytes read
            offset += numberOfBytesRead;
            Position += numberOfBytesRead;
            totalBytesRead += numberOfBytesRead;

            // Decrement the count by the number of bytes read
            count -= numberOfBytesRead;
        }

        return totalBytesRead;
    }

    public override int ReadByte()
    {
        CheckIfDisposed();

        // Read the byte from process memory
        var didRead = NativeMethods.ReadProcessMemory(
            ProcessHandle,
            (IntPtr)Position,
            ReadBuffer,
            1,
            out var numberOfBytesRead);

        // Check if the read was successful
        if (!didRead || (numberOfBytesRead != 1))
            throw new IOException("Unable to read byte from process");

        // Increment the stream position by the number of bytes read
        Position += numberOfBytesRead;

        return ReadBuffer[0];
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        CheckIfDisposed();

        switch (origin)
        {
            case SeekOrigin.Begin:
                Position = offset;

                break;

            case SeekOrigin.Current:
                Position += offset;

                break;

            case SeekOrigin.End:
                throw new NotSupportedException("Cannot seek from end of process memory stream");
        }

        return Position;
    }

    public override void SetLength(long value)
    {
        CheckIfDisposed();

        throw new NotSupportedException("Cannot set length of process memory stream");
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        CheckIfDisposed();

        while (count > 0)
        {
            // Do not exceed the buffer size for each block written
            var blockSize = Math.Min(count, WriteBuffer.Length);

            // Copy the block to the write buffer
            Buffer.BlockCopy(
                buffer,
                offset,
                WriteBuffer,
                0,
                blockSize);

            // Write block to process memory
            var didWrite = NativeMethods.WriteProcessMemory(
                ProcessHandle,
                (IntPtr)Position,
                WriteBuffer,
                blockSize,
                out var numberOfBytesWritten);

            // Check if the write was successful
            if (!didWrite || (numberOfBytesWritten != blockSize))
                throw new IOException("Unable to write block to process");

            // Increment the offset and stream position by the number of bytes written
            offset += numberOfBytesWritten;
            Position += numberOfBytesWritten;

            // Decrement the count by the number of bytes written
            count -= numberOfBytesWritten;
        }
    }

    public override void WriteByte(byte value)
    {
        CheckIfDisposed();

        // Copy value to write buffer
        WriteBuffer[0] = value;

        // Write byte to process memory
        var didWrite = NativeMethods.WriteProcessMemory(
            ProcessHandle,
            (IntPtr)Position,
            WriteBuffer,
            1,
            out var numberOfBytesWritten);

        // Check if the write was successful
        if (!didWrite || (numberOfBytesWritten != 1))
            throw new IOException("Unable to write byte to process");

        // Increment the stream position by the number of bytes written
        Position += numberOfBytesWritten;
    }
    #endregion

    #region IDisposable Methods
    protected override void Dispose(bool isDisposing)
    {
        if (IsDisposed)
            return;

        if (isDisposing)
        {
            // Dispose of managed resources here
        }

        // Dispose of unmanaged resources here
        if (!LeaveOpen)
            ProcessHandle.Dispose();

        base.Dispose(isDisposing);
        IsDisposed = true;
    }

    private void CheckIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(GetType().Name);
    }
    #endregion
}