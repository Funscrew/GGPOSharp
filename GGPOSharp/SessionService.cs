using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GGPOSharp
{
  // ==============================================================================================================================
  public class SessionService
  {
    private ulong LastSessionId = 0;
    private object GetSessionLock = new object();

    // --------------------------------------------------------------------------------------------------------------------------
    public SessionService()
    {
      GetNextSessionId();
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public ulong GetNextSessionId()
    {
      lock (GetSessionLock)
      {
        while (true)
        {
          ulong res = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
          if (res == LastSessionId)
          {
            continue;
          }
          LastSessionId = res;
          return res;
        }
      }
    }

  }
}
