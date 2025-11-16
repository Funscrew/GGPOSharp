using System.Text;


namespace GGPOSharp;

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
