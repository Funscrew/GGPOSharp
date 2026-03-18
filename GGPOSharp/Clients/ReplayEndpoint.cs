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
  public class ReplayEndpoint : GGPOEndpoint
  {
    // --------------------------------------------------------------------------------------------------------------------------  
    public ReplayEndpoint(IGGPOClient client_, GGPOEndpointOptions ops_, ConnectStatus[] localConnectStatus_)
      : base(client_, ops_, localConnectStatus_)
    {

    }

    // --------------------------------------------------------------------------------------------------------------------------  
    public override void OnLoopPoll()
    {
      base.OnLoopPoll();
    }

    //// --------------------------------------------------------------------------------------------------------------------------  
    //public override void OnLoopPoll()
    //{
    //  base.OnLoopPoll();

    //  // NOTE: Personally, I think that all of the endpoints should be responsible for handling
    //  // the events internally.  That is something we can care about later however...
    //  var evt = new UdpEvent();
    //  while (this.GetEvent(ref evt))
    //  {


    //    switch (evt.type)
    //    {
    //      case EEventType.Connected:
    //      case EEventType.Synchronizing:
    //      case EEventType.Synchronized:
    //      int x = 10;
    //      break;

    //      default:
    //        throw new ArgumentOutOfRangeException($"No support for event type: {evt.type}");
    //    }


    //  }
    //}


  }
}
