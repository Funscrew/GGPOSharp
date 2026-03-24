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
    ReplayAppliance Appliance = null;

    /// <summary>
    /// The acks that we still need to send out.
    /// </summary>
    private RingBuffer<GameInput> _PendingAcks = new RingBuffer<GameInput>(64);

    // --------------------------------------------------------------------------------------------------------------------------  
    public ReplayEndpoint(IGGPOClient client_, GGPOEndpointOptions ops_, ConnectStatus[] localConnectStatus_)
      : base(client_, ops_, localConnectStatus_)
    {
      this.Appliance = this.Client as ReplayAppliance;
      //if (this.Appliance == null)
      //{
      //  throw new InvalidOperationException($"Parent should be a {nameof(ReplayAppliance)} instance!");
      //}
    }

    // --------------------------------------------------------------------------------------------------------------------------  
    public override void OnLoopPoll()
    {
      base.OnLoopPoll();
      SendPendingAcks();
    }

    // --------------------------------------------------------------------------------------------------------------------------  
    protected override bool OnInput(ref UdpMsg msg, int msgLen)
    {
      // The replay client needs to do that same thing as the normal endpoint by keeping
      // a set of ACKS to send back to the client. 
      // In this case our parent is going to be a replay appliance so we may need to keep a separate list
      // of stuff that needs to be acked / resolved on our end.....
      if (this.Appliance != null)
      {
        this.Appliance.MergeInput(msg);
      }

      int xawf = 10;
      // Housekeeping.....
      bool res = base.OnInput(ref msg, msgLen);

      // Let's grab the inputs?


      return res;

      // We got an input, so we need to relay it to the client (parent) so that it can do the merge / record.
      // We need to ignore it if we have received this before. --> Main client can do that.
      // We need to ACK the inputs received as well.

      // In order to deal with OOP, we will only ACK packets up to a certain contiguous number.

    }

    // --------------------------------------------------------------------------------------------------------------------------  
    protected override void SendInputEvent(ref GameInput input)
    {
      base.SendInputEvent(ref input);

      // Send the ACK for this input!
      SendInputAck(ref input);
    }

    // --------------------------------------------------------------------------------------------------------------------------  
    private void SendInputAck(ref GameInput input)
    {
      _PendingAcks.Push(input);
      SendPendingAcks();
    }

    // --------------------------------------------------------------------------------------------------------------------------  
    /// <summary>
    /// This is like 'SendPendingOutput' but it is for input ACK messages.
    /// </summary>
    private void SendPendingAcks()
    {
      while (_PendingAcks.Size > 0)
      {
        // NOTE: Getting a ref to this would be ideal.....
        var i = _PendingAcks.First();

        // Get the input:
        // Send the ack data.
        var msg = new UdpMsg(EMsgType.InputAck);
        msg.u.input_ack.ack_frame = i.frame;

        SendMsg(ref msg);

        // NOTE: We need to do some book keeping to make sure that we are allowed to remove this...
        // We need to check the ACK frame from the received inputs first....
        _PendingAcks.Pop();
      }
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
