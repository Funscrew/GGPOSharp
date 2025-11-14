namespace GGPOSharp;

// =======================================================================================
public static class Utils
{

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
