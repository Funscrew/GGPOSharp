using System.Diagnostics;

namespace GGPOSharp;

// =======================================================================================
public static class Utils
{
  public static bool IsLoggingEnabled = true;

  // ------------------------------------------------------------------------
  internal static void LogEvent(string v, Event evt)
  {
    Debug.WriteLine("implement this logging!");
    // throw new NotImplementedException();
  }

  // ------------------------------------------------------------------------
  // REFACTOR / NOTE:
  internal static void Log(string msgType, ref UdpMsg msg)
  {
    Debug.WriteLine("implement this logging!");
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
  internal static void Log(string msg)
  {
    // TODO: Hand this off to a real logging facility....
    Console.WriteLine(msg);
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
}
