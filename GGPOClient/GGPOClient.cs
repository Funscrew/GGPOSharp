using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GGPOClient
{

  // ================================================================================================================
  public class GGPOClient
  {
  }




  // ================================================================================================================
  [StructLayout(LayoutKind.Explicit)]
  public struct TestUnion
  {
    // This should overlap 'Single' by 4 bytes.
    [FieldOffset(0)] public double Double;
    [FieldOffset(0)] public float Single;
  }


  //// ================================================================================================================
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

  //[StructLayout(LayoutKind.Explicit)]
  //public struct SyncReply
  //{
  //  [FieldOffset(0)]
  //}



}
