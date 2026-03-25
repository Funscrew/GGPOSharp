using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

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
      //if (this.Appliance != null)
      //{
      //  this.Appliance.MergeInput(msg);
      //}

      int xawf = 10;
      bool res = base.OnInput(ref msg, msgLen);

      // Housekeeping.  We can get rid of all confirmed acks.
      // TODO: I'd like to log the size of these ring buffers to see what is typical.  Is there really a certain amount of 'overdraw' in the system always?
      while (_PendingAcks.Size > 0 && _PendingAcks.Front().frame < msg.u.input.ack_frame)
      {
        Utils.LogIt(LogCategories.INPUT, "ACK: Throwing away pending ACK frame %d", _PendingAcks.Front().frame);
        _last_acked_input = _PendingAcks.Front();
        _PendingAcks.Pop();

        if (this.Appliance != null)
        {
          this.Appliance.MergeInput(ref _last_acked_input, this.PlayerIndex);
        }
      }

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
      // GameInput last;
      // NEW:
      // We will collect all of the pending acks and send a message for each:
      // In the future we can combine them all into a single message to ACK mulitple inputs.
      if (_PendingAcks.Size > 0)
      {
        var last = _last_acked_input;
        var front = _PendingAcks.Front();
        Utils.ASSERT(last.frame == -1 || last.frame + 1 == front.frame);

        for (int i = 0; i < _PendingAcks.Size; i++)
        {
          var ack = _PendingAcks[i];

          var msg = new UdpMsg(EMsgType.InputAck);
          msg.u.input_ack.ack_frame = ack.frame;

          SendMsg(ref msg);
        }
      }

    }

  }
}
