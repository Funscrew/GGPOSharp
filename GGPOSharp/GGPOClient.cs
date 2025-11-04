using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;

using MsgHandler = System.Action<GGPOSharp.UdpMsg, int>;

namespace GGPOSharp
{

  // ================================================================================================================
  public class GGPOClientOptions
  {
    public const int LOCAL_PORT = 7001;
    public const int REMOTE_PORT = 7000;

    public int SendLatency { get; set; } = 0;

    public int LocalPort { get; set; } = LOCAL_PORT;
    public string RemoteAddress { get; set; }
    public int RemotePort { get; set; } = REMOTE_PORT;
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
    private RingBuffer<int> SendQueue = new RingBuffer<int>(SEND_QUEUE_SIZE);

    /// <summary>
    /// Data used when we are syncing the clients.
    /// </summary>
    private SyncData SyncData = new SyncData();

    /// <summary>
    /// Data used when the client is in running state.
    /// </summary>
    private RunningData RunningData = new RunningData();


    private UdpClient Client = null!;

    private int LocalPort;
    private int RemotePort;
    private IPEndPoint Remote;

    private GGPOClientOptions Options = null!;

    private Stopwatch Time = Stopwatch.StartNew();

    private MsgHandler[] MsgHandlers = new MsgHandler[8];

    // -------------------------------------------------------------------------------------
    public GGPOClient(GGPOClientOptions ops_)
    {
      // MsgHandlers[0] = OnInvalid;

      Options = ops_;
      Client = new UdpClient(Options.LocalPort);
      Remote = new IPEndPoint(IPAddress.Parse(Options.RemoteAddress), Options.RemotePort);

      // We can't send packets if we don't connect!
      Client.Connect(Remote);

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
      SyncData.roundtrips_remaining = SYNC_PACKETS_COUNT;
      SendSyncRequest();
    }

    // -------------------------------------------------------------------------------------
    private void SendSyncRequest()
    {
      // throw new NotImplementedException();
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


      switch (CurrentState)
      {
        case EClientState.Disconnected:
          break;

        case EClientState.Syncing:
        // 
        case EClientState.Synchronzied:

        default:
          throw new InvalidOperationException($"Invalid current state: {CurrentState}");
      }
    }

    // ------------------------------------------------------------------------
    private void ReceiveMessages()
    {
      // Pull in all messages, while they are available.
      while (Client.Available > 0)
      {

        // Get the next message.....
        byte[] data = Client.Receive(ref Remote);

        UdpMsg msg = new UdpMsg();
        UdpMsg.FromBytes(data, ref msg);

        // Logging?
        LogIt("MSG", data);

        // Now that we have the message we can do something with it....
        HandleMessage(ref msg, data.Length);
      }
    }


    // ------------------------------------------------------------------------
    private void HandleMessage(ref UdpMsg msg, int msgLen)
    {
      throw new Exception("please finish me!");
      //// We will use an array of function pointers.....
      //switch (msg.header.type)
      //{
      //  default:
      //    Debug.WriteLine($"No support for message type: {msg.header.type}!");
      //}
      //// throw new NotImplementedException();
    }

    // ------------------------------------------------------------------------
    private void OnInvalid(ref UdpMsg msg, int msgLen) { 
      throw new GGPOException("Invalid message!");
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

      // throw new NotImplementedException();
    }

    // ------------------------------------------------------------------------
    private void LogIt(string v, byte[] data)
    {
      // It is OK to do nothing for now...
      // throw new NotImplementedException();
    }
  }


  // ================================================================================================================
  public unsafe struct QueueEntry
  {
    public int queue_time;
    public IPEndPoint dest_addr;
    public UdpMsg* msg;
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
