using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace GGPOSharp;

// =======================================================================================
public class GGPOLogOptions
{
  public bool LogToFile { get; set; } = false;
  public string FilePath { get; set; } = string.Empty;

  /// <summary>
  /// Comma delimited list of categories that will be logged.  All others will be ignored.
  /// If empty, all categories will be logged.
  /// </summary>
  public string ActiveCategories { get; set; } = string.Empty;
}

// =======================================================================================
public static class Utils
{
  public const int LOG_VERSION = 1;

  public static bool IsLoggingEnabled = false;

  private static GGPOLogOptions LogOptions = null!;
  private static bool IsLogActive = false;
  private static FileStream? LogStream = null;

  private static Stopwatch Clock = Stopwatch.StartNew();

  // ------------------------------------------------------------------------
  internal static void InitLogging(GGPOLogOptions options_)
  {
    LogOptions = options_;
    IsLogActive = LogOptions.LogToFile;

    if (IsLogActive && !string.IsNullOrWhiteSpace(LogOptions.FilePath))
    {
      LogStream = File.OpenWrite(LogOptions.FilePath);

      // Write the init message...
      // TODO: Maybe we could add some more information about the current GGPO settings?  delay, etc.?
      WriteString(LogStream, "# GGPO-LOG\n");
      WriteString(LogStream, "# VERSION:%d\n", LOG_VERSION);

      int len = LogOptions.ActiveCategories.Length;
      WriteString(LogStream, "# ACTIVE: %s\n", len == 0 ? "[ALL]" : LogOptions.ActiveCategories);
      WriteString(LogStream, "# START:%d\n", Clock.ElapsedMilliseconds);
    }
  }

  // ------------------------------------------------------------------------
  internal static void WriteString(Stream to, string fmt, params object[] args)
  {
    string toWrite = string.Format(fmt, args);
    var data = Encoding.UTF8.GetBytes(toWrite);
    to.Write(data, 0, data.Length);
  }

  // ------------------------------------------------------------------------
  internal static void CloseLog()
  {
    if (LogStream != null)
    {
      LogStream.Close();
      LogStream = null;
    }
  }

  // ------------------------------------------------------------------------
  internal static bool IsCategoryActive(string category)
  {
    bool res = LogOptions.ActiveCategories.Contains(category);
    return res;
  }

  // ------------------------------------------------------------------------
  internal static void LogIt(string category, string fmt, params object[] args)
  {
    if (!IsLogActive || !IsCategoryActive(category)) { return; }

    long time = Clock.ElapsedMilliseconds;
    string msg = $"{time}|{category}|{string.Format(fmt, args)}\n";
    WriteString(LogStream!, msg);
  }

  // ------------------------------------------------------------------------
  internal static void LogEvent(string v, UdpEvent evt)
  {
    if (!IsLogActive || !IsCategoryActive(LogCategories.EVENT)) { return; }

    LogIt(LogCategories.EVENT, "%d", (int)evt.type);
  }

  // ----------------------------------------------------------------------------------------------------------------
  internal static void LogMsg(EMsgDirection dir, ref UdpMsg msg)
  {
    if (!IsLogActive || !IsCategoryActive(LogCategories.MESSAGE)) { return; }

    string msgBuf = $"{(int)dir}:{msg.header.type}:{msg.header.sequence_number}";

    // Original....
    switch (msg.header.type)
    {
      case EMsgType.SyncRequest:
        msgBuf += msg.u.sync_request.random_request;
        break;

      case EMsgType.SyncReply:
        msgBuf += msg.u.sync_reply.random_reply;
        break;

      case EMsgType.Input:
        msgBuf += $"{msg.u.input.start_frame}:{msg.u.input.num_bits}";
        break;

      case EMsgType.QualityReport:
        break;
      case EMsgType.QualityReply:
        break;
      case EMsgType.KeepAlive:
        break;
      case EMsgType.InputAck:
        break;
      case EMsgType.ChatCommand:
        break;

      default:
        ASSERT(false, "Unknown UdpMsg type.");
        break;
    }

    LogIt(LogCategories.MESSAGE, msgBuf);
  }


  // ----------------------------------------------------------------------------------------------------------------
  internal static void LogNetworkStats(int totalBytesSent, int totalPacketsSent, int ping)
  {
    if (!IsLogActive || !IsCategoryActive(LogCategories.NETWORK)) { return; }

    LogIt(LogCategories.NETWORK, "%d:%d:%d", totalBytesSent, totalPacketsSent, ping);
  }

  //// ------------------------------------------------------------------------
  //// Variadic version.  This is what we will want to convert everything to.
  //[Obsolete]
  //internal static void Log(string fmt, params object[] args)
  //{
  //  if (!IsLoggingEnabled) { return; }
  //  string msg = string.Format(fmt, args);
  //}

  // ------------------------------------------------------------------------
  // We do a lot of logging, and that logging is conditional.
  // Most of the messages that we pass in are interpolated strings.
  // That means that even if logging is disabled, then we are doing all of the work
  // to format those strings, and then they get nuked!  I want to change this so that we
  // call into the same type of function that the C++ uses (probably sprintf) so that
  // we only do the work IF we are actually going to do something with the data!
  [Obsolete]
  internal static void Log(string msg)
  {
    // TODO: Hand this off to a real logging facility....
    if (Utils.IsLoggingEnabled)
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


// ====================================================================================================================
public enum EMsgDirection
{
  Send = 0,
  Receive = 1
}

// ====================================================================================================================
public static class LogCategories
{
  public const string GENERAL = "NA";
  public const string MESSAGE = "MSG";
  public const string ENDPOINT = "EP";
  public const string EVENT = "EVT";
  public const string SYNC = "SYNC";
  public const string RUNNING = "RUN";
  public const string CONNECTION = "CONN";
  public const string ERROR = "ERR";
  public const string NETWORK = "NET";
  public const string INPUT = "INP";
  public const string TEST = "TEST";
  public const string UDP = "UDP";
  public const string INPUT_QUEUE = "INPQ";
  public const string TIMESYNC = "TIME";
}
