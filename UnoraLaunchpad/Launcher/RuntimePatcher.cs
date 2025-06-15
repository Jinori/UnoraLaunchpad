using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using UnoraLaunchpad.Launcher; // For ClientVersion

namespace UnoraLaunchpad; // Assuming this is the correct namespace

/// <summary>
/// Applies runtime patches to a game client's memory stream using a specific client version's addresses.
/// Operates on a provided <see cref="Stream"/>, typically a <see cref="ProcessMemoryStream"/>.
/// </summary>
/// <param name="clientVersion">The client version definition with patch addresses.</param>
/// <param name="stream">The memory stream of the game client. Must be seekable and writable.</param>
/// <param name="leaveOpen">If true, the stream is left open when the patcher is disposed.</param>
public sealed class RuntimePatcher(ClientVersion clientVersion, Stream stream, bool leaveOpen = false) : IDisposable
{
    private readonly BinaryWriter Writer = new(stream, Encoding.UTF8, leaveOpen);
    private bool _isDisposed; // Corrected field name

    #region Patch Methods

    /// <summary>
    /// Applies server hostname patch using an <see cref="IPAddress"/>.
    /// </summary>
    /// <param name="ipAddress">The new server IP address.</param>
    public void ApplyServerHostnamePatch(IPAddress ipAddress) => ApplyServerHostnamePatch(ipAddress.GetAddressBytes());

    /// <summary>
    /// Applies server hostname patch using raw IP address bytes.
    /// Writes PUSH instructions for each byte of the IP in reverse, then NOPs original instructions.
    /// </summary>
    /// <param name="ipAddressBytes">Bytes of the IP address.</param>
    public void ApplyServerHostnamePatch(IEnumerable<byte> ipAddressBytes)
    {
        CheckIfDisposed();
        stream.Position = clientVersion.ServerHostnamePatchAddress;

        foreach (var ipByte in ipAddressBytes.Reverse())
        {
            Writer.Write((byte)0x6A); // PUSH
            Writer.Write(ipByte);
        }

        // NOP out original instructions using the helper
        byte[] nopSequence = Enumerable.Repeat((byte)0x90, 13).ToArray();
        WriteBytes(clientVersion.SkipHostnamePatchAddress, nopSequence);
    }

    /// <summary>
    /// Applies server port patch.
    /// </summary>
    /// <param name="port">The new server port.</param>
    /// <exception cref="ArgumentOutOfRangeException">If port is invalid.</exception>
    public void ApplyServerPortPatch(int port)
    {
        if (port <= 0 || port > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");
        CheckIfDisposed();
        var portHiByte = (byte)((port >> 8) & 0xFF);
        var portLoByte = (byte)(port & 0xFF);
        WriteBytes(clientVersion.ServerPortPatchAddress, [portLoByte, portHiByte]);
    }

    /// <summary>
    /// Applies patch to skip the game's intro video.
    /// Modifies a CMP instruction.
    /// </summary>
    public void ApplySkipIntroVideoPatch()
    {
        CheckIfDisposed();
        WriteBytes(clientVersion.IntroVideoPatchAddress, [(byte)0x83, (byte)0xFA, (byte)0x00, (byte)0x90, (byte)0x90, (byte)0x90]);
    }

    /// <summary>
    /// Applies patch to allow multiple game instances.
    /// Modifies mutex check related instructions.
    /// </summary>
    public void ApplyMultipleInstancesPatch()
    {
        CheckIfDisposed();
        WriteBytes(clientVersion.MultipleInstancePatchAddress, [(byte)0x31, (byte)0xC0, (byte)0x90, (byte)0x90, (byte)0x90, (byte)0x90]);
    }

    /// <summary>
    /// Applies patch to hide in-game walls/obstructions.
    /// Changes a conditional jump to bypass rendering logic.
    /// </summary>
    public void ApplyHideWallsPatch()
    {
        CheckIfDisposed();
        WriteBytes(clientVersion.HideWallsPatchAddress, [(byte)0xEB, (byte)0x17, (byte)0x90]);
    }

    /// <summary>
    /// Sets the stream position and writes a sequence of bytes.
    /// </summary>
    /// <param name="address">The address in the stream to write to.</param>
    /// <param name="bytes">The byte array to write.</param>
    private void WriteBytes(long address, byte[] bytes)
    {
        stream.Position = address;
        foreach (byte b in bytes)
        {
            Writer.Write(b);
        }
    }
    #endregion

    #region IDisposable Methods
    /// <summary>
    /// Releases resources used by the <see cref="RuntimePatcher"/>.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Handles disposing of managed resources.
    /// </summary>
    /// <param name="isDisposing">True if called from Dispose(), false if from finalizer.</param>
    private void Dispose(bool isDisposing)
    {
        if (_isDisposed) // Use corrected field name
            return;
        if (isDisposing)
            Writer.Dispose();
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