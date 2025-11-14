using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;


namespace GGPOSharp;

// ================================================================================================================
// NOTE: These should match what is compiled in the C++ code....
public static class GGPOConsts
{
  public const int MAX_NAME_SIZE = 16;              
  public const int UDP_MSG_MAX_PLAYERS = 4;         
  public const int MAX_COMPRESSED_BITS = 4096;      
  public const int MAX_GGPOCHAT_SIZE = 128;         

  public const int MAX_PREDICTION_FRAMES = 8;
  public const int INPUT_QUEUE_LENGTH = 128;
  public const int DEFAULT_INPUT_SIZE = 4;
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

// ================================================================================================================
// MINE:
[StructLayout(LayoutKind.Explicit)]
struct ConnectStatus
{
  [FieldOffset(0)] private int _value;

  public bool disconnected
  {
    get => (_value & 0x1) != 0;
    set
    {
      if (value)
        this._value |= 0x1;
      else
        this._value &= ~0x1;
    }
  }

  public int last_frame
  {
    get => _value >> 1;                 // use remaining 31 bits
    set => this._value = (this._value & 0x1) | (value << 1);
  }
}


//[StructLayout(LayoutKind.Sequential, Pack = 1)]
//public struct ConnectStatus
//{
//  // TODO: Replace with real fields. These are placeholders.
//  public int Connected;     // e.g., 0/1
//  public int LastFrame;     // e.g., last confirmed frame
//}

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

  public fixed sbyte playerName[GGPOConsts.MAX_NAME_SIZE];

  // --------------------------------------------------------------------------------------------------------------
  public string GetPlayerName()
  {
    fixed (sbyte* p = playerName)
    {
      return AnsiHelpers.PtrToAnsiString(p, GGPOConsts.MAX_NAME_SIZE);
    }
  }

  // --------------------------------------------------------------------------------------------------------------
  public void SetPlayerName(char[] value)
  {
    fixed (sbyte* p = playerName)
    {
      for (int i = 0; i < value.Length; i++)
      {
        playerName[i] = (sbyte)value[i];
      }
    }
  }

  // --------------------------------------------------------------------------------------------------------------
  public void SetPlayerName(string value)
  {
    fixed (sbyte* p = playerName)
    {
      AnsiHelpers.WriteAnsiString(value, p, GGPOConsts.MAX_NAME_SIZE);
    }
  }
}

// ================================================================================================================
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct QualityReport
{
  public sbyte frame_advantage; // what's the other guy's frame advantage?
  public uint ping;
}

// ================================================================================================================
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct QualityReply
{
  public uint pong;
}

// ================================================================================================================
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct InputMsg
{
  //// Raw storage for peer_connect_status[UDP_MSG_MAX_PLAYERS]
  //// Size = sizeof(ConnectStatus) * UDP_MSG_MAX_PLAYERS.
  //public const int PeerConnectStatusBytes = sizeof(int) * 2 * ProtoConsts.UDP_MSG_MAX_PLAYERS; // two ints per ConnectStatus
  //public fixed byte peer_connect_status_bytes[PeerConnectStatusBytes];
  // public const int CONNECT_STATUS_SIZE = sizeof(int) * ProtoConsts.UDP_MSG_MAX_PLAYERS;
  // private fixed byte peer_connect_status_bytes[CONNECT_STATUS_SIZE];
  private fixed int peer_connect_data[GGPOConsts.UDP_MSG_MAX_PLAYERS];

  public UInt32 start_frame;

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

  public fixed byte bits[GGPOConsts.MAX_COMPRESSED_BITS];



  // ------------------------------------------------------------------------------------------
  // Helpers to get/set a ConnectStatus by index (unsafe)
  internal ConnectStatus GetPeerConnectStatus(int index)
  {
    if (index >= GGPOConsts.UDP_MSG_MAX_PLAYERS)
    {
      throw new ArgumentOutOfRangeException(nameof(index));
    }

    //fixed (byte* basePtr = peer_connect_status_bytes)
    //{
    //  int stride = sizeof(int); // * 2;
    //  byte* p = basePtr + (index * stride);
    //  return *(ConnectStatus*)p;
    //}
    fixed (int* basePtr = peer_connect_data)
    {
      int* p = basePtr + index;
      return *(ConnectStatus*)p;
    }
  }

  // ------------------------------------------------------------------------------------------
  internal void SetPeerConnectStatus(int index, in ConnectStatus value)
  {
    if (index >= GGPOConsts.UDP_MSG_MAX_PLAYERS)
    {
      throw new ArgumentOutOfRangeException(nameof(index));
    }
    //fixed (byte* basePtr = peer_connect_status_bytes)
    //{
    //  int stride = sizeof(int); // * 2;
    //  *(ConnectStatus*)(basePtr + (index * stride)) = value;
    //}
    fixed (int* basePtr = peer_connect_data)
    {
      int* p = basePtr + index;
      *(ConnectStatus*)p = value;
    }
  }
}

// ================================================================================================
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

// ================================================================================================
// NOTE: This is the same as 'Chat', but uses different buffer sizes.  There is probably a way
// to better unify the code.
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct ConnectData
{
  public fixed sbyte text[GGPOConsts.MAX_NAME_SIZE + 1];

  public int GetTextSize()
  {
    fixed (sbyte* p = text)
    {
      return AnsiHelpers.PtrToAnsiStringLength(p, GGPOConsts.MAX_NAME_SIZE + 1);
    }
  }
  public string GetText()
  {
    fixed (sbyte* p = text)
    {
      return AnsiHelpers.PtrToAnsiString(p, GGPOConsts.MAX_NAME_SIZE + 1);
    }
  }

  public void SetText(string value)
  {
    fixed (sbyte* p = text)
    {
      AnsiHelpers.WriteAnsiString(value, p, GGPOConsts.MAX_NAME_SIZE + 1);
    }
  }
}

// ================================================================================================
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct Chat
{
  public fixed sbyte text[GGPOConsts.MAX_GGPOCHAT_SIZE + 1];

  // -------------------------------------------------------------------------------------
  public int GetTextSize()
  {
    fixed (sbyte* p = text)
    {
      return AnsiHelpers.PtrToAnsiStringLength(p, GGPOConsts.MAX_GGPOCHAT_SIZE + 1);
    }
  }

  // -------------------------------------------------------------------------------------
  public string GetText()
  {
    fixed (sbyte* p = text)
    {
      return AnsiHelpers.PtrToAnsiString(p, GGPOConsts.MAX_GGPOCHAT_SIZE + 1);
    }
  }

  // -------------------------------------------------------------------------------------
  public void SetText(string value)
  {
    fixed (sbyte* p = text)
    {
      AnsiHelpers.WriteAnsiString(value, p, GGPOConsts.MAX_GGPOCHAT_SIZE + 1);
    }
  }

  // -------------------------------------------------------------------------------------
  public void SetText(sbyte* data, int size)
  {
    fixed (sbyte* p = text)
    {
      for (int i = 0; i < size; i++)
      {
        text[i] = data[i];
      }
      // AnsiHelpers.WriteAnsiString(value, p, ProtoConsts.MAX_GGPOCHAT_SIZE + 1);
    }
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

  // public ConnectStatus connect_status;
  public MessageHeader header;
  public U u;   // The actual data we care about....

  public UdpMsg() { }
  public UdpMsg(EMsgType msgType)
  {
    header.type = msgType;
  }

  // -------------------------------------------------------------------------------------------------------------
  public unsafe int PacketSize()
  {
    // Makes sure that the compiler isn't getting any ideas....
    Debug.Assert(sizeof(MessageHeader) == 5, "Bad message header size!");

    int res = sizeof(MessageHeader) + PayloadSize();
    return res;
  }

  // -------------------------------------------------------------------------------------------------------------
  public unsafe int PayloadSize()
  {
    int res = 0;


    switch (header.type)
    {
      case EMsgType.SyncRequest:
        return sizeof(SyncRequest);
      case EMsgType.SyncReply:
        {
          res = sizeof(SyncReply);
          return res;
        }

      case EMsgType.QualityReport:
        return sizeof(QualityReport);
      case EMsgType.QualityReply:
        return sizeof(QualityReply);
      case EMsgType.InputAck:
        return sizeof(InputAck);
      case EMsgType.KeepAlive:
        return 0;

      case EMsgType.Input:
        // NOTE: The calculation of the size of this data is a bit wonky.
        // It could actually be computed once when the application starts up, I think....
        // C++ way!
        //res = (int)((char*)&u.input.bits - (char*)&u.input);
        //res += (u.input.num_bits + 7) / 8;

        int size2 = sizeof(InputMsg) - (sizeof(byte) * GGPOConsts.MAX_COMPRESSED_BITS);
        size2 += (u.input.num_bits + 7) / 8;
        // Debug.Assert (size == size2);

        // Assuming that this computation is correct!
        res = size2;

        return res;

      case EMsgType.ChatCommand:
        // Include one extra byte to ensure zero termination.
        res = u.chat.GetTextSize() + 1; //  GetText()  strnlen_s(u.chat.text, MAX_GGPOCHAT_SIZE) + 1;
        return res;

      default:
        throw new InvalidOperationException($"Unsupported type: {header.type}!");
    }

  }



  //// --------------------------------------------------------------------------------------------------------------
  //public static unsafe void FromBytes(byte[] data, ref UdpMsg res, int len)
  //{
  //  // NEW:  Unsafe style!
  //  // This is cool b/c we don't have to get too worried about the struct definitions / write interop code...
  //  if (data == null) throw new ArgumentNullException(nameof(data));
  //  if (data.Length > sizeof(UdpMsg)) throw new ArgumentException("Too much data for UdpMsg", nameof(data));

  //  fixed (byte* pSrc = data)
  //  {
  //    res = *(UdpMsg*)pSrc;
  //  }

  //  // OLD:
  //  //res.header.magic = BitConverter.ToUInt16(data, 0);
  //  //res.header.sequence_number = BitConverter.ToUInt16(data, sizeof(UInt16));
  //  //res.header.type = (EMsgType)data[sizeof(UInt16) * 2];

  //  //switch (res.header.type)
  //  //{
  //  //  case EMsgType.SyncRequest:
  //  //    SyncRequest.FromBytes(data, SIZE_OF_MESSAGE_HEADER, ref res.u.sync_request);
  //  //    break;

  //  //  default:
  //  //    throw new InvalidOperationException($"The header type: {res.header.type} is not supported!");
  //  //}
  //}
  public static unsafe void FromBytes(byte[] data, ref UdpMsg res, int len)
  {
    if (data == null) throw new ArgumentNullException(nameof(data));
    if (len < 0 || len > data.Length) throw new ArgumentOutOfRangeException(nameof(len));
    if (len > sizeof(UdpMsg)) throw new ArgumentException("len exceeds size of UdpMsg");

    // Create a stack buffer the size of UdpMsg and zero it first.
    // This way any missing bytes beyond len are safely zero.
    byte* buffer = stackalloc byte[sizeof(UdpMsg)];
    for (int i = 0; i < sizeof(UdpMsg); i++) buffer[i] = 0;

    // Copy only 'len' bytes from the source
    fixed (byte* pSrc = data)
    {
      Buffer.MemoryCopy(pSrc, buffer, sizeof(UdpMsg), len);
    }

    // Now reinterpret into UdpMsg
    res = *(UdpMsg*)buffer;
  }

  //// --------------------------------------------------------------------------------------------------------------
  //public static unsafe void ToBytes(in UdpMsg msg, byte[] dst, int copySize)
  //{
  //  // NOTE: whatever calls this code needs a way to make sure that we aren't
  //  // always going to send the full size of the message.  Many times, the payload can be
  //  // smaller than sizeof(UdpMsg) especially when there isn't chat data, etc.
  //  // throw new Exception();

  //  if (dst is null) { throw new ArgumentNullException(nameof(dst)); }
  //  if (dst.Length < sizeof(UdpMsg)) { throw new ArgumentException($"Too short for {nameof(UdpMsg)}", nameof(dst)); }

  //  // NOTE: We might even come up with a way to only copy the bytes we need.
  //  // --> copySize
  //  fixed (byte* pDst = dst)
  //  {
  //    *(UdpMsg*)pDst = msg;
  //  }
  //}
  public static unsafe void ToBytes(in UdpMsg msg, byte[] dest, int len)
  {
    if (dest == null) throw new ArgumentNullException(nameof(dest));
    if (len < 0 || len > dest.Length) throw new ArgumentOutOfRangeException(nameof(len));
    if (len > sizeof(UdpMsg)) throw new ArgumentException("len exceeds size of UdpMsg");

    fixed (byte* pDest = dest)
    fixed (UdpMsg* pSrc = &msg)
    {
      Buffer.MemoryCopy(pSrc, pDest, len, len);
    }
  }

}

// ==============================================================================================================
public static unsafe class AnsiHelpers
{
  // ------------------------------------------------------------------------------------
  public static int PtrToAnsiStringLength(sbyte* p, int maxLen)
  {
    int len = 0;
    while (len < maxLen && p[len] != 0)
    {
      len++;
    }
    return len;
  }

  // ------------------------------------------------------------------------------------
  public static string PtrToAnsiString(sbyte* p, int maxLen)
  {
    int len = 0;
    while (len < maxLen && p[len] != 0)
    {
      len++;
    }
    return Encoding.ASCII.GetString((byte*)p, len);
  }

  // ------------------------------------------------------------------------------------
  public static void WriteAnsiString(string value, sbyte* dest, int capacity)
  {
    var bytes = Encoding.ASCII.GetBytes(value ?? string.Empty);
    int n = Math.Min(bytes.Length, Math.Max(0, capacity - 1)); // leave room for NULL
    for (int i = 0; i < n; i++)
    {
      dest[i] = (sbyte)bytes[i];
    }

    dest[n] = 0;
    for (int i = n + 1; i < capacity; i++)
    {
      dest[i] = 0;
    }
  }
}
