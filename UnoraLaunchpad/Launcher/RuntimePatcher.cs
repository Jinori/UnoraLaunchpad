using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace UnoraLaunchpad;

public sealed class RuntimePatcher : IDisposable
{
    private readonly ClientVersion ClientVersion;
    private readonly Stream Stream;
    private readonly BinaryWriter Writer;
    private bool IsDisposed;

    public RuntimePatcher(ClientVersion clientVersion, Stream stream, bool leaveOpen = false)
    {
        ClientVersion = clientVersion;
        Stream = stream;
        Writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen);
    }

    #region Patch Methods
    public void ApplyServerHostnamePatch(IPAddress ipAddress) => ApplyServerHostnamePatch(ipAddress.GetAddressBytes());

    public void ApplyServerHostnamePatch(IEnumerable<byte> ipAddressBytes)
    {
        CheckIfDisposed();

        Stream.Position = ClientVersion.ServerHostnamePatchAddress;

        // Write IP bytes in reverse
        foreach (var ipByte in ipAddressBytes.Reverse())
        {
            Writer.Write((byte)0x6A); // PUSH
            Writer.Write(ipByte);
        }

        Stream.Position = ClientVersion.SkipHostnamePatchAddress;

        for (var i = 0; i < 13; i++)
            Writer.Write((byte)0x90); // NOP
    }

    public void ApplyServerPortPatch(int port)
    {
        if (port <= 0)
            throw new ArgumentOutOfRangeException(nameof(port));

        CheckIfDisposed();

        Stream.Position = ClientVersion.ServerPortPatchAddress;

        var portHiByte = (port >> 8) & 0xFF;
        var portLoByte = port & 0xFF;

        // Write lo and hi order bytes
        Writer.Write((byte)portLoByte);
        Writer.Write((byte)portHiByte);
    }

    public void ApplySkipIntroVideoPatch()
    {
        CheckIfDisposed();

        Stream.Position = ClientVersion.IntroVideoPatchAddress;

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

        Stream.Position = ClientVersion.MultipleInstancePatchAddress;

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

        Stream.Position = ClientVersion.HideWallsPatchAddress;

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