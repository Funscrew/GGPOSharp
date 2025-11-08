using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

// using MsgHandler = System.Action<GGPOSharp.UdpMsg, int>;
using System;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.CompilerServices;

delegate void MsgHandler<T>(ref T msg, int msgLen);

namespace GGPOSharp
{

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


    public EClientState CurrentState { get; private set; } = EClientState.Disconnected;

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
    private uint _last_send_time = 0;
    private uint _last_recv_time = 0;
    public uint _shutdown_timeout = 0;
    public uint _disconnect_event_sent = 0;
    public uint _disconnect_timeout = 0;
    public uint _disconnect_notify_start = 0;
    bool _disconnect_notify_sent = false;

    public UInt16 _next_send_seq = 0;
    public UInt16 _next_recv_seq = 0;


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
      if (CurrentState != EClientState.Disconnected)
      {
        throw new InvalidOperationException("Invalid state to begin synchronize operations.");
      }

      CurrentState = EClientState.Syncing;
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
      LogIt("send", ref msg);

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

      switch (CurrentState)
      {
        case EClientState.Disconnected:
          break;

        case EClientState.Syncing:
          // do sync timeout + resend stuff here....
          next_interval = (SyncState.roundtrips_remaining == SYNC_PACKETS_COUNT) ? SYNC_FIRST_RETRY_INTERVAL : SYNC_RETRY_INTERVAL;
          if (_last_send_time > 0 && _last_send_time + next_interval < now)
          {
            LogIt($"No luck syncing after {next_interval} ms... Re-queueing sync packet.");
            SendSyncRequest();
          }
          break;

        case EClientState.Synchronzied:

        default:
          throw new InvalidOperationException($"Invalid current state: {CurrentState}");
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
        LogIt("MSG", ref msg);

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
    private void OnInvalid(ref UdpMsg msg, int msgLen)
    {
      throw new GGPOException("Invalid message!");
    }

    // ------------------------------------------------------------------------
    private void OnSyncRequest(ref UdpMsg msg, int msgLen)
    {
      throw new GGPOException("sync request!!");
    }

    // ------------------------------------------------------------------------
    private void OnSyncReply(ref UdpMsg msg, int msgLen)
    {
      throw new GGPOException("sync reply!!");
    }

    // ------------------------------------------------------------------------
    /// <summary>
    /// Send the outgoing messages in the queue.
    /// </summary>
    private void PumpSendQueue()
    {
      // TODO: Send the messages.
      // TODO: Simulate network latency / jitter.
      // TODO: Simulate OO (out out order) packets.

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
          LogIt($"creating rogue oop (seq: {entry.msg.header.sequence_number}  delay: {delay})");
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
          //_udp->SendTo((char*)entry.msg, packetSize, 0,
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
          LogIt("sending rogue oop!");

          SendMsgPacket(_oo_packet.msg);
          // int packetSize = _oo_packet.msg.PacketSize();
          //_udp->SendTo((char*)_oo_packet.msg, packetSize, 0,
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
    private void LogIt(string msgType, ref UdpMsg msg)
    {
    }

    // ------------------------------------------------------------------------
    private void LogIt(string msg)
    {
    }

    // ------------------------------------------------------------------------
    private void LogIt(string v, byte[] data)
    {
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



}
