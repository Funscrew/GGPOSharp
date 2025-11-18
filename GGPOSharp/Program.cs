using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace GGPOSharp
{
  // ========================================================================================================
  internal class Program
  {
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    public static extern void TimeBeginPeriod(int t);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    public static extern void TimeEndPeriod(int t);

    // ------------------------------------------------------------------------------------------------------
    static unsafe void Main(string[] args)
    {
      Console.WriteLine("Welcome to GGPO Example Client!");

      const int PLAYER_ONE = 0;
      const int PLAYER_TWO = 1;
      var ops = new GGPOClientOptions(PLAYER_TWO, "Screwie", Defaults.LOCAL_PORT)
      {
        Callbacks = new GGPOSessionCallbacks()
        {
          begin_game = OnBeginGame,
          free_buffer = OnFreeGamestateBuffer,
          on_event = OnEvent,
          rollback_frame = OnRollback,
          save_game_state = SaveGameState,
          load_game_state = LoadGameState
        }
      };



      var c = new GGPOClient(ops);
      c.AddLocal("Screwie", PLAYER_TWO, null);
      
      c.AddRemote(Defaults.REMOTE_HOST, Defaults.REMOTE_PORT, PLAYER_ONE);
      c.Lock();

      // Game loop:
      // No, this isn't meant to be a sophisticated timing scenario, just get us in the ballpark...
      TimeBeginPeriod(1);

      var sw = Stopwatch.StartNew();
      const double FPS = 60.0d;
      double lastTime = double.MinValue;
      double frameTime = 1.0d / FPS;
      double nextFrameTime = 0.0d;

      // Some test input.  This mimics no buttons being pushed, and one DIP set
      // for 3rd strike.
      // The rest of the array data is reserved for the rest of the player inputs.
      byte[] testInput = new byte[5 * GGPOConsts.UDP_MSG_MAX_PLAYERS];
      testInput[1] = 1;

      int frameCount = 0;
      while (true)
      {
        double elapsed = sw.Elapsed.TotalSeconds;
        if (elapsed < nextFrameTime)
        {
          c.Idle();
        }
        else
        {
          // Send + receive inputs across the network.
          if (!c._synchronizing)
          {
            bool syncOK = c.SyncInputs(testInput, 5, GGPOConsts.UDP_MSG_MAX_PLAYERS);

            // Tell the client that we have moved ahead one frame.
            if (syncOK)
            {
              c.IncrementFrame();
            }
          }

          // TODO: Make a switch for this.
          // We get the stats, and then we can splat them to the screen however
          // we please.
          // var stats = c.GetNetworkStats();
          // OutputStats(stats);

          // This is where we will increment the frame!
          ++frameCount;
          nextFrameTime += frameTime;

          //double curFPS = frameCount / elapsed;
          //if (frameCount % 60 == 0)
          //{
          //  Console.WriteLine($"FPS:{curFPS:f2}");
          //}
        }

        //double remainder = frameTime - (elapsed - lastTime);
        //if (remainder <= 0.0d)
        //{
        //  ++frameCount;

        //  c.SyncInputs(testInput, testInput.Length);

        //  // c.IN

        //  c.DoPoll(0);
        //  // double curFPS = frameCount / elapsed;
        //  //if (frameCount % 60 == 0) {
        //  //  Console.WriteLine($"FPS:{curFPS:f2}");
        //  //}
        //  lastTime = elapsed;
        //}
        //else
        //{
        //  c.Idle();
        //  // int sleepFor = (int)remainder * 1000;
        //  Thread.Sleep((int)(remainder * 1000.0d));
        //}

      }

      //int frameCount = 0;
      //while (true)
      //{
      //  double elapsed = sw.Elapsed.TotalSeconds;
      //  double remainder = frameTime - (elapsed - lastTime);
      //  if (remainder <= 0.0d)
      //  {
      //    ++frameCount;

      //    c.SyncInputs(testInput, testInput.Length);

      //    // c.IN

      //    c.DoPoll(0);
      //    // double curFPS = frameCount / elapsed;
      //    //if (frameCount % 60 == 0) {
      //    //  Console.WriteLine($"FPS:{curFPS:f2}");
      //    //}
      //    lastTime = elapsed;
      //  }
      //  else
      //  {
      //    c.Idle();
      //    // int sleepFor = (int)remainder * 1000;
      //    Thread.Sleep((int)(remainder * 1000.0d));
      //  }

      //}

      //TimeEndPeriod(1);
      //return;

      //// OPTIONS:
      //const int LISTEN_PORT = 7001;
      //const int REMOTE_PORT = 7000;
      //Console.WriteLine("Waiting for incoming data...");

      //var client = new UdpClient(LISTEN_PORT);
      //var remote = new IPEndPoint(IPAddress.Any, REMOTE_PORT);

      //string useHost = args[0];
      //var remoteHost = new IPEndPoint(IPAddress.Parse(useHost), REMOTE_PORT);
      //// NOTE: If we don't call this, then we can't connect!
      //client.Connect(remoteHost);

      //while (true)
      //{
      //  // if (client.Available > 0) {
      //  byte[] data = client.Receive(ref remote);

      //  UdpMsg msg = new UdpMsg();
      //  UdpMsg.FromBytes(data, ref msg);
      //  Console.WriteLine($"Received content: {data.Length} bytes long....");

      //  // Handle the message.  What we get + how we handle depends on the current state....
      //  switch (msg.header.type)
      //  {
      //  case EMsgType.SyncRequest:
      //    Console.WriteLine("We received a sync request!");
      //  break;
      //    default:
      //      throw new InvalidOperationException($"Unsupported message type: {msg.header.type}");
      //  }
      //  // msg.Header.Type == EMsgType.SyncRequest;

      //  // NOTE: We are assuming that we are in sync mode......
      //  // Let's send a message back....
      //  UdpMsg reply = new UdpMsg();
      //  byte[] toSend = new byte[32];

      //  reply.ToBytes(toSend, out int length);

      //  client.Send(toSend, length);

      //  /// string data = msg.ad
      //}

    }

    // ------------------------------------------------------------------------------------------------------
    private static void OnBeginGame(string gameName)
    {
      Console.WriteLine("The game has started!");
    }

    // ------------------------------------------------------------------------------------------------------
    private static unsafe bool OnFreeGamestateBuffer(byte* arg)
    {
      // NOTE: We don't have to do anything here!
      Console.WriteLine("An indication to free a buffer happened!");
      return true;
    }

    // ------------------------------------------------------------------------------------------------------
    private static unsafe bool LoadGameState(byte** buffer, int len)
    {
      // NOTE: We don't attempt to load game state....
      Console.WriteLine("no state to load...");
      return true;
    }

    // This is just here so that we can have a non-zero game state....
    // private static byte FAKE_STATE = 1;
    // ------------------------------------------------------------------------------------------------------
    private static unsafe bool SaveGameState(byte** buffer, int* len, int* checksum, int frame)
    {
      // NOTE: This is a FAKE buffer!
      // We don't have a game state, but the system expects something non-zero!
      // In the future I want there to be options for how the save / load, etc. handlers work....
      *buffer = (byte*)0x1;
      *len = 1;
      *checksum = 0;

      Console.WriteLine("nothing to save....");
      return true;
      // throw new NotImplementedException();
    }

    // ------------------------------------------------------------------------------------------------------
    private static bool OnEvent(ref GGPOEvent arg)
    {
      Console.WriteLine($"There was an event: {arg.code}");
      return true;
    }


    // ------------------------------------------------------------------------------------------------------
    static void OnRollback(int frameCount)
    {
      Console.WriteLine($"A rollback of: {frameCount} frames was detected!");
    }
  }
}
