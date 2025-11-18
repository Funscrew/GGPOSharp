using System.Runtime.InteropServices;
using System.Net;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Net.Sockets;
using System.Threading;

namespace GGPOSharp;

delegate bool MsgHandler<T>(ref T msg, int msgLen);


// ================================================================================================================
public class GGPOEndpoint
{
  public const int SEND_QUEUE_SIZE = 64;

  public const int UDP_HEADER_SIZE = 28;                  /* Size of IP + UDP headers */
  public const int SYNC_PACKETS_COUNT = 5;
  public const int SYNC_RETRY_INTERVAL = 2000;
  public const int SYNC_FIRST_RETRY_INTERVAL = 500;
  public const int RUNNING_RETRY_INTERVAL = 200;
  public const int KEEP_ALIVE_INTERVAL = 200;
  public const int QUALITY_REPORT_INTERVAL = 1000;
  public const int NETWORK_STATS_INTERVAL = 1000;
  public const int UDP_SHUTDOWN_TIMER = 5000;
  public const int MAX_SEQ_DISTANCE = (1 << 15);



  // Network transmission information

  //Udp* _udp;
  //sockaddr_in _peer_addr;
  private UInt16 _magic_number;
  private int _queue;
  UInt16 _remote_magic_number;
  bool _connected;
  int _send_latency;
  int _oop_percent;

  // This is functionally the same as 'QueueEntry'
  //struct {
  //  int send_time;
  //  sockaddr_in dest_addr;
  //  UdpMsg* msg;
  //}
  //_oo_packet;
  private QueueEntry _oo_packet = new QueueEntry();

  private RingBuffer<QueueEntry> _send_queue = new RingBuffer<QueueEntry>(SEND_QUEUE_SIZE);

  /// <summary>
  /// Data used when we are syncing the clients.
  /// </summary>
  private SyncData SyncState = new SyncData();

  /// <summary>
  /// Data used when the client is in running state.
  /// </summary>
  private RunningData RunningState = new RunningData();

  /// <summary>
  /// Client that owns this endpoint.
  /// </summary>
  private GGPOClient Client = null!;

  private int LocalPort;
  private int RemotePort;
  private IPEndPoint RemoteIP;
  private EndPoint UseRemote;

  private GGPOEndpointOptions Options = null!;

  private Stopwatch Clock = Stopwatch.StartNew();

  private MsgHandler<UdpMsg>[] MsgHandlers = new MsgHandler<UdpMsg>[9];

  // Network Stats
  int _round_trip_time;
  int _packets_sent;
  int _bytes_sent;
  int _kbps_sent;
  int _stats_start_time;


  // NOTE: This needs to be passed in during initialization so that anything that uses the client can see the data....
  // OR!  We can just let them peer in and interrogate the client for the information! --> Probably a better plan!
  // NOTE: I'm not a super fan of how the local / peer connect statuses are represented....
  // There is a bit of a disconnect between the player index and the index of *_connect_status*
  ConnectStatus[] _local_connect_status = null!;
  ConnectStatus[] _peer_connect_status = new ConnectStatus[GGPOConsts.UDP_MSG_MAX_PLAYERS];
  // UdpMsg::connect_status _peer_connect_status[ProtoConsts.UDP_MSG_MAX_PLAYERS];

  public EClientState _current_state { get; private set; } = EClientState.Disconnected;

  /*
   * Fairness.
   */
  int _local_frame_advantage;
  int _remote_frame_advantage;

  // Packet Loss
  RingBuffer<GameInput> _pending_output = new RingBuffer<GameInput>(64);
  GameInput _last_received_input;
  GameInput _last_sent_input;
  GameInput _last_acked_input;

  private uint _last_send_time = 0;
  private uint _last_recv_time = 0;
  private uint _shutdown_timeout = 0;
  private bool _disconnect_event_sent = false;
  private uint _disconnect_timeout = 0;
  private uint _disconnect_notify_start = 0;
  private bool _disconnect_notify_sent = false;

  public UInt16 _next_send_seq = 0;
  public UInt16 _next_recv_seq = 0;

  TimeSync _timesync = null!;

  /*
   * Event queue
   */
  RingBuffer<Event> _event_queue = new RingBuffer<Event>(64);

  // Your name.  This will be exchanged with other peers on sync.
  string _playerName = null!; //new char[ProtoConsts.MAX_NAME_SIZE];


  // Buffer for receiving messages.  We use this one so we don't have to allocate bytes every frame.
  private byte[] ReceiveBuffer = new byte[8192];


  // -------------------------------------------------------------------------------------
  public GGPOEndpoint(GGPOClient client_, GGPOEndpointOptions ops_, ConnectStatus[] localConnectStatus_)
  {
    MsgHandlers[(byte)EMsgType.Invalid] = OnInvalid;
    MsgHandlers[(byte)EMsgType.SyncRequest] = OnSyncRequest;
    MsgHandlers[(byte)EMsgType.SyncReply] = OnSyncReply;
    MsgHandlers[(byte)EMsgType.Input] = OnInput;
    MsgHandlers[(byte)EMsgType.QualityReport] = OnQualityReport;
    MsgHandlers[(byte)EMsgType.QualityReply] = OnQualityReply;
    MsgHandlers[(byte)EMsgType.KeepAlive] = OnKeepAlive;
    MsgHandlers[(byte)EMsgType.InputAck] = OnInputAck;
    MsgHandlers[(byte)EMsgType.ChatCommand] = OnChat;

    Options = ops_;
    Client = client_;

    RemoteIP = new IPEndPoint(IPAddress.Parse(Options.RemoteHost), Options.RemotePort);
    UseRemote = RemoteIP;

    _last_sent_input.init(-1, null, 1);
    _last_received_input.init(-1, null, 1);
    _last_acked_input.init(-1, null, 1);

    // memset(&_state, 0, sizeof _state);
    SyncState = new SyncData();
    RunningState = new RunningData();

    _timesync = new TimeSync();

    // memset(_peer_connect_status, 0, sizeof(_peer_connect_status));
    //for (int i = 0; i < ARRAY_SIZE(_peer_connect_status); i++)
    //{
    //  _peer_connect_status[i].last_frame = -1;
    //}
    int len = _peer_connect_status.Length;
    for (int i = 0; i < len; i++)
    {
      _peer_connect_status[i] = new ConnectStatus();
      _peer_connect_status[i].last_frame = -1;
    }

    // PEER ADDRESS is set otherwise.
    //memset(&_peer_addr, 0, sizeof _peer_addr);

    //_oo_packet.msg = NULL;
    _oo_packet.MsgIndex = -1;

    // These are set in the options....
    //_send_latency = Platform::GetConfigInt(L"ggpo.network.delay");
    //_oop_percent = Platform::GetConfigInt(L"ggpo.oop.percent");

    // memset(_playerName, 0, MAX_NAME_SIZE);
    _playerName = Options.PlayerName; /**/; // Options.PlayerName;
    // _playerName.SetValue(Options.PlayerName);

    this._local_connect_status = localConnectStatus_;
    while (_magic_number == 0)
    {
      _magic_number = (UInt16)Random.Shared.Next();
    }

    // Begin the sync operation.....
    Synchronize();
  }

  // If we have an instance, we are initialized!
  internal bool IsInitialized() { return true; }
  internal bool IsSynchronized() { return _current_state == EClientState.Running; }
  internal bool IsRunning() { return _current_state == EClientState.Running; }

  // ----------------------------------------------------------------------------------------------------------
  internal bool GetEvent(ref Event e)
  {
    if (_event_queue.Size == 0)
    {
      return false;
    }
    e = _event_queue.Front();
    _event_queue.Pop();
    return true;
  }

  // ------------------------------------------------------------------------------------------------
  internal void Disconnect()
  {
    _current_state = EClientState.Disconnected;
    _shutdown_timeout = (uint)(Clock.ElapsedMilliseconds + UDP_SHUTDOWN_TIMER);
  }

  // -------------------------------------------------------------------------------------
  private unsafe bool OnChat(ref UdpMsg msg, int msgLen)
  {
    var evt = new Event(EEventType.ChatCommand);
    // evt.u.input.input = _last_received_input;
    //_last_received_input.desc(desc, ARRAY_SIZE(desc));

    //_state.running.last_input_packet_recv_time = Platform::GetCurrentTimeMS();
    int textlen = msgLen - 5; //sizeof(UdpMsg::header);
    fixed (sbyte* txtData = msg.u.chat.text)
    {
      evt.u.chat.SetText(txtData, textlen);
    }
    // strcpy_s(evt.u.chat.text, textlen + 1, msg->u.chat.text);

    //Log("Sending frame %d to emu queue %d (%s).\n", _last_received_input.frame, _queue, desc);
    QueueEvent(evt);


    return true;
  }

  // -------------------------------------------------------------------------------------
  private bool OnInputAck(ref UdpMsg msg, int msgLen)
  {
    // Get rid of our buffered input
    while (_pending_output.Size != 0 && _pending_output.Front().frame < msg.u.input_ack.ack_frame)
    {
      Utils.Log($"Throwing away pending output frame {_pending_output.Front().frame}");
      _last_acked_input = _pending_output.Front();
      _pending_output.Pop();
    }
    return true;
  }

  // -------------------------------------------------------------------------------------
  private bool OnKeepAlive(ref UdpMsg msg, int msgLen)
  {
    // Yep, we just say OK!
    return true;
  }

  // -------------------------------------------------------------------------------------
  private bool OnQualityReport(ref UdpMsg msg, int msgLen)
  {
    // send a reply so the other side can compute the round trip transmit time.
    UdpMsg reply = new UdpMsg(EMsgType.QualityReply);
    reply.u.quality_reply.pong = msg.u.quality_report.ping;
    SendMsg(ref reply);

    _remote_frame_advantage = msg.u.quality_report.frame_advantage;
    return true;
  }

  // -------------------------------------------------------------------------------------
  private bool OnQualityReply(ref UdpMsg msg, int msgLen)
  {
    _round_trip_time = (int)(Clock.ElapsedMilliseconds - msg.u.quality_reply.pong);
    return true;
  }

  // -------------------------------------------------------------------------------------
  private bool OnInput(ref UdpMsg msg, int msgLen)
  {
    /*
     * If a disconnect is requested, go ahead and disconnect now.
     */
    bool disconnect_requested = msg.u.input.disconnect_requested;
    if (disconnect_requested)
    {
      if (_current_state != EClientState.Disconnected && !_disconnect_event_sent)
      {
        Utils.Log("Disconnecting endpoint on remote request.\n");
        QueueEvent(new Event(EEventType.Disconnected));
        _disconnect_event_sent = true;
      }
    }
    else
    {
      /*
       * Update the peer connection status if this peer is still considered to be part
       * of the network.
       */
      //var remote_status = msg.u.input.peer_connect_status;
      for (int i = 0; i < _peer_connect_status.Length; i++)
      {
        var remote_status = msg.u.input.GetPeerConnectStatus(i);
        Utils.ASSERT(remote_status.last_frame >= _peer_connect_status[i].last_frame);

        _peer_connect_status[i].disconnected = _peer_connect_status[i].disconnected || remote_status.disconnected;
        _peer_connect_status[i].last_frame = Math.Max(_peer_connect_status[i].last_frame, remote_status.last_frame);
      }
    }

    /*
     * Decompress the input.
     */
    int last_received_frame_number = _last_received_input.frame;
    unsafe
    {
      if (msg.u.input.num_bits > 0)
      {
        int offset = 0;
        fixed (byte* bits = msg.u.input.bits)
        {

          int numBits = msg.u.input.num_bits;
          int currentFrame = (int)msg.u.input.start_frame;

          _last_received_input.size = msg.u.input.input_size;
          if (_last_received_input.frame < 0)
          {
            _last_received_input.frame = (int)msg.u.input.start_frame - 1;
          }
          while (offset < numBits)
          {
            /*
             * Keep walking through the frames (parsing bits) until we reach
             * the inputs for the frame right after the one we're on.
             */
            if (currentFrame > (_last_received_input.frame + 1))
            {
              throw new InvalidOperationException("invalid frame number!");
            }
            bool useInputs = currentFrame == _last_received_input.frame + 1;

            while (BitVector.ReadBit(bits, ref offset) != 0)
            {
              bool on = BitVector.ReadBit(bits, ref offset) == 1;
              int button = BitVector.ReadNibblet(bits, ref offset);
              if (useInputs)
              {
                if (on)
                {
                  _last_received_input.set(button);
                }
                else
                {
                  _last_received_input.clear(button);
                }
              }
            }
            Utils.ASSERT(offset <= numBits);

            /*
             * Now if we want to use these inputs, go ahead and send them to
             * the emulator.
             */
            if (useInputs)
            {
              /*
               * Move forward 1 frame in the stream.
               */
              Utils.ASSERT(currentFrame == _last_received_input.frame + 1);
              _last_received_input.frame = currentFrame;

              /*
               * Send the event to the emualtor
               */
              // UdpProtocol::Event evt(UdpProtocol::Event::Input);
              var evt = new Event(EEventType.Input);
              evt.u.input = _last_received_input;

              // NOTE: This is expensive loggin that we are not going to support in the C# version.  It
              // should be replaced with something better tho.
              string desc = "NOT SUPPORTED - REWRITE";
              //const int DESC_SIZE = 1024;
              //byte[] desc = new byte[1024];
              //_last_received_input.desc(desc, DESC_SIZE);

              RunningState.last_input_packet_recv_time = (uint)Clock.ElapsedMilliseconds;

              Utils.Log($"Sending frame {_last_received_input.frame} to emu queue {_queue} (<{desc}>).");
              QueueEvent(evt);

            }
            else
            {
              Utils.Log($"Skipping past frame:({currentFrame}) current is {_last_received_input}.");
            }

            /*
             * Move forward 1 frame in the input stream.
             */
            currentFrame++;
          }
        }
      }
    }

    Utils.ASSERT(_last_received_input.frame >= last_received_frame_number);

    /*
     * Get rid of our buffered input
     */
    while (_pending_output.Size > 0 && _pending_output.Front().frame < msg.u.input.ack_frame)
    {
      Utils.Log($"Throwing away pending output frame {_pending_output.Front().frame}");
      _last_acked_input = _pending_output.Front();
      _pending_output.Pop();
    }
    return true;
  }

  // -------------------------------------------------------------------------------------
  internal int RecommendFrameDelay()
  {
    // XXX: require idle input should be a configuration parameter
    return _timesync.recommend_frame_wait_duration(false);
  }



  // -------------------------------------------------------------------------------------
  /// <summary>
  /// Begin the synchronize operation.  All clients need to be synced first.
  /// </summary>
  public void Synchronize()
  {
    if (_current_state != EClientState.Disconnected)
    {
      throw new InvalidOperationException("Invalid state to begin synchronize operations.");
    }

    _current_state = EClientState.Syncing;
    SyncState.roundtrips_remaining = SYNC_PACKETS_COUNT;
    SendSyncRequest();
  }

  // -------------------------------------------------------------------------------------
  private void SendSyncRequest()
  {
    SyncState.random = (UInt32)(Random.Shared.Next() & 0xFFFF);

    var msg = new UdpMsg(EMsgType.SyncRequest);
    msg.u.sync_request.random_request = SyncState.random;
    SendMsg(ref msg);

    // throw new NotImplementedException();
  }

  // -------------------------------------------------------------------------------------
  public unsafe void SendMsg(ref UdpMsg msg)
  {
    Utils.Log("send", ref msg);

    _packets_sent++;
    _last_send_time = (uint)Clock.ElapsedMilliseconds;
    _bytes_sent += msg.PacketSize();

    msg.header.magic = _magic_number;
    msg.header.sequence_number = _next_send_seq++;


    _send_queue.Push(new QueueEntry()
    {
      queue_time = (int)Clock.ElapsedMilliseconds,
      dest_addr = this.RemoteIP,

      // NOTE: This is a BIG copy, so we will find a different way to handle it in the future.
      // probably index into a fixed size array.
      msg = msg,
    });
    PumpSendQueue();

  }

  // -------------------------------------------------------------------------------------
  // NOTE: This is like 'OnLoopPoll' from the C++ version....
  public void RunFrame()
  {
    OnLoopPoll();
  }

  // ----------------------------------------------------------------------------------------------------------
  internal void SendInput(ref GameInput input)
  {
    if (_current_state == EClientState.Running)
    {
      /*
       * Check to see if this is a good time to adjust for the rift...
       */
      _timesync.rollback_frame(ref input, _local_frame_advantage, _remote_frame_advantage);

      /*
       * Save this input packet
       *
       * XXX: This queue may fill up for spectators who do not ack input packets in a timely
       * manner.  When this happens, we can either resize the queue (ug) or disconnect them
       * (better, but still ug).  For the meantime, make this queue really big to decrease
       * the odds of this happening...
       */
      _pending_output.Push(input);
    }
    SendPendingOutput();
  }

  // -------------------------------------------------------------------------------------
  // REFACTOR: Rename to 'DoPoll' or 'Poll' or whatever.
  public void OnLoopPoll()
  {
    if (Options.IsLocal) { return; }

    // Receive messages here!
    ReceiveMessages();

    PumpSendQueue();

    int next_interval = 0;
    int now = (int)this.Clock.ElapsedMilliseconds;

    switch (_current_state)
    {
      //case EClientState.Disconnected:
      //  break;

      case EClientState.Syncing:
        // do sync timeout + resend stuff here....
        next_interval = (SyncState.roundtrips_remaining == SYNC_PACKETS_COUNT) ? SYNC_FIRST_RETRY_INTERVAL : SYNC_RETRY_INTERVAL;
        if (_last_send_time > 0 && _last_send_time + next_interval < now)
        {
          Utils.Log($"No luck syncing after {next_interval} ms... Re-queueing sync packet.");
          SendSyncRequest();
        }
        break;

      //// NOTE: This state never gets tripped!
      //case EClientState.Synchronzied:
      //  int x = 10;
      //  break;

      case EClientState.Running:

        // xxx: rig all this up with a timer wrapper
        if (RunningState.last_input_packet_recv_time == 0 || RunningState.last_input_packet_recv_time + RUNNING_RETRY_INTERVAL < now)
        {
          Utils.Log($"Haven't exchanged packets in a while (last received:{_last_received_input.frame}  last sent:{_last_sent_input.frame}).  Resending.");
          SendPendingOutput();
          RunningState.last_input_packet_recv_time = (uint)now;
        }

        if (RunningState.last_quality_report_time == 0 || RunningState.last_quality_report_time + QUALITY_REPORT_INTERVAL < now)
        {
          UdpMsg msg = new UdpMsg(EMsgType.QualityReport);
          msg.u.quality_report.ping = (uint)Clock.ElapsedMilliseconds;
          msg.u.quality_report.frame_advantage = (sbyte)_local_frame_advantage;
          SendMsg(ref msg);
          RunningState.last_quality_report_time = (uint)now;
        }

        if (RunningState.last_network_stats_interval == 0 || RunningState.last_network_stats_interval + NETWORK_STATS_INTERVAL < now)
        {
          UpdateNetworkStats();
          RunningState.last_network_stats_interval = (uint)now;
        }

        if (_last_send_time != 0 && _last_send_time + KEEP_ALIVE_INTERVAL < now)
        {
          Utils.Log("Sending keep alive packet\n");

          // NOTE : Check this for memory... 
          var msg = new UdpMsg(EMsgType.KeepAlive);
          SendMsg(ref msg);
        }

        if (_disconnect_timeout != 0 && _disconnect_notify_start != 0 &&
          !_disconnect_notify_sent && (_last_recv_time + _disconnect_notify_start < now))
        {
          Utils.Log($"Endpoint has stopped receiving packets for {_disconnect_notify_start} ms.  Sending notification.");
          Event e = new Event(EEventType.NetworkInterrupted);
          e.u.network_interrupted.disconnect_timeout = (int)(_disconnect_timeout - _disconnect_notify_start);
          QueueEvent(e);
          _disconnect_notify_sent = true;
        }

        if (_disconnect_timeout != 0 && (_last_recv_time + _disconnect_timeout < now))
        {
          if (!_disconnect_event_sent)
          {
            Utils.Log($"Endpoint has stopped receiving packets for {_disconnect_timeout} ms.  Disconnecting.");
            QueueEvent(new Event(EEventType.Disconnected));
            _disconnect_event_sent = true;
          }
        }

        break;


      default:
        throw new InvalidOperationException($"Invalid current state: {_current_state}");
    }
  }

  // ------------------------------------------------------------------------
  private void UpdateNetworkStats()
  {
    int now = (int)Clock.ElapsedMilliseconds;

    if (_stats_start_time == 0)
    {
      _stats_start_time = now;
    }

    int total_bytes_sent = _bytes_sent + (UDP_HEADER_SIZE * _packets_sent);
    float seconds = (float)((now - _stats_start_time) / 1000.0);
    float bytes_sec = total_bytes_sent / seconds;
    float udp_overhead = (float)(100.0 * (UDP_HEADER_SIZE * _packets_sent) / _bytes_sent);

    _kbps_sent = (int)(bytes_sec / 1024);

    // NOTE: This might be a good place to write some stats..?
    var pps = (float)_packets_sent * 1000 / (now - _stats_start_time);
    var totalKbs = total_bytes_sent / 1024.0;

    Utils.Log($"Network Stats -- Bandwidth: {_kbps_sent:f2} KBps   Packets Sent: {_packets_sent} ({pps:f2} pps) KB Sent: {totalKbs:f2}    UDP Overhead: {udp_overhead:f2} pct.");
    //_kbps_sent,
    //_packets_sent,
    //(float)_packets_sent * 1000 / (now - _stats_start_time),
    //total_bytes_sent / 1024.0,
    //udp_overhead);
  }

  // ------------------------------------------------------------------------
  // REFACTOR:  This is more like 'SendPendingLocalInputs'
  private unsafe void SendPendingOutput()
  {
    UdpMsg msg = new UdpMsg(EMsgType.Input);
    int i = 0;
    int j = 0;
    int offset = 0;

    GameInput last;


    if (_pending_output.Size != 0)
    {
      byte* bits = msg.u.input.bits;

      last = _last_acked_input;

      msg.u.input.start_frame = (uint)_pending_output.Front().frame;
      msg.u.input.input_size = (byte)_pending_output.Front().size;

      Utils.ASSERT(last.frame == -1 || last.frame + 1 == msg.u.input.start_frame);

      // TODO: Review this.  Seems like a lot of extra work to save a byte or two, maybe....
      // Who knows tho.....
      for (j = 0; j < _pending_output.Size; j++)
      {
        // TODO: This is a copy of the data.... We may want to fix that....
        GameInput current = _pending_output[j]; // .Item(j);

        // Only update the message if the data is different.
        // if (memcmp(current.bits, last.bits, current.size) != 0)
        if (!Utils.MemMatches(current.data, last.data, current.size))
        {

          // This assert is checking consts.  Probably don't need to do this each time....
          Utils.ASSERT((GameInput.GAMEINPUT_MAX_BYTES * GameInput.GAMEINPUT_MAX_PLAYERS * 8) < (1 << BitVector.BITVECTOR_NIBBLE_SIZE));

          for (i = 0; i < current.size * 8; i++)
          {
            Utils.ASSERT(i < (1 << BitVector.BITVECTOR_NIBBLE_SIZE));

            if (current.value(i) != last.value(i))
            {
              BitVector.SetBit(msg.u.input.bits, ref offset);
              // (current.value(i) ? BitVector.SetBit : BitVector.ClearBit)(bits, &offset);
              if (current.value(i))
              {
                BitVector.SetBit(bits, ref offset);
              }
              else
              {
                BitVector.ClearBit(bits, ref offset);
              }
              BitVector.WriteNibblet(bits, i, ref offset);
            }
          }
        }
        BitVector.ClearBit(msg.u.input.bits, ref offset);
        last = _last_sent_input = current;
      }
    }
    else
    {
      msg.u.input.start_frame = 0;
      msg.u.input.input_size = 0;
    }
    msg.u.input.ack_frame = _last_received_input.frame;
    msg.u.input.num_bits = (UInt16)offset;

    msg.u.input.disconnect_requested = _current_state == EClientState.Disconnected;
    // NOTE: The C++ sets this pointer for p2p, but not spectators.
    // I think that if we spectate here we could just set some other flag.....
    // Let's just proceed like we always have this data for now....
    //    if (_local_connect_status)  
    //    {
    for (int pci = 0; i < GGPOConsts.UDP_MSG_MAX_PLAYERS; i++)
    {
      msg.u.input.SetPeerConnectStatus(pci, _local_connect_status[pci]);
    }
    // memcpy(msg.u.input.peer_connect_status, _local_connect_status, sizeof(UdpMsg::connect_status) * UDP_MSG_MAX_PLAYERS);
    //    }
    //else
    //{
    //  memset(msg.u.input.peer_connect_status, 0, sizeof(UdpMsg::connect_status) * UDP_MSG_MAX_PLAYERS);
    //}

    Utils.ASSERT(offset < GGPOConsts.MAX_COMPRESSED_BITS);

    SendMsg(ref msg);
  }

  // ------------------------------------------------------------------------
  private void ReceiveMessages()
  {
    // Pull in all messages, while they are available.
    //while (Client.Available > 0)
    //{
    while (true)
    {
      // Get the next message.....
      // byte[] data = Client.Receive
      int received = Client.UdpClient.Receive(ReceiveBuffer, ref UseRemote);
      if (received == 0)
      {
        break;
      }

      UdpMsg msg = new UdpMsg();
      UdpMsg.FromBytes(ReceiveBuffer, ref msg, received);

      // Logging?
      Utils.Log("MSG", ref msg);

      // Now that we have the message we can do something with it....
      HandleMessage(ref msg, received);
    }
  }


  // ------------------------------------------------------------------------
  private void HandleMessage(ref UdpMsg msg, int msgLen)
  {
    var handler = this.MsgHandlers[(int)msg.header.type];
    handler(ref msg, msgLen);
  }

  // ----------------------------------------------------------------------------------------------------------
  internal unsafe bool GetPeerConnectStatus(int id, int* frame)
  {
    *frame = _peer_connect_status[id].last_frame;
    return !_peer_connect_status[id].disconnected;
  }

  // ------------------------------------------------------------------------
  internal void SetLocalFrameNumber(int localFrame)
  {
    /*
     * Estimate which frame the other guy is one by looking at the
     * last frame they gave us plus some delta for the one-way packet
     * trip time.
     */
    int remoteFrame = _last_received_input.frame + (_round_trip_time * 60 / 1000);

    /*
     * Our frame advantage is how many frames *behind* the other guy
     * we are.  Counter-intuative, I know.  It's an advantage because
     * it means they'll have to predict more often and our moves will
     * pop more frequenetly.
     */
    _local_frame_advantage = remoteFrame - localFrame;
  }


  // ------------------------------------------------------------------------
  private bool OnInvalid(ref UdpMsg msg, int msgLen)
  {
    throw new GGPOException("Invalid message!");
  }

  // ------------------------------------------------------------------------
  private bool OnSyncRequest(ref UdpMsg msg, int msgLen)
  {
    if (_remote_magic_number != 0 && msg.header.magic != _remote_magic_number)
    {
      Utils.Log($"Ignoring sync request from unknown endpoint ({msg.header.magic} != {_remote_magic_number}).");
      return false;
    }
    UdpMsg reply = new UdpMsg(EMsgType.SyncReply);
    reply.u.sync_reply.random_reply = msg.u.sync_request.random_request;

    reply.u.sync_reply.SetPlayerName(_playerName);
    //      strcpy_s(reply.u.sync_reply.playerName, _playerName);

    SendMsg(ref reply);
    return true;


    //throw new GGPOException("sync request!!");
  }

  // ------------------------------------------------------------------------
  private bool OnSyncReply(ref UdpMsg msg, int msgLen)
  {
    if (_current_state != EClientState.Syncing)
    {
      Utils.Log("Ignoring SyncReply while not synching.");
      return msg.header.magic == _remote_magic_number;
    }

    if (msg.u.sync_reply.random_reply != SyncState.random)
    {
      Utils.Log($"sync reply {msg.u.sync_reply.random_reply} != {SyncState.random}.  Keep looking...");
      return false;
    }

    if (!_connected)
    {
      var evt = new Event(EEventType.Connected);

      // TODO: A direct copy from sbyte in the future!
      string pn = msg.u.sync_reply.GetPlayerName();
      evt.u.connected.SetText(pn);
      // strcpy_s(evt.u.connected.playerName, msg.u.sync_reply.playerName);
      QueueEvent(evt);

      _connected = true;
    }

    Utils.Log($"Checking sync state ({SyncState.roundtrips_remaining} round trips remaining).");
    if (--SyncState.roundtrips_remaining == 0)
    {
      Utils.Log("Synchronized!");

      var e = new Event(EEventType.Synchronized);
      QueueEvent(e);
      _current_state = EClientState.Running;
      _last_received_input.frame = -1;
      _remote_magic_number = msg.header.magic;
    }
    else
    {
      var evt = new Event(EEventType.Synchronizing);
      evt.u.synchronizing.total = SYNC_PACKETS_COUNT;
      evt.u.synchronizing.count = SYNC_PACKETS_COUNT - (int)SyncState.roundtrips_remaining;
      QueueEvent(evt);
      SendSyncRequest();
    }
    return true;
  }

  // ------------------------------------------------------------------------
  private void QueueEvent(in Event evt)
  {
    Utils.LogEvent("Queuing event", evt);
    _event_queue.Push(evt);
  }


  // ------------------------------------------------------------------------
  /// <summary>
  /// Send the outgoing messages in the queue.
  /// </summary>
  private void PumpSendQueue()
  {
    // Ported from C++:
    while (!_send_queue.IsEmpty)
    {
      QueueEntry entry = _send_queue.Front();

      // NOTE: If latency is set, then messages may not be sent this time around.
      // Like that of '_oop_percent' (below) this is probably useful for testing scenarios
      // to simulate jitter and out of order packets.
      if (_send_latency != 0)
      {
        // should really come up with a gaussian distribution based on the configured
        // value, but this will do for now.
        int jitter = (_send_latency * 2 / 3) + (Random.Shared.Next(_send_latency) / 3);
        if ((int)Clock.ElapsedMilliseconds < _send_queue.Front().queue_time + jitter)
        {
          break;
        }
      }

      // NOTE: I believe that the purpose of this is for simulating out of order packets in
      // test scenarios.  In this case, the current entry is set as the OO packet which will
      // get sent at a later time (see below ~ line 823)
      if (_oop_percent != 0 && !_oo_packet.HasMessage && (Random.Shared.Next(100) < _oop_percent))
      {
        int delay = Random.Shared.Next(_send_latency * 10 + 1000);
        Utils.Log($"creating rogue oop (seq: {entry.msg.header.sequence_number}  delay: {delay})");
        _oo_packet.queue_time = (int)Clock.ElapsedMilliseconds + delay;
        _oo_packet.msg = entry.msg;
        _oo_packet.dest_addr = entry.dest_addr;
      }
      else
      {
        // Make sure that there is a valid address to send to!
        if (RemoteIP == null)
        {
          throw new Exception("There is no remote address!");
        }
        // ASSERT(entry.dest_addr.sin_addr.s_addr);

        // Send the packet!
        //_udp.SendTo((char*)entry.msg, packetSize, 0,
        //  (struct sockaddr*)&entry.dest_addr, sizeof entry.dest_addr);
        SendMsgPacket(entry.msg);
        //int packetSize = entry.msg.PacketSize();
        //byte[] toSend = new byte[4096];
        //UdpMsg.ToBytes(entry.msg, toSend, packetSize);
        //Client.Send(toSend, packetSize);

        //            // TODO: set the message index to something invalid.
        //      // delete entry.msg;
        //  }
        _send_queue.Pop();
      }

      if (_oo_packet.HasMessage && _oo_packet.queue_time < (int)Clock.ElapsedMilliseconds)
      {
        Utils.Log("sending rogue oop!");

        SendMsgPacket(_oo_packet.msg);
        _oo_packet.MsgIndex = -1;
        // int packetSize = _oo_packet.msg.PacketSize();
        //_udp.SendTo((char*)_oo_packet.msg, packetSize, 0,
        //  (struct sockaddr*)&_oo_packet.dest_addr, sizeof _oo_packet.dest_addr);

        //delete _oo_packet.msg;
        //_oo_packet.msg = NULL;
      }
    }

  }

  // ------------------------------------------------------------------------
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  private void SendMsgPacket(in UdpMsg msg)
  {
    int packetSize = msg.PacketSize();

    // NOTE: This should be at class level so we don't make too much garbage...
    // Or we could use a span?
    byte[] toSend = new byte[4200];
    UdpMsg.ToBytes(msg, toSend, packetSize);
    // Client.Send(toSend, packetSize);


    // Client.Send(toSend, packetSize, Remote);
    Client.UdpClient.Send(toSend, packetSize, ref UseRemote);
  }


}


// ================================================================================================================
public class QueueEntry
{
  public int queue_time;
  public EndPoint dest_addr;

  // NOTE: We can't really have a pointer here as the originating object will disappear!
  // Maybe I need to have a copy of the byte array instead?  Maybe index into some array where these are
  // created....?
  // public UdpMsg* msg;

  // ARG!
  // This is a whole copy of a message, which is pretty big, so we
  // will have to come up with another way to do this... I'm thinking
  // we should have an array of messages that we can index into...
  // NOTE: At time of writing, UdpMsg size is 4144 bytes!
  public UdpMsg msg;

  public int MsgIndex = -1;
  public bool HasMessage { get { return MsgIndex != -1; } }
}

// ================================================================================================================
public struct SyncData
{
  public UInt32 roundtrips_remaining;
  public UInt32 random;
}

// ================================================================================================================
public struct RunningData
{
  public UInt32 last_quality_report_time;
  public UInt32 last_network_stats_interval;
  public UInt32 last_input_packet_recv_time;
}

// ================================================================================================================
public enum EClientState
{
  Invalid = 0,
  Syncing,
  Synchronzied,
  Running,
  Disconnected
};



// ========================================================================================================
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct GameInput
{
  public const UInt16 GAMEINPUT_MAX_BYTES = 7;
  public const UInt16 GAMEINPUT_MAX_PLAYERS = 4;    // NOTE: This probably need to be 2?
  public const int NULL_FRAME = -1;


  public int frame;
  public int size; /* size in bytes of the entire input for all players */

  private const int BITS_SIZE = GAMEINPUT_MAX_BYTES * GAMEINPUT_MAX_PLAYERS;
  public fixed byte data[BITS_SIZE];

  // ------------------------------------------------------------------------------------------
  public GameInput() { }

  public unsafe void Clear()
  {
    frame = 0;
    size = 0;
    // NOTE: memcpy or something else is probably better here...
    for (int i = 0; i < BITS_SIZE; i++)
    {
      data[i] = 0;
    }
  }

  public bool is_null() { return frame == NULL_FRAME; }

  // ------------------------------------------------------------------------------------------
  public void init(int iframe, byte[] ibits, int isize, int offset)
  {
    Utils.ASSERT(isize < GAMEINPUT_MAX_BYTES);

    frame = iframe;
    size = isize;

    // TODO: We could probably come up with a better way to copy this data...
    for (int i = 0; i < size; i++)
    {
      data[i] = 0;
    }
    if (ibits != null)
    {
      for (int i = 0; i < size; i++)
      {
        if (i < size)
        {
          data[i + offset] = ibits[i];
        }
      }
    }

    // C++ style!
    //frame = iframe;
    //size = isize;
    //memset(bits, 0, sizeof(bits));
    //if (ibits)
    //{
    //  memcpy(bits + (offset * isize), ibits, isize);
    //}

  }

  // ------------------------------------------------------------------------------------------
  public void init(int iframe, byte[] ibits, int isize)
  {
    init(iframe, ibits, isize, 0);
  }

  // ----------------------------------------------------------------------------------------
  public bool value(int i)
  {
    return (data[i / 8] & (1 << (i % 8))) != 0;
  }

  // ----------------------------------------------------------------------------------------
  public void set(int i)
  {
    data[i / 8] |= (byte)(1 << (i % 8));
  }

  // ----------------------------------------------------------------------------------------
  public void clear(int i)
  {
    data[i / 8] &= (byte)~(1 << (i % 8));
  }

  // ----------------------------------------------------------------------------------------
  public unsafe void erase()
  {
    fixed (byte* pBits = data)
    {
      Unsafe.InitBlock(pBits, 0, BITS_SIZE);
    }
  }

  // ----------------------------------------------------------------------------------------
  public void desc(byte[] buf, int buf_size, bool show_frame = true)
  {
    // NOTE: I am not porting this as it is just some expensive logging messages
    // that can be handled in a better way, both in C++ and here.
    //Debug.WriteLine
    //// NOTE: The C++ version of this code sort of 
    //Utils.ASSERT(size > 0);   // Not sure why size would ever be zero... ?
    //int remaining = buf_size;
    //if (show_frame)
    //{
    //  remaining -= sprintf_s(buf, buf_size, $"(frame:{frame} size:{size} ");
    //}
    //else
    //{
    //  remaining -= sprintf_s(buf, buf_size, "(size:%d ", size);
    //}

    //for (int i = 0; i < size * 8; i++)
    //{
    //  char[] buf2 = new char[16];
    //  if (value(i))
    //  {
    //    int c = sprintf_s(buf2, ARRAY_SIZE(buf2), "%2d ", i);
    //    strncat_s(buf, remaining, buf2, ARRAY_SIZE(buf2));
    //    remaining -= c;
    //  }
    //}
    //strncat_s(buf, remaining, ")", 1);
  }

  // ----------------------------------------------------------------------------------------
  public bool equal(in GameInput other)
  {
    bool bitsonly = true;
    if (!bitsonly && frame != other.frame)
    {
      Utils.Log($"frames don't match: {frame}, {other.frame}");
    }
    if (size != other.size)
    {
      Utils.Log($"sizes don't match: {size}, {other.size}");
    }

    bool memMatch = false;
    fixed (byte* p = data)
    fixed (byte* p2 = other.data)
    {
      memMatch = Utils.MemMatches(p, p2, size);
    }

    if (!memMatch)
    {
      Utils.Log("bits don't match");
    }

    // NOTE: Because of this assert, we don't need to check the condition: size==other.size
    Utils.ASSERT(size != 0 && other.size != 0);
    return (bitsonly || frame == other.frame) &&
           size == other.size &&
           memMatch;
  }

}


// ==================================================================================================================
public struct UdpStats
{
  public int bytes_sent;
  public int packets_sent;
  public float kbps_sent;
};

// ==================================================================================================================
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Stats
{
  public int Ping;
  public int RemoteFrameAdvantage;
  public int LocalFrameAdvantage;
  public int SendQueueLen;
  public UdpStats Udp; // whatever your Udp::Stats mapped to
}

// ==================================================================================================================
public enum EEventType
{
  Unknown = -1,
  Connected,
  Synchronizing,
  Synchronized,
  Input,
  Disconnected,
  NetworkInterrupted,
  NetworkResumed,
  ChatCommand
}

// ================================================================================================
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct Event
{

  // ----------------------------------------------------------------------------------------------
  public Event() { }

  // ----------------------------------------------------------------------------------------------
  public Event(EEventType eventType_)
  {
    type = eventType_;
  }

  public EEventType type;

  // REFACTOR: Find a better name for this + sync with C++ code ('data' will do the trick!)
  public EventData u;


  [StructLayout(LayoutKind.Explicit)]
  public unsafe struct EventData
  {
    [FieldOffset(0)]
    public GameInput input;

    [FieldOffset(0)]
    public SyncData synchronizing;

    [FieldOffset(0)]
    public PlayerConnectData connected;

    [FieldOffset(0)]
    public NetworkInterruptedData network_interrupted;

    [FieldOffset(0)]
    public Chat chat;
  }

  public struct SyncData
  {
    public int total;
    public int count;
  }

  public struct NetworkInterruptedData
  {
    public int disconnect_timeout;
  }
}


// ================================================================================================================
/// <summary>
/// This is used for remote / other connections that aren't the local player.
/// </summary>
public class GGPOEndpointOptions
{
  /// <summary>
  /// Is this endpoint a local player?
  /// </summary>
  public bool IsLocal { get; set; } = false;

  /// <summary>
  /// Index of the player that this endpoint represents.
  /// </summary>
  public int PlayerIndex { get; set; } = -1;

  /// <summary>
  /// Name of the player.  This is only used for local players.
  /// </summary>
  public string? PlayerName { get; set; } = null;

  public string RemoteHost { get; set; } = Defaults.REMOTE_HOST;
  public int RemotePort { get; set; } = Defaults.REMOTE_PORT;

  // NOTE: Other player names come from the network!
  // public string PlayerName { get; set; } = default!;

  /// <summary>
  /// These should only be set in scenarios where you want to simulate certain network conditions.
  /// </summary>
  public TestOptions TestOptions { get; set; } = new TestOptions();
}


