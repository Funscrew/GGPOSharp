using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace GGPOSharp
{

  // ==============================================================================================================================
  /// <summary>
  /// Like a P2P endpoint, but does far fewer things as it really only need to collect inputs, and make sure that all of them have come in.
  /// </summary>
  internal class ReplayEndpoint
  {

    private EndPoint RemoteEP = null!;

    // --------------------------------------------------------------------------------------------------------------------------
    public ReplayEndpoint(EndPoint remoteEp_, int playerIndex_)
    {
      RemoteEP = remoteEp_;
      PlayerIndex = playerIndex_;
    }

    public int PlayerIndex { get; private set; }

    /// <summary>
    /// What is the highest frame number that we have received.
    /// </summary>
    public int MaxFrameCount { get { return 0; } }


  }

}
