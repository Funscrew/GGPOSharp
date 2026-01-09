using GGPOSharp.Clients;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
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

    static GGPOClient Client = null!;

    // Some test input.  This mimics no buttons being pushed, and one DIP set
    // for 3rd strike.
    // The rest of the array data is reserved for the rest of the player inputs.
    // REFACTOR: Move this elsehere....  Might not even want to use a const so mem can be dynamic for different games.....
    public const int INPUT_SIZE = 5;
    static byte[] TestInput = new byte[INPUT_SIZE * GGPOConsts.UDP_MSG_MAX_PLAYERS];

    // NOTE: We are assuming one connected client still...
    // private static int ReconnectEndpointIndex = -1;
    private const int RECONNECT_WAIT_TIME = 250;
    private static double ReconnectTime = -1;

    private static Stopwatch Clock = null;

    const int PLAYER_ONE = 0;
    const int PLAYER_TWO = 1;
    const byte PROTOCOL_VERSION = 3;
    const string LOCAL_PLAYER_NAME = "Screwie";

    // ------------------------------------------------------------------------------------------------------
    static unsafe void Main(string[] args)
    {
      Console.WriteLine("Welcome to GGPO Example Client!");

      // TODO: Copy the CLI command from fs-fbneo for this....
      //var logOps = new GGPOLogOptions()
      //{
      //  LogToFile = false,
      //  FilePath = "ggpo-log.txt",
      //  ActiveCategories = $"{LogCategories.MESSAGE}"
      //};
      //Utils.InitLogging(logOps);


      // const UInt32 clientVersion = PROTOCOL_VERSION;


      InitializeClient();

      // Game loop:
      // No, this isn't meant to be a sophisticated timing scenario, just get us in the ballpark...
      TimeBeginPeriod(1);

      Clock = Stopwatch.StartNew();
      const double FPS = 60.0d;
      double lastTime = double.MinValue;
      double frameTime = 1.0d / FPS;
      double nextFrameTime = 0.0d;


      int frameCount = 0;
      while (true)
      {
        double elapsed = Clock.Elapsed.TotalSeconds;
        if (elapsed < nextFrameTime)
        {
          if (ReconnectTime != -1 && elapsed >= ReconnectTime)
          {
            // Put the client back into a listening / sync state...
            Console.WriteLine("Putting the client back into sync state, waiting for remote connection...");
            ReconnectTime = -1.0d;
            InitializeClient();

          }
          else
          {
            Client.Idle();
          }
        }
        else
        {
          // This is so we send the correct data each time.
          // Following the FC example, we write our inputs at the p1 address
          // and it will pass it on to the correct input queue.
          for (int i = 0; i < INPUT_SIZE; i++)
          {
            TestInput[0] = 0;
          }
          // TestInput[1] = 1;

          // Send + receive inputs across the network.
          // NOTE: The bytes in TestInput will be overwritten during this process!  This is
          // by design!  For emulators, etc. it is convenient to always use the p1 control scheme,
          // even if you are repping p2!
          bool synced = RunFrame(Client, TestInput);

          // TODO: What do we do here?  
          // For now, I am going to reset the inputs that we send to the other player.
          // Of course, I will have their inputs too, but at this point I want to think about
          // how we might capture + replay that data according to our first challenge!
          if (synced)
          {
            // We can read in the inputs from here...?
            // Technically, this would be enough to record inputs for a game as if we are synced, we havea  complete frame.
            // Of course, we would want to do that inside of the actual client somewhere....
          }

          // TODO: Make a switch for this.
          // We get the stats, and then we can splat them to the screen however
          // we please.
          // var stats = c.GetNetworkStats();
          // OutputStats(stats);
          // NOTE: --> This is happening internally anyway.....

          // This is where we will increment the frame!
          ++frameCount;
          nextFrameTime += frameTime;
        }

      }
    }

    // ------------------------------------------------------------------------------------------------------
    private static unsafe void InitializeClient()
    {
      Console.WriteLine("Initializing the client...");

      if (Client != null) { 
        Console.WriteLine("Disposing old client....");
        Client.Dispose();
      }

      var ops = new GGPOClientOptions(PLAYER_ONE, Defaults.LOCAL_PORT, PROTOCOL_VERSION)
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

      Client = new InputEchoClient(ops, new InputEchoOptions());


      var local = Client.AddLocalPlayer(LOCAL_PLAYER_NAME, PLAYER_ONE, null);

      var remote = Client.AddRemotePlayer(Defaults.REMOTE_HOST, Defaults.REMOTE_PORT, PLAYER_TWO);
      remote.SetPlayerName(LOCAL_PLAYER_NAME);

      // No more endpoints can be added!
      Client.Lock();
    }

    // ------------------------------------------------------------------------------------------------------
    private static bool RunFrame(GGPOClient c, byte[] testInput)
    {
      if (!c._synchronizing)
      {
        bool syncOK = c.SyncInput(testInput, INPUT_SIZE, GGPOConsts.UDP_MSG_MAX_PLAYERS);

        // Tell the client that we have moved ahead one frame.
        if (syncOK)
        {
          c.IncrementFrame();
        }
        return syncOK;
      }
      return false;
    }

    // ------------------------------------------------------------------------------------------------------
    private static void OnBeginGame(string gameName)
    {
      Console.WriteLine("The game has started!  Beginning sync....");
    }

    // ------------------------------------------------------------------------------------------------------
    private static unsafe bool OnFreeGamestateBuffer(byte* arg)
    {
      // NOTE: We don't have to do anything here!
      //Console.WriteLine("An indication to free a buffer happened!");
      return true;
    }

    // ------------------------------------------------------------------------------------------------------
    private static unsafe bool LoadGameState(byte** buffer, int len)
    {
      // NOTE: We don't attempt to load game state....
      // Console.WriteLine("no state to load...");
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

      //Console.WriteLine("nothing to save....");
      return true;
      // throw new NotImplementedException();
    }

    // ------------------------------------------------------------------------------------------------------
    private static unsafe bool OnEvent(ref GGPOEvent evt)
    {
      if (evt.u.datagram.code == (byte)EDatagramCode.DATAGRAM_CODE_CHAT)
      {
        fixed (byte* pData = evt.u.datagram.data)
        {
          string text = AnsiHelpers.PtrToFixedLengthString(pData, evt.u.datagram.dataSize, GGPOConsts.MAX_GGPO_DATA_SIZE);
          Console.WriteLine($"Text is: {text}");
        }
      }

      else if (evt.u.datagram.code == (byte)EDatagramCode.DATAGRAM_CODE_DISCONNECT)
      {
        Console.WriteLine("disconnect notice was received...");
        ReconnectTime = Clock.Elapsed.TotalSeconds + ((float)RECONNECT_WAIT_TIME / 1000.0f);
      }

      //Console.WriteLine($"There was an event: {arg.code}");
      return true;
    }


    // ------------------------------------------------------------------------------------------------------
    static void OnRollback(int flags)
    {
      // We run the next frame on rollback, or it all gets fucked!
      RunFrame(Client, TestInput);

      //Console.WriteLine($"A rollback was detected!");
    }
  }
}
