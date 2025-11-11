using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

// using MsgHandler = System.Action<GGPOSharp.UdpMsg, int>;
using System;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.CompilerServices;

delegate bool MsgHandler<T>(ref T msg, int msgLen);

namespace GGPOSharp
{

  // ==================================================================================================================
  public struct UdpStats
  {
    public int bytes_sent;
    public int packets_sent;
    public float kbps_sent;
  };


  //// ==================================================================================================================
  //struct Stats
  //{
  //  int ping;
  //  int remote_frame_advantage;
  //  int local_frame_advantage;
  //  int send_queue_len;
  //  Udp::Stats udp;
  //};
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public struct Stats
  {
    public int Ping;
    public int RemoteFrameAdvantage;
    public int LocalFrameAdvantage;
    public int SendQueueLen;
    public UdpStats Udp; // whatever your Udp::Stats mapped to
  }


  //  // ==================================================================================================================
  //  struct Event
  //  {
  //    enum Type
  //    {
  //      Unknown = -1,
  //      Connected,
  //      Synchronizing,
  //      Synchronized,
  //      Input,
  //      Disconnected,
  //      NetworkInterrupted,
  //      NetworkResumed,
  //      ChatCommand
  //    };

  //    Type type;
  //    union {
  //      struct {
  //      GameInput input;
  //    }
  //    input;

  //      struct {
  //      int total;
  //      int count;
  //    }
  //    synchronizing;

  //      struct {
  //      char playerName[MAX_NAME_SIZE];
  //    }
  //    connected;

  //      struct {
  //      int disconnect_timeout;
  //    }
  //    network_interrupted;

  //      struct {
  //      char text[MAX_GGPOCHAT_SIZE + 1];
  //    }
  //    chat;

  //    }
  //  u;			// REFACTOR: Rename this to 'data'

  //    UdpProtocol::Event(Type t = Unknown) : type(t) { }
  //};
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
      public ConnectData connected;

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
  public class GGPOClientOptions
  {
    public const int LOCAL_PORT = 7001;
    public const int REMOTE_PORT = 7000;

    public int LocalPort { get; set; } = LOCAL_PORT;
    public string RemoteAddress { get; set; } = "127.0.0.1";
    public int RemotePort { get; set; } = REMOTE_PORT;

    /// <summary>
    /// These should only be set in scenarios where you want to simulate certain network conditions.
    /// </summary>
    public TestOptions TestOptions { get; set; } = new TestOptions();
  }

  // ================================================================================================================
  public class TestOptions
  {
    /// <summary>
    /// Use this to simulate latency / jitter when sending packets. (in ms)
    /// </summary>
    public int SendLatency { get; set; } = 0;

    /// <summary>
    /// A certain % of packets will be sent based on this option.
    /// </summary>
    public float OOPercent { get; set; } = 0.0f;
  }


  // ================================================================================================================
  public class GGPOClient
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


    public EClientState _current_state { get; private set; } = EClientState.Disconnected;

    /*
  * Network transmission information
  */
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
    private RunningData RunningData = new RunningData();


    private UdpBlaster Client = null!;

    private int LocalPort;
    private int RemotePort;
    private IPEndPoint RemoteIP;
    private EndPoint UseRemote;

    private GGPOClientOptions Options = null!;

    private Stopwatch Clock = Stopwatch.StartNew();

    private MsgHandler<UdpMsg>[] MsgHandlers = new MsgHandler<UdpMsg>[8];

    // Network Stats
    int _round_trip_time;
    int _packets_sent;
    int _bytes_sent;
    int _kbps_sent;
    int _stats_start_time;


    // Packet Loss
    RingBuffer<GameInput> _pending_output = new RingBuffer<GameInput>(64);
    GameInput _last_received_input;
    GameInput _last_sent_input;
    GameInput _last_acked_input;

    private uint _last_send_time = 0;
    private uint _last_recv_time = 0;
    public uint _shutdown_timeout = 0;
    public uint _disconnect_event_sent = 0;
    public uint _disconnect_timeout = 0;
    public uint _disconnect_notify_start = 0;
    bool _disconnect_notify_sent = false;

    public UInt16 _next_send_seq = 0;
    public UInt16 _next_recv_seq = 0;

    TimeSync _timesync;

    /*
     * Event queue
     */
    RingBuffer<Event> _event_queue = new RingBuffer<Event>(64);

    // Your name.  This will be exchanged with other peers on sync.
    char[] _playerName = new char[ProtoConsts.MAX_NAME_SIZE];


    // -------------------------------------------------------------------------------------
    public GGPOClient(GGPOClientOptions ops_)
    {
      MsgHandlers[(byte)EMsgType.Invalid] = OnInvalid;
      MsgHandlers[(byte)EMsgType.SyncRequest] = OnSyncRequest;
      MsgHandlers[(byte)EMsgType.SyncReply] = OnSyncReply;
      //MsgHandlers[(byte)EMsgType.Invalid] = OnInvalid;
      //MsgHandlers[(byte)EMsgType.Invalid] = OnInvalid;
      //MsgHandlers[(byte)EMsgType.Invalid] = OnInvalid;

      Options = ops_;
      Client = new UdpBlaster(Options.LocalPort);
      RemoteIP = new IPEndPoint(IPAddress.Parse(Options.RemoteAddress), Options.RemotePort);
      UseRemote = RemoteIP;

      // We can't send packets if we don't connect!
      // Client.Connect(Remote);

      // Begin the sync operation.....
      Synchronize();
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
      Log("send", ref msg);

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
      DoPoll();
      HandleEvents();
    }

    // -------------------------------------------------------------------------------------
    public void HandleEvents()
    {
    }

    // -------------------------------------------------------------------------------------
    public void DoPoll()
    {
      // Receive messages here!
      ReceiveMessages();

      PumpSendQueue();

      int next_interval = 0;
      int now = (int)this.Clock.ElapsedMilliseconds;

      switch (_current_state)
      {
        case EClientState.Disconnected:
          break;

        case EClientState.Syncing:
          // do sync timeout + resend stuff here....
          next_interval = (SyncState.roundtrips_remaining == SYNC_PACKETS_COUNT) ? SYNC_FIRST_RETRY_INTERVAL : SYNC_RETRY_INTERVAL;
          if (_last_send_time > 0 && _last_send_time + next_interval < now)
          {
            Log($"No luck syncing after {next_interval} ms... Re-queueing sync packet.");
            SendSyncRequest();
          }
          break;

        case EClientState.Synchronzied:
          int x = 10;
          break;

        default:
          throw new InvalidOperationException($"Invalid current state: {_current_state}");
      }
    }

    private byte[] ReceiveBuffer = new byte[8192];

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
        int received = Client.Receive(ReceiveBuffer, ref UseRemote);
        if (received == 0)
        {
          break;
        }

        UdpMsg msg = new UdpMsg();
        UdpMsg.FromBytes(ReceiveBuffer, ref msg, received);

        // Logging?
        Log("MSG", ref msg);

        // Now that we have the message we can do something with it....
        HandleMessage(ref msg, received);
      }
    }


    // ------------------------------------------------------------------------
    private void HandleMessage(ref UdpMsg msg, int msgLen)
    {
      var handler = this.MsgHandlers[(int)msg.header.type];
      handler(ref msg, msgLen);

      //throw new Exception("please finish me!");
      //// We will use an array of function pointers.....
      //switch (msg.header.type)
      //{
      //  default:
      //    Debug.WriteLine($"No support for message type: {msg.header.type}!");
      //}
      //// throw new NotImplementedException();
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
        Log($"Ignoring sync request from unknown endpoint ({msg.header.magic} != {_remote_magic_number}).");
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
        Log("Ignoring SyncReply while not synching.");
        return msg.header.magic == _remote_magic_number;
      }

      if (msg.u.sync_reply.random_reply != SyncState.random)
      {
        Log($"sync reply {msg.u.sync_reply.random_reply} != {SyncState.random}.  Keep looking...");
        return false;
      }

      if (!_connected)
      {
        var evt = new Event(EEventType.Connected);

        // TODO: A direct copy from sbyte in the future!
        string pn = msg.u.sync_reply.GetPlayerName();
        evt.u.connected.SetText(pn);
        // strcpy_s(evt.u.connected.playerName, msg.u.sync_reply.playerName);
        QueueEvent(ref evt);

        _connected = true;
      }

      Log($"Checking sync state ({SyncState.roundtrips_remaining} round trips remaining).");
      if (--SyncState.roundtrips_remaining == 0)
      {
        Log("Synchronized!");

        var e = new Event(EEventType.Synchronized);
        QueueEvent(ref e);
        _current_state = EClientState.Running;
        _last_received_input.frame = -1;
        _remote_magic_number = msg.header.magic;
      }
      else
      {
        var evt = new Event(EEventType.Synchronizing);
        evt.u.synchronizing.total = SYNC_PACKETS_COUNT;
        evt.u.synchronizing.count = SYNC_PACKETS_COUNT - (int)SyncState.roundtrips_remaining;
        QueueEvent(ref evt);
        SendSyncRequest();
      }
      return true;
    }

    private void QueueEvent(ref Event evt)
    {
      LogEvent("Queuing event", evt);
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
          Log($"creating rogue oop (seq: {entry.msg.header.sequence_number}  delay: {delay})");
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
          Log("sending rogue oop!");

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
      Client.Send(toSend, packetSize, ref UseRemote);
    }


    // ------------------------------------------------------------------------
    private void LogEvent(string v, Event evt)
    {
      Debug.WriteLine("implement this logging!");
      // throw new NotImplementedException();
    }

    // ------------------------------------------------------------------------
    private void Log(string msgType, ref UdpMsg msg)
    {
      Debug.WriteLine("implement this logging!");
    }

    // ------------------------------------------------------------------------
    private void Log(string msg)
    {
      Debug.WriteLine("implement this logging!");
    }

    // ------------------------------------------------------------------------
    private void Log(string v, byte[] data)
    {
      Debug.WriteLine("implement this logging!");
      // It is OK to do nothing for now...
      // throw new NotImplementedException();
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




  // ================================================================================================================
  [StructLayout(LayoutKind.Explicit)]
  public struct TestUnion
  {
    // This should overlap 'Single' by 4 bytes.
    [FieldOffset(0)] public double Double;
    [FieldOffset(0)] public float Single;
  }


  //// ================================================================================================================
  //[StructLayout(LayoutKind.Explicit)]
  //public struct SyncRequest
  //{
  //  [FieldOffset(0)] public UInt32 RandomRequest;
  //  [FieldOffset(4)] public UInt16 RemoteMagicNumber;
  //  [FieldOffset(6)] public byte RemoteEndpoint;

  //  // --------------------------------------------------------------------------------------------------------------
  //  internal static void FromBytes(byte[] data, int startOffset, ref SyncRequest res)
  //  {
  //    res.RandomRequest = BitConverter.ToUInt32(data, startOffset);
  //    res.RemoteMagicNumber = BitConverter.ToUInt16(data, startOffset + sizeof(UInt32));
  //    res.RemoteEndpoint = data[startOffset + sizeof(UInt32) + sizeof(UInt16)];
  //  }
  //}

  //[StructLayout(LayoutKind.Explicit)]
  //public struct SyncReply
  //{
  //  [FieldOffset(0)]
  //}


  // ========================================================================================================
  public class TimeSync
  {
    public const int FRAME_WINDOW_SIZE = 40;
    public const int MIN_UNIQUE_FRAMES = 10;
    public const int MIN_FRAME_ADVANTAGE = 3;
    public const int MAX_FRAME_ADVANTAGE = 9;

    protected int[] _local = new int[TimeSync.FRAME_WINDOW_SIZE];
    protected int[] _remote = new int[TimeSync.FRAME_WINDOW_SIZE];
    protected GameInput[] _last_inputs = new GameInput[MIN_UNIQUE_FRAMES];
    protected int _next_prediction;

    // -----------------------------------------------------------------------------------------------------
    public TimeSync()
    {
      _next_prediction = FRAME_WINDOW_SIZE * 3;
    }
    // virtual ~TimeSync();

    // -----------------------------------------------------------------------------------------------------
    public void rollback_frame(ref GameInput input, int localAdvantage, int remoteAdvantage)
    {
      throw new NotSupportedException();
      //// Remember the last frame and frame advantage
      //_last_inputs[input.frame % ARRAY_SIZE(_last_inputs)] = input;
      //_local[input.frame % ARRAY_SIZE(_local)] = localAdvantage;
      //_remote[input.frame % ARRAY_SIZE(_remote)] = remoteAdvantage;
    }

    // -----------------------------------------------------------------------------------------------------
    public int recommend_frame_wait_duration(bool require_idle_input)
    {
      throw new NotSupportedException();

      //// Average our local and remote frame advantages
      //int i, sum = 0;
      //float advantage, radvantage;
      //for (i = 0; i < ARRAY_SIZE(_local); i++)
      //{
      //  sum += _local[i];
      //}
      //advantage = sum / (float)ARRAY_SIZE(_local);

      //sum = 0;
      //for (i = 0; i < ARRAY_SIZE(_remote); i++)
      //{
      //  sum += _remote[i];
      //}
      //radvantage = sum / (float)ARRAY_SIZE(_remote);

      //static int count = 0;
      //count++;

      //// See if someone should take action.  The person furthest ahead
      //// needs to slow down so the other user can catch up.
      //// Only do this if both clients agree on who's ahead!!
      //if (advantage >= radvantage)
      //{
      //  return 0;
      //}

      //// Both clients agree that we're the one ahead.  Split
      //// the difference between the two to figure out how long to
      //// sleep for.
      //int sleep_frames = (int)(((radvantage - advantage) / 2) + 0.5);

      //LogIt("iteration %d:  sleep frames is %d", count, sleep_frames);

      //// Some things just aren't worth correcting for.  Make sure
      //// the difference is relevant before proceeding.
      //if (sleep_frames < MIN_FRAME_ADVANTAGE)
      //{
      //  return 0;
      //}

      //// Make sure our input had been "idle enough" before recommending
      //// a sleep.  This tries to make the emulator sleep while the
      //// user's input isn't sweeping in arcs (e.g. fireball motions in
      //// Street Fighter), which could cause the player to miss moves.
      //if (require_idle_input)
      //{
      //  for (i = 1; i < ARRAY_SIZE(_last_inputs); i++)
      //  {
      //    if (!_last_inputs[i].equal(_last_inputs[0], true))
      //    {
      //      LogIt("iteration %d:  rejecting due to input stuff at position %d...!!!", count, i);
      //      return 0;
      //    }
      //  }
      //}

      //// Success!!! Recommend the number of frames to sleep and adjust
      //return MIN(sleep_frames, MAX_FRAME_ADVANTAGE);
    }

  }



  // ========================================================================================================
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  public unsafe struct GameInput
  {
    public const UInt16 GAMEINPUT_MAX_BYTES = 7;
    public const UInt16 GAMEINPUT_MAX_PLAYERS = 4;    // NOTE: This probably need to be 2?
    public const int NullFrame = -1;

    public GameInput() { }

    public int frame;
    public int size; /* size in bytes of the entire input for all players */

    private const int BITS_SIZE = GAMEINPUT_MAX_BYTES * GAMEINPUT_MAX_PLAYERS;
    public fixed byte bits[BITS_SIZE];

    public bool is_null() { return frame == NullFrame; }
    public void init(int frame, char[] bits, int size, int offset)
    {
      throw new NotSupportedException();
      //ASSERT(isize);
      //ASSERT(isize <= GAMEINPUT_MAX_BYTES);
      //frame = iframe;
      //size = isize;
      //memset(bits, 0, sizeof(bits));
      //if (ibits)
      //{
      //  memcpy(bits + (offset * isize), ibits, isize);
      //}
    }

    public void init(int frame, char[] bits, int size)
    {
      throw new NotSupportedException();
      //ASSERT(isize);
      //ASSERT(isize <= GAMEINPUT_MAX_BYTES * GAMEINPUT_MAX_PLAYERS);
      //frame = iframe;
      //size = isize;
      //memset(bits, 0, sizeof(bits));
      //if (values)
      //{
      //  memcpy(bits, values, isize);
      //}
    }

    // ----------------------------------------------------------------------------------------
    public bool value(int i)
    {
      return (bits[i / 8] & (1 << (i % 8))) != 0;
    }

    // ----------------------------------------------------------------------------------------
    public void set(int i)
    {
      bits[i / 8] |= (byte)(1 << (i % 8));
    }

    // ----------------------------------------------------------------------------------------
    public void clear(int i)
    {
      bits[i / 8] &= (byte)~(1 << (i % 8));
    }

    // ----------------------------------------------------------------------------------------
    public unsafe void erase()
    {
      fixed (byte* pBits = bits)
      {
        Unsafe.InitBlock(pBits, 0, BITS_SIZE);
      }
    }

    // ----------------------------------------------------------------------------------------
    public void desc(char[] buf, int buf_size, bool show_frame = true)
    {
      throw new NotImplementedException();
      //ASSERT(size);
      //size_t remaining = buf_size;
      //if (show_frame)
      //{
      //  remaining -= sprintf_s(buf, buf_size, "(frame:%d size:%d ", frame, size);
      //}
      //else
      //{
      //  remaining -= sprintf_s(buf, buf_size, "(size:%d ", size);
      //}

      //for (int i = 0; i < size * 8; i++)
      //{
      //  char buf2[16];
      //  if (value(i))
      //  {
      //    int c = sprintf_s(buf2, ARRAY_SIZE(buf2), "%2d ", i);
      //    strncat_s(buf, remaining, buf2, ARRAY_SIZE(buf2));
      //    remaining -= c;
      //  }
      //}
      //strncat_s(buf, remaining, ")", 1);
    }

    public void LogIt(char[] prefix, bool show_frame = true)
    {
      throw new NotSupportedException();
      //char buf[1024];
      //size_t c = strlen(prefix);
      //strcpy_s(buf, prefix);
      //desc(buf + c, ARRAY_SIZE(buf) - c, show_frame);
      //strncat_s(buf, ARRAY_SIZE(buf) - strlen(buf), "", 1);
      //LogIt(buf);
    }

    public bool equal(ref GameInput input, bool bitsonly)
    {
      throw new NotSupportedException();
      //if (!bitsonly && frame != other.frame)
      //{
      //  LogIt("frames don't match: %d, %d", frame, other.frame);
      //}
      //if (size != other.size)
      //{
      //  LogIt("sizes don't match: %d, %d", size, other.size);
      //}
      //if (memcmp(bits, other.bits, size))
      //{
      //  LogIt("bits don't match");
      //}
      //ASSERT(size && other.size);
      //return (bitsonly || frame == other.frame) &&
      //       size == other.size &&
      //       memcmp(bits, other.bits, size) == 0;
    }
  }


}
