namespace GGPOSharp;

// =======================================================================================
public static class Utils
{
  // -------------------------------------------------------------------------------------
  // TODO: Add a message to this.
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
