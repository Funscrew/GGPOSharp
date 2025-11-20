using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace GGPOSharp;

// =======================================================================================
public static class Utils
{
  public static bool IsLoggingEnabled = false;

  private static FileStream? MsgLogFile = null;

  // ------------------------------------------------------------------------
  static Utils()
  {
    // TEMP: I am just going to slap some data to disk so we can figure out some issues....
    string msgLogPath = "msglog.txt";
    if (File.Exists(msgLogPath))
    {
      File.Delete(msgLogPath);
    }
    MsgLogFile = File.OpenWrite(msgLogPath);


  }

  // ------------------------------------------------------------------------
  internal static void LogEvent(string v, Event evt)
  {
    Debug.WriteLine("implement this logging!");
    // throw new NotImplementedException();
  }

  // ------------------------------------------------------------------------
  // REFACTOR / NOTE:
  internal unsafe static void Log(string msgType, ref UdpMsg msg)
  {
    return;

    if (msgType == "send" && msg.header.type == EMsgType.Input)
    {
      fixed (byte* pb = msg.u.input.bits)
      {

        //int bits = msg.u.input.num_bits;
        //string bitString = string.Empty;
        //for (int i = 0; i < bits; i++)
        //{
        //  int curByte = i / 8;
        //  int bitVal = (msg.u.input.bits[curByte] >> i) & 0x1;
        //  bitString += bitVal == 1 ? "1" : "0";
        //}

        var peerData = new List<string>();
        for (int i = 0;i< GGPOConsts.UDP_MSG_MAX_PLAYERS; i++) {
          ConnectStatus cs =  msg.u.input.GetPeerConnectStatus(i);
          peerData.Add(cs.last_frame.ToString());
        }
        string peerVal = string.Join(":", peerData);

        string nextLine = $"input: {msg.u.input.start_frame} - {msg.u.input.num_bits} - ({peerVal}) {Environment.NewLine}";

        byte[] msgBuffer = Encoding.UTF8.GetBytes(nextLine);
        MsgLogFile.Write(msgBuffer, 0, msgBuffer.Length);
        MsgLogFile.Flush();

        // Debug.WriteLine("implement this logging!");
      }
    }
  }

  // ------------------------------------------------------------------------
  // Variadic version.  This is what we will want to convert everything to.
  internal static void Log(string fmt, params object[] args)
  {
    if (!IsLoggingEnabled) { return; }
    string msg = string.Format(fmt, args);
  }

  // ------------------------------------------------------------------------
  // We do a lot of logging, and that logging is conditional.
  // Most of the messages that we pass in are interpolated strings.
  // That means that even if logging is disabled, then we are doing all of the work
  // to format those strings, and then they get nuked!  I want to change this so that we
  // call into the same type of function that the C++ uses (probably sprintf) so that
  // we only do the work IF we are actually going to do something with the data!
  internal static void Log(string msg, bool display = true)
  {
    // TODO: Hand this off to a real logging facility....
    if (display)
    {
      Console.WriteLine(msg);
    }
  }



  // ----------------------------------------------------------------------------------------
  public static unsafe bool MemMatches(byte* data1, byte* data2, int size)
  {
    // probably not as fast as memcmp, but that is OK for now...
    for (int i = 0; i < size; i++)
    {
      if (data1[i] != data2[i])
      {
        return false;
      }
    }
    return true;
  }

  // -------------------------------------------------------------------------------------
  public static void ASSERT(bool condition, string? msg = null)
  {
    if (!condition)
    {
      string errMsg = "Assert failed!";
      if (msg != null)
      {
        errMsg += (" " + msg);
      }
      throw new InvalidOperationException(errMsg);
    }
  }

  // -------------------------------------------------------------------------------------
  internal static unsafe void ClearMem(byte* output, int size)
  {
    // TODO: There is probably a more efficient way to do this....
    for (int i = 0; i < size; i++)
    {
      output[i] = 0;
    }
  }

  // -------------------------------------------------------------------------------------
  internal unsafe static void CopyMem(byte[] dest, int destOffset, byte* src, uint size)
  {
    for (int i = 0; i < size; i++)
    {
      dest[destOffset + i] = src[i];
    }
  }

  // -------------------------------------------------------------------------------------
  internal static unsafe void CopyMem(void* dest, void* src, uint size)
  {
    Unsafe.CopyBlock(dest, src, size);
  }
}
