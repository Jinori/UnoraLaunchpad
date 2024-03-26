using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace UnoraLaunchpad;

public sealed class RuntimePatcher(ClientVersion clientVersion, Stream stream, bool leaveOpen = false) : IDisposable
{
    private readonly BinaryWriter Writer = new(stream, Encoding.UTF8, leaveOpen);
    private bool IsDisposed;

    #region Patch Methods
    public void ApplyServerHostnamePatch(IPAddress ipAddress) => ApplyServerHostnamePatch(ipAddress.GetAddressBytes());

    public void ApplyServerHostnamePatch(IEnumerable<byte> ipAddressBytes)
    {
        CheckIfDisposed();

        stream.Position = clientVersion.ServerHostnamePatchAddress;

        // Write IP bytes in reverse
        foreach (var ipByte in ipAddressBytes.Reverse())
        {
            Writer.Write((byte)0x6A); // PUSH
            Writer.Write(ipByte);
        }

        stream.Position = clientVersion.SkipHostnamePatchAddress;

        for (var i = 0; i < 13; i++)
            Writer.Write((byte)0x90); // NOP
    }

    public void ApplyServerPortPatch(int port)
    {
        if (port <= 0)
            throw new ArgumentOutOfRangeException(nameof(port));

        CheckIfDisposed();

        stream.Position = clientVersion.ServerPortPatchAddress;

        var portHiByte = (port >> 8) & 0xFF;
        var portLoByte = port & 0xFF;

        // Write lo and hi order bytes
        Writer.Write((byte)portLoByte);
        Writer.Write((byte)portHiByte);
    }

    public void ApplySkipIntroVideoPatch()
    {
        CheckIfDisposed();

        stream.Position = clientVersion.IntroVideoPatchAddress;

        Writer.Write((byte)0x83); // CMP
        Writer.Write((byte)0xFA); // EDX
        Writer.Write((byte)0x00); // 0
        Writer.Write((byte)0x90); // NOP
        Writer.Write((byte)0x90); // NOP
        Writer.Write((byte)0x90); // NOP
    }

    public void ApplyMultipleInstancesPatch()
    {
        CheckIfDisposed();

        stream.Position = clientVersion.MultipleInstancePatchAddress;

        Writer.Write((byte)0x31); // XOR
        Writer.Write((byte)0xC0); // EAX, EAX
        Writer.Write((byte)0x90); // NOP
        Writer.Write((byte)0x90); // NOP
        Writer.Write((byte)0x90); // NOP
        Writer.Write((byte)0x90); // NOP
    }

    public void ApplyHideWallsPatch()
    {
        CheckIfDisposed();

        stream.Position = clientVersion.HideWallsPatchAddress;

        Writer.Write((byte)0xEB); // JMP SHORT
        Writer.Write((byte)0x17); // +17
        Writer.Write((byte)0x90); // NOP
    }
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
            Writer.Dispose();

        IsDisposed = true;
    }

    private void CheckIfDisposed()
    {
        if (IsDisposed)
            throw new ObjectDisposedException(GetType().Name);
    }
    #endregion
}