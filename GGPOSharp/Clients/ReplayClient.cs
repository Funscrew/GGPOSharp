using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GGPOSharp.Clients
{

  // ==============================================================================================================================
  /// <summary>
  /// This is very much like GGPOClient, but it is only for sending replay data to an appliance.
  /// </summary>
  internal class ReplayClient : GGPOEndpoint
  {
    // --------------------------------------------------------------------------------------------------------------------------  
    public ReplayClient(GGPOClient client_, GGPOEndpointOptions ops_, ConnectStatus[] localConnectStatus_) 
      : base(client_, ops_, localConnectStatus_)
    {
    }
  }
}
