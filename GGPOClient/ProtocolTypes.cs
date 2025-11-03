using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GGPOClient;

using System;
using System.Runtime.InteropServices;

//#pragma warning disable 649


// ================================================================================================================
// NOTE: These should match what is compiled in the C++ code....
public static class ProtoConsts
{
  public const int MAX_NAME_SIZE = 16;                 // TODO
  public const int UDP_MSG_MAX_PLAYERS = 4;            // TODO
  public const int MAX_COMPRESSED_BITS = 4096;         // TODO
  public const int MAX_GGPOCHAT_SIZE = 128;            // TODO
}


// ================================================================================================================
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MessageHeader
{
  public UInt16 magic;        // magic_number
  public UInt16 sequence_number;
  public EMsgType type;                  /* packet type.  Corresponds to: 'EMsgType' enum */
}

// ================================================================================================================
public enum EMsgType : byte
{
  Invalid = 0,
  SyncRequest = 1,
  SyncReply = 2,
  Input = 3,
  QualityReport = 4,
  QualityReply = 5,
  KeepAlive = 6,
  InputAck = 7,
  ChatCommand = 8
};
//// ================================================================================================================
//// MINE:
//[StructLayout(LayoutKind.Explicit)]
//struct ConnectStatus
//{
//  [FieldOffset(0)] private int value;

//  public bool IsDisconnected
//  {
//    get => (value & 0x1) != 0;
//    set
//    {
//      if (value)
//        this.value |= 0x1;
//      else
//        this.value &= ~0x1;
//    }
//  }

//  public int LastFrame
//  {
//    get => value >> 1;                 // use remaining 31 bits
//    set => this.value = (this.value & 0x1) | (value << 1);
//  }
//}


// If you already have a ConnectStatus definition, replace this with yours.
// Keep it blittable and packed to match the C++ layout.
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ConnectStatus
{
  // TODO: Replace with real fields. These are placeholders.
  public int Connected;     // e.g., 0/1
  public int LastFrame;     // e.g., last confirmed frame
}

//// ================================================================================================================
//// MINE:
//[StructLayout(LayoutKind.Explicit)]
//public struct SyncRequest
//{
//  [FieldOffset(0)] public UInt32 RandomRequest;
//  [FieldOffset(4)] public UInt16 RemoteMagicNumber;
//  [FieldOffset(6)] public byte RemoteEndpoint;

//  // --------------------------------------------------------------------------------------------------------------
//  internal static void FromBytes(byte[] data, int startOffset, ref SyncRequest res)
//  {
//    res.RandomRequest = BitConverter.ToUInt32(data, startOffset);
//    res.RemoteMagicNumber = BitConverter.ToUInt16(data, startOffset + sizeof(UInt32));
//    res.RemoteEndpoint = data[startOffset + sizeof(UInt32) + sizeof(UInt16)];
//  }
//}

// ================================================================================================================
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SyncRequest
{
  public uint random_request;   // please reply back with this random data
  public ushort remote_magic;
  public byte remote_endpoint;

  // ---------------------------------------------------------------------------------
  internal static void FromBytes(byte[] data, int startOffset, ref SyncRequest res)
  {
    res.random_request = BitConverter.ToUInt32(data, startOffset);
    res.remote_magic = BitConverter.ToUInt16(data, startOffset + sizeof(UInt32));
    res.remote_endpoint = data[startOffset + sizeof(UInt32) + sizeof(UInt16)];
  }
}

// ================================================================================================================
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
public unsafe struct SyncReply
{
  public uint random_reply;     // OK, here's your random data back

  public fixed sbyte playerName[ProtoConsts.MAX_NAME_SIZE];

  public string GetPlayerName()
  {
    fixed (sbyte* p = playerName)
    {
      return AnsiHelpers.PtrToAnsiString(p, ProtoConsts.MAX_NAME_SIZE);
    }
  }
  public void SetPlayerName(string value)
  {
    fixed (sbyte* p = playerName)
    {
      AnsiHelpers.WriteAnsiString(value, p, ProtoConsts.MAX_NAME_SIZE);
    }
  }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct QualityReport
{
  public sbyte frame_advantage; // what's the other guy's frame advantage?
  public uint ping;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct QualityReply
{
  public uint pong;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct InputMsg
{
  // Raw storage for peer_connect_status[UDP_MSG_MAX_PLAYERS]
  // Size = sizeof(ConnectStatus) * UDP_MSG_MAX_PLAYERS.
  public const int PeerConnectStatusBytes =
      sizeof(int) * 2 * ProtoConsts.UDP_MSG_MAX_PLAYERS; // two ints per ConnectStatus

  public fixed byte peer_connect_status_bytes[PeerConnectStatusBytes];

  public uint start_frame;

  // bitfields packed into a single int
  private int _flags;
  public bool disconnect_requested
  {
    get { return (_flags & 0x1) != 0; }
    set { _flags = value ? (_flags | 0x1) : (_flags & ~0x1); }
  }
  public int ack_frame
  {
    get { return (_flags >> 1) & 0x7FFFFFFF; }
    set { _flags = (_flags & 0x1) | ((value & 0x7FFFFFFF) << 1); }
  }

  public ushort num_bits;
  public byte input_size;

  public fixed byte bits[ProtoConsts.MAX_COMPRESSED_BITS];

  // Helpers to get/set a ConnectStatus by index (unsafe)
  public ConnectStatus GetPeerConnectStatus(int index)
  {
    if ((uint)index >= ProtoConsts.UDP_MSG_MAX_PLAYERS) throw new ArgumentOutOfRangeException(nameof(index));
    fixed (byte* basePtr = peer_connect_status_bytes)
    {
      int stride = sizeof(int) * 2;
      byte* p = basePtr + (index * stride);
      return *(ConnectStatus*)p;
    }
  }

  public void SetPeerConnectStatus(int index, in ConnectStatus value)
  {
    if ((uint)index >= ProtoConsts.UDP_MSG_MAX_PLAYERS) throw new ArgumentOutOfRangeException(nameof(index));
    fixed (byte* basePtr = peer_connect_status_bytes)
    {
      int stride = sizeof(int) * 2;
      *(ConnectStatus*)(basePtr + (index * stride)) = value;
    }
  }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct InputAck
{
  // In C++: int ack_frame : 31;
  private int _ackFlags;

  public int ack_frame
  {
    get { return _ackFlags & 0x7FFFFFFF; }
    set
    {
      _ackFlags = (_ackFlags & unchecked((int)0x80000000)) | (value & 0x7FFFFFFF);
    }
  }
}


[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct Chat
{
  // char text[MAX_GGPOCHAT_SIZE + 1];
  public fixed sbyte text[ProtoConsts.MAX_GGPOCHAT_SIZE + 1];

  public string GetText()
  {
    fixed (sbyte* p = text) { return AnsiHelpers.PtrToAnsiString(p, ProtoConsts.MAX_GGPOCHAT_SIZE + 1); }
  }

  public void SetText(string value)
  {
    fixed (sbyte* p = text) { AnsiHelpers.WriteAnsiString(value, p, ProtoConsts.MAX_GGPOCHAT_SIZE + 1); }
  }
}

//[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
//public struct Chat
//{
//  // C++: char text[MAX_GGPOCHAT_SIZE + 1];
//  // For ByValTStr, SizeConst is in characters; +1 for terminating NUL.
//  [MarshalAs(UnmanagedType.ByValTStr, SizeConst = ProtoConsts.MAX_GGPOCHAT_SIZE + 1)]
//  public string text;
//}

// ===== The union =====
[StructLayout(LayoutKind.Explicit, Pack = 1)]
public struct U
{
  [FieldOffset(0)] public SyncRequest sync_request;
  [FieldOffset(0)] public SyncReply sync_reply;
  [FieldOffset(0)] public QualityReport quality_report;
  [FieldOffset(0)] public QualityReply quality_reply;
  [FieldOffset(0)] public InputMsg input;
  [FieldOffset(0)] public InputAck input_ack;
  [FieldOffset(0)] public Chat chat;
}

// If you have a containing message type with the union named "u" (like in your C++):
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Message
{
  public U u;
}


// ================================================================================================================
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct UdpMsg
{
  const int SIZE_OF_MESSAGE_HEADER = 5;

  // public ConnectStatus connect_status;
  public MessageHeader header;
  public U u;   // The actual data we care about....


  // --------------------------------------------------------------------------------------------------------------
  public static unsafe void FromBytes(byte[] data, ref UdpMsg res)
  {
    // NEW:  Unsafe style!
    // This is cool b/c we don't have to get too worried about the struct definitions / write interop code...
    if (data == null) throw new ArgumentNullException(nameof(data));
    if (data.Length > sizeof(UdpMsg)) throw new ArgumentException("Too much data for UdpMsg", nameof(data));

    fixed (byte* pSrc = data)
    {
      res = *(UdpMsg*)pSrc;
    }

    // OLD:
    //res.header.magic = BitConverter.ToUInt16(data, 0);
    //res.header.sequence_number = BitConverter.ToUInt16(data, sizeof(UInt16));
    //res.header.type = (EMsgType)data[sizeof(UInt16) * 2];

    //switch (res.header.type)
    //{
    //  case EMsgType.SyncRequest:
    //    SyncRequest.FromBytes(data, SIZE_OF_MESSAGE_HEADER, ref res.u.sync_request);
    //    break;

    //  default:
    //    throw new InvalidOperationException($"The header type: {res.header.type} is not supported!");
    //}
  }

  
  public static unsafe void ToBytes(in U u, byte[] dst)
  {
    // NOTE: whatever calls this code needs a way to make sure that we aren't
    // always going to send the full size of the message.  Many times, the payload can be
    // smaller than sizeof(UdpMsg) especially when there isn't chat data, etc.
    throw new Exception();

    if (dst is null) throw new ArgumentNullException(nameof(dst));
    if (dst.Length < sizeof(U)) throw new ArgumentException("Too short for U", nameof(dst));

    fixed (byte* pDst = dst)
    {
      *(U*)pDst = u;
    }
  }

  // https://stackoverflow.com/questions/59788851/preferred-way-to-populate-a-byte-buffer-using-binaryprimitives

  // --------------------------------------------------------------------------------------------------------------
  /// <summary>
  /// Squirt the contents of this message into the given byte array and also output the number of bytes that
  /// are actually used.
  /// </summary>
  internal void ToBytes(byte[] toSend, out int length)
  {
    length = 5;
    unsafe
    {
      fixed (byte* dest = &toSend[0])
      {
        *(UInt16*)dest = header.magic;
        *(UInt16*)(dest + sizeof(UInt16)) = header.sequence_number;
        // *(UInt16*)(dest + sizeof(UInt16)) = Header.SequenceNumber;
        *(dest + sizeof(UInt16) + sizeof(UInt16)) = (byte)EMsgType.SyncReply;
      }
    }

    //BinaryPrimitives.WriteUInt16BigEndian(toSend, Header.MagicNumber);
    //BinaryPrimitives.WriteUInt16BigEndian(toSend, Header.SequenceNumber);
    //BinaryPrimitives.Write (toSend, Header.SequenceNumber);

    //   throw new NotImplementedException();
    int x = 10;
  }
}



// ===== Small ANSI helpers =====
public static unsafe class AnsiHelpers
{
  public static string PtrToAnsiString(sbyte* p, int maxLen)
  {
    int len = 0;
    while (len < maxLen && p[len] != 0) { len++; }
    return Encoding.ASCII.GetString((byte*)p, len);
  }

  public static void WriteAnsiString(string value, sbyte* dest, int capacity)
  {
    var bytes = Encoding.ASCII.GetBytes(value ?? string.Empty);
    int n = Math.Min(bytes.Length, Math.Max(0, capacity - 1)); // leave room for NUL
    for (int i = 0; i < n; i++) dest[i] = (sbyte)bytes[i];
    dest[n] = 0;
    for (int i = n + 1; i < capacity; i++) dest[i] = 0;
  }
}