namespace GGPOSharp
{

  // ==============================================================================================================================
  public class ReplayEndpoint : GGPOEndpoint
  {
    public int RemotePlayerIndex { get; private set; }

    // ----------------------------------------------------------------------------------------------------------------
    public ReplayEndpoint(GGPOClient client_, GGPOEndpointOptions ops_, ConnectStatus[] localConnectStatus_, int remotePlayerIndex_)
      : base(client_, ops_, localConnectStatus_)
    { 
      RemotePlayerIndex = remotePlayerIndex_;
    }

  }

  //// ==============================================================================================================================
  ///// <summary>
  ///// Like a P2P endpoint, but does far fewer things as it really only need to collect inputs, and make sure that all of them have come in.
  ///// </summary>
  //internal class ReplayEndpoint_V1
  //{

  //  private EndPoint RemoteEP = null!;
  //  private byte[] ReceiveBuffer = new byte[8192];
  //  private GGPOClient Client = null;

  //  // BASE CLASS VARS
  //  private Stopwatch Clock { get { return Client.Clock; } }
  //  private UInt16 _remote_magic_number;
  //  public UInt16 _next_send_seq = 0;
  //  public UInt16 _next_recv_seq = 0;
  //  public EClientState _current_state { get; private set; } = EClientState.Disconnected;
  //  private uint _last_recv_time = 0;
  //  private bool _disconnect_notify_sent = false;
  //  private MsgHandler<UdpMsg>[] MsgHandlers = new MsgHandler<UdpMsg>[9];
  //  RingBuffer<UdpEvent> _event_queue = new RingBuffer<UdpEvent>(64);

  //  // --------------------------------------------------------------------------------------------------------------------------
  //  public ReplayEndpoint(GGPOClient client_, EndPoint remoteEp_, int playerIndex_)
  //  {
  //    Client = client_;
  //    RemoteEP = remoteEp_;
  //    PlayerIndex = playerIndex_;

  //    MsgHandlers[(byte)EMsgType.Invalid] = OnInvalid;
  //    MsgHandlers[(byte)EMsgType.SyncRequest] = OnSyncRequest;
  //    MsgHandlers[(byte)EMsgType.SyncReply] = OnSyncReply;
  //    MsgHandlers[(byte)EMsgType.Input] = OnInput;
  //    MsgHandlers[(byte)EMsgType.QualityReport] = NoOp;
  //    MsgHandlers[(byte)EMsgType.QualityReply] = NoOp;
  //    MsgHandlers[(byte)EMsgType.KeepAlive] = NoOp;
  //    MsgHandlers[(byte)EMsgType.InputAck] = NoOp;
  //    MsgHandlers[(byte)EMsgType.Datagram] = OnDatagram;

  //  }

  //  public int PlayerIndex { get; private set; }

  //  /// <summary>
  //  /// What is the highest frame number that we have received.
  //  /// </summary>
  //  public int MaxFrameCount { get { return 0; } }

  //  // TODO: Make part of endpoint interface?
  //  public void OnLoopPoll() { 
  //    ReceiveMessages();
  //  }


  //  // ------------------------------------------------------------------------
  //  // REFACTOR: MAke part of a base class...
  //  private void ReceiveMessages()
  //  {
  //    // Pull in all messages, while they are available.
  //    //while (Client.Available > 0)
  //    //{
  //    while (true)
  //    {
  //      // Get the next message.....
  //      // byte[] data = Client.Receive
  //      int received = Client.UDP.Receive(ReceiveBuffer, ref RemoteEP);
  //      if (received == 0)
  //      {
  //        break;
  //      }

  //      UdpMsg msg = new UdpMsg();
  //      UdpMsg.FromBytes(ReceiveBuffer, ref msg, received);

  //      // Now that we have the message we can do something with it....
  //      HandleMessage(ref msg, received);
  //    }
  //  }


  //  // ------------------------------------------------------------------------
  //  private void HandleMessage(ref UdpMsg msg, int msgLen)
  //  {

  //    // filter out messages that don't match what we expect
  //    UInt16 seq = msg.header.sequence_number;
  //    if (msg.header.type != EMsgType.SyncRequest && msg.header.type != EMsgType.SyncReply)
  //    {
  //      if (msg.header.magic != _remote_magic_number)
  //      {
  //        Utils.LogIt(LogCategories.MESSAGE, "magic-mismatch");
  //        return;
  //      }

  //      // filter out out-of-order packets
  //      UInt16 skipped = (UInt16)((int)seq - (int)_next_recv_seq);
  //      // Log("checking sequence number . next - seq : %d - %d = %d", seq, _next_recv_seq, skipped);
  //      if (skipped > GGPOConsts.MAX_SEQ_DISTANCE)
  //      {
  //        Utils.LogIt(LogCategories.ENDPOINT, "OOP dropped: (seq: %d, last seq:%d)", seq, _next_recv_seq);
  //        return;
  //      }
  //    }

  //    _next_recv_seq = seq;
  //    Utils.LogMsg(EMsgDirection.Receive, ref msg);

  //    if ((int)msg.header.type >= MsgHandlers.Length)
  //    {
  //      OnInvalid(ref msg, msgLen);
  //    }

  //    var handler = this.MsgHandlers[(int)msg.header.type];
  //    bool handled = handler(ref msg, msgLen);

  //    if (handled)
  //    {
  //      _last_recv_time = (uint)Clock.ElapsedMilliseconds;
  //      if (_disconnect_notify_sent && _current_state == EClientState.Running)
  //      {
  //        QueueEvent(new UdpEvent(EEventType.NetworkResumed)); // Event(Event::NetworkResumed));
  //        _disconnect_notify_sent = false;
  //      }
  //    }
  //  }

  //  // ------------------------------------------------------------------------
  //  private void QueueEvent(in UdpEvent evt)
  //  {
  //    Utils.LogEvent("Queuing event", evt);
  //    _event_queue.Push(evt);
  //  }

  //  // ------------------------------------------------------------------------
  //  // REFACTOR: BASE CLASS
  //  private bool OnInvalid(ref UdpMsg msg, int msgLen)
  //  {
  //    throw new GGPOException("Invalid message!");
  //  }

  //  // ------------------------------------------------------------------------
  //  /// <summary>
  //  /// Use this for a message that we don't need, or want to handle.
  //  /// </summary>
  //  private bool NoOp(ref UdpMsg msg, int msgLen)
  //  {
  //    return true;
  //  }

  //  // ------------------------------------------------------------------------
  //  /// <summary>
  //  /// Use this for a message that we don't need, or want to handle.
  //  /// </summary>
  //  private bool OnDatagram(ref UdpMsg msg, int msgLen)
  //  {
  //    Log.Warning("datagram is not handled in the replay appliance!");
  //    return true;
  //  }

  //  // ------------------------------------------------------------------------
  //  private bool OnSyncRequest(ref UdpMsg msg, int msgLen)
  //  {
  //    if (_remote_magic_number != 0 && msg.header.magic != _remote_magic_number)
  //    {
  //      Utils.LogIt(LogCategories.SYNC, "SyncRequest from unknown endpoint :%d != %d)", msg.header.magic, _remote_magic_number);
  //      return false;
  //    }
  //    UdpMsg reply = new UdpMsg(EMsgType.SyncReply);
  //    reply.u.sync_reply.random_reply = msg.u.sync_request.random_request;
  //    reply.u.sync_reply.client_version = this.Client.ClientVersion;
  //    reply.u.sync_reply.delay = 0;
  //    reply.u.sync_reply.runahead = 0;

  //    // So this endpoint is responding to a sync request, so we should be replying with the name
  //    // of the local player....
  //    // I think that the sync requests should be the ones that have the player names set....
  //    // _playerName = "abc";
  //    reply.u.sync_reply.SetPlayerName("REPLAY_APPLIANCE");

  //    SendMsg(ref reply);
  //    return true;
  //  }

  //  // -------------------------------------------------------------------------------------
  //  // REFACTOR: BASE CLASS
  //  public unsafe void SendMsg(ref UdpMsg msg)
  //  {
  //    _packets_sent++;
  //    _last_send_time = (uint)Clock.ElapsedMilliseconds;
  //    _bytes_sent += msg.PacketSize();

  //    msg.header.magic = _magic_number;
  //    msg.header.sequence_number = _next_send_seq++;

  //    _send_queue.Push(new QueueEntry()
  //    {
  //      queue_time = (int)Clock.ElapsedMilliseconds,
  //      dest_addr = this.RemoteIP,

  //      // NOTE: This is a BIG copy, so we will find a different way to handle it in the future.
  //      // probably index into a fixed size array.
  //      msg = msg,
  //    });

  //    Utils.LogMsg(EMsgDirection.Send, ref msg);
  //    PumpSendQueue();
  //  }

  //  // -------------------------------------------------------------------------------------
  //  private bool OnInput(ref UdpMsg msg, int msgLen)
  //  {
  //    /*
  //     * If a disconnect is requested, go ahead and disconnect now.
  //     */
  //    bool disconnect_requested = msg.u.input.disconnect_requested;
  //    if (disconnect_requested)
  //    {
  //      if (_current_state != EClientState.Disconnected && !_disconnect_event_sent)
  //      {
  //        QueueEvent(new UdpEvent(EEventType.Disconnected));
  //        _disconnect_event_sent = true;
  //      }
  //    }
  //    else
  //    {
  //      /*
  //       * Update the peer connection status if this peer is still considered to be part
  //       * of the network.
  //       */
  //      //var remote_status = msg.u.input.peer_connect_status;
  //      for (int i = 0; i < _peer_connect_status.Length; i++)
  //      {
  //        var remote_status = msg.u.input.GetPeerConnectStatus(i);
  //        Utils.ASSERT(remote_status.last_frame >= _peer_connect_status[i].last_frame);

  //        _peer_connect_status[i].disconnected = _peer_connect_status[i].disconnected || remote_status.disconnected;
  //        _peer_connect_status[i].last_frame = Math.Max(_peer_connect_status[i].last_frame, remote_status.last_frame);
  //      }
  //    }

  //    /*
  //     * Decompress the input.
  //     */
  //    int last_received_frame_number = _last_received_input.frame;
  //    unsafe
  //    {
  //      if (msg.u.input.num_bits > 0)
  //      {
  //        int offset = 0;
  //        fixed (byte* bits = msg.u.input.bits)
  //        {

  //          int numBits = msg.u.input.num_bits;
  //          int currentFrame = (int)msg.u.input.start_frame;

  //          _last_received_input.size = msg.u.input.input_size;
  //          if (_last_received_input.frame < 0)
  //          {
  //            _last_received_input.frame = (int)msg.u.input.start_frame - 1;
  //          }
  //          while (offset < numBits)
  //          {
  //            /*
  //             * Keep walking through the frames (parsing bits) until we reach
  //             * the inputs for the frame right after the one we're on.
  //             */
  //            if (currentFrame > (_last_received_input.frame + 1))
  //            {
  //              throw new InvalidOperationException("invalid frame number!");
  //            }
  //            bool useInputs = currentFrame == _last_received_input.frame + 1;

  //            while (BitVector.ReadBit(bits, ref offset) != 0)
  //            {
  //              bool on = BitVector.ReadBit(bits, ref offset) == 1;
  //              int button = BitVector.ReadNibblet(bits, ref offset);
  //              if (useInputs)
  //              {
  //                if (on)
  //                {
  //                  _last_received_input.set(button);
  //                }
  //                else
  //                {
  //                  _last_received_input.clear(button);
  //                }
  //              }
  //            }
  //            Utils.ASSERT(offset <= numBits);

  //            /*
  //             * Now if we want to use these inputs, go ahead and send them to
  //             * the emulator.
  //             */
  //            if (useInputs)
  //            {
  //              /*
  //               * Move forward 1 frame in the stream.
  //               */
  //              Utils.ASSERT(currentFrame == _last_received_input.frame + 1);
  //              _last_received_input.frame = currentFrame;

  //              /*
  //               * Send the event to the emualtor
  //               */
  //              // UdpProtocol::Event evt(UdpProtocol::Event::Input);
  //              var evt = new UdpEvent(EEventType.Input);
  //              evt.u.input = _last_received_input;

  //              // NOTE: This is expensive loggin that we are not going to support in the C# version.  It
  //              // should be replaced with something better tho.
  //              // string desc = "NOT SUPPORTED - REWRITE";
  //              //const int DESC_SIZE = 1024;
  //              //byte[] desc = new byte[1024];
  //              //_last_received_input.desc(desc, DESC_SIZE);

  //              RunningState.last_input_packet_recv_time = (uint)Clock.ElapsedMilliseconds;

  //              // Utils.Log("Sending frame %d to emu queue %d (%d).", _last_received_input.frame, _queue, desc);
  //              QueueEvent(evt);

  //            }
  //            else
  //            {
  //              Utils.LogIt(LogCategories.INPUT, "Skip:%d (%d)", currentFrame, _last_received_input.frame);
  //            }

  //            /*
  //             * Move forward 1 frame in the input stream.
  //             */
  //            currentFrame++;
  //          }
  //        }
  //      }
  //    }

  //    Utils.ASSERT(_last_received_input.frame >= last_received_frame_number);

  //    /*
  //     * Get rid of our buffered input
  //     */
  //    while (_pending_output.Size > 0 && _pending_output.Front().frame < msg.u.input.ack_frame)
  //    {
  //      Utils.LogIt(LogCategories.INPUT, "ACK: Throwing away pending output frame %d", _pending_output.Front().frame);
  //      _last_acked_input = _pending_output.Front();
  //      _pending_output.Pop();
  //    }
  //    return true;
  //  }


  //  // ------------------------------------------------------------------------
  //  private bool OnSyncReply(ref UdpMsg msg, int msgLen)
  //  {
  //    if (_current_state != EClientState.Syncing)
  //    {
  //      Utils.LogIt(LogCategories.SYNC, "SyncReply while not synching");
  //      return msg.header.magic == _remote_magic_number;
  //    }

  //    if (msg.u.sync_reply.random_reply != SyncState.random)
  //    {
  //      Utils.LogIt(LogCategories.SYNC, "mismatched reply: %d != %d", msg.u.sync_reply.random_reply, SyncState.random);
  //      return false;
  //    }

  //    if (!_connected)
  //    {
  //      var evt = new UdpEvent(EEventType.Connected);

  //      // TODO: The player names should be sent out with the sync request NOT the reply!
  //      string pn = msg.u.sync_reply.GetPlayerName();
  //      evt.u.connected.SetText(pn);
  //      QueueEvent(evt);

  //      _connected = true;
  //    }

  //    Utils.LogIt(LogCategories.SYNC, "%d round trips remaining", SyncState.roundtrips_remaining);
  //    if (--SyncState.roundtrips_remaining == 0)
  //    {
  //      var e = new UdpEvent(EEventType.Synchronized);
  //      QueueEvent(e);
  //      _current_state = EClientState.Running;
  //      _last_received_input.frame = -1;
  //      _remote_magic_number = msg.header.magic;
  //    }
  //    else
  //    {
  //      var evt = new UdpEvent(EEventType.Synchronizing);
  //      evt.u.synchronizing.total = SYNC_PACKETS_COUNT;
  //      evt.u.synchronizing.count = SYNC_PACKETS_COUNT - (int)SyncState.roundtrips_remaining;
  //      QueueEvent(evt);
  //      SendSyncRequest();
  //    }
  //    return true;
  //  }


  //}

}
