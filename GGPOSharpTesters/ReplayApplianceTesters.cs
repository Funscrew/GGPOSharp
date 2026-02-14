using drewCo.Tools;
using drewCo.Tools.Logging;
using GGPOSharp;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using System.Reflection.Metadata;

namespace GGPOSharpTesters
{

  // ==============================================================================================================================
  public class ReplayApplianceTesters
  {
    // This is typical of a local network.
    // NOTE: In reality we should have a way to register the simulate ping + jitter for EACH port -> port connection.
    // we can get all fany with that at some other point in time..
    protected const int SIM_PING = 4;
    protected const int SIM_JITTER = 0;


    // --------------------------------------------------------------------------------------------------------------------------
    [SetUp]
    public void Setup()
    { }

    // --------------------------------------------------------------------------------------------------------------------------
    private static void NoOp_BeginGame(string gameName)
    {
      Log.Debug($"The game: {gameName} was started!");
    }

    // --------------------------------------------------------------------------------------------------------------------------
    private static bool NoOp_Event(ref GGPOEvent evt)
    {
      return true;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    private unsafe static bool NoOp_SaveGame(byte** buffer, int* len, int* checksum, int frame)
    {
      // We need to have some kind of data to save, or the system will explode!
      *buffer = (byte*)0x1;
      *len = 1;
      *checksum = 0;

      return true;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    private unsafe static bool NoOp_LoadGame(byte** buffer, int len)
    {
      return true;
    }

    // ------------------------------------------------------------------------------------------------------
    private static unsafe bool NoOp_FreeBuffer(byte* arg)
    {
      // NOTE: We don't have to do anything here!
      //Log.Info("An indication to free a buffer happened!");
      return true;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// This shows that we are able to create two clients and have them communicate over a virtual, ideal network.
    /// The main purpose of this is to show that we can indeed simulate a network, which will allow us to
    /// develop the protocol bits faster + make automated tests to hadle all kinds of scenarios.
    /// </summary>
    [Test]
    public unsafe void CanSimluateNetworkGame()
    {
      const int MAX_PLAYERS = 4;      // GGPO Default.  Really should be two!


      //const int RA_PORT = 7002;
      const UInt64 SESSION_ID = 12345;

      // This is how we actually move the messages around....
      var timeSource = new SimTimer();
      var testQueue = new TestMessageQueue();


      // NOTE: The hosts don't actually matter.  Just make them IP addresses.
      const int PLAYER1_INDEX = 0;
      const string PLAYER1_HOST = "127.0.0.1";
      const int PLAYER1_PORT = 7000;

      const int PLAYER2_INDEX = 1;
      const string PLAYER2_HOST = "192.168.1.3";
      const int PLAYER2_PORT = 7001;

      //byte[] p1Input = new byte[5 * MAX_PLAYERS];
      //byte[] p2Input = new byte[5 * MAX_PLAYERS];


      var ops1 = new PlayerOptions()
      {
        PlayerIndex = PLAYER1_INDEX,
        Host = PLAYER1_HOST,
        Port = PLAYER1_PORT,
        TimeSource = timeSource,
        InputBuffer = new byte[5 * MAX_PLAYERS],
        Name = "Joe"
      };
      var ops2 = new PlayerOptions()
      {
        PlayerIndex = PLAYER2_INDEX,
        Host = PLAYER2_HOST,
        Port = PLAYER2_PORT,
        TimeSource = timeSource,
        InputBuffer = new byte[5 * MAX_PLAYERS],
        Name = "Archie"
      };
      var p1GGPO = CreateGGPOClient(ops1, ops2, testQueue, SESSION_ID);
      var p2GGPO = CreateGGPOClient(ops2, ops1, testQueue, SESSION_ID);

      var p1 = p1GGPO.GetLocalPlayer();
      var p2 = p2GGPO.GetLocalPlayer();

      var context = new TestContext(timeSource, new[] { p1GGPO, p2GGPO }, new[] { ops1.InputBuffer, ops2.InputBuffer });

      const int MAX_FRAMES = 50;
      context.RunGame(MAX_FRAMES);

      // Here we can check to see if the players are synced or not...
      Assert.That(p1._current_state == EClientState.Running, "P1 should be listed as running!");
      Assert.That(p2._current_state == EClientState.Running, "P2 should be listed as running!");

    }


    // --------------------------------------------------------------------------------------------------------------------------
    private unsafe GGPOSessionCallbacks CreateCallbacks(byte playerIndex)
    {
      var callbacks = new GGPOSessionCallbacks()
      {
        free_buffer = NoOp_FreeBuffer,
        begin_game = NoOp_BeginGame,
        on_event = NoOp_Event,
        save_game_state = NoOp_SaveGame,
        load_game_state = NoOp_LoadGame,
      };

      return callbacks;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    private GGPOClient CreateGGPOClient(PlayerOptions local, PlayerOptions remote, TestMessageQueue msgQueue, UInt64 sessionId, GGPOSessionCallbacks? callbacks = null)
    {
      if (callbacks == null) { callbacks = CreateCallbacks(local.PlayerIndex); }


      var udp = new SimUdp(local.Host, local.Port, local.TimeSource, msgQueue, SIM_PING, SIM_JITTER);
      var clientOps = new GGPOClientOptions(local.PlayerIndex, local.Port, Defaults.PROTOCOL_VERSION, sessionId);

      clientOps.IdleTimeout = 0;
      clientOps.Callbacks = callbacks;
      var res = new GGPOClient(clientOps, udp, local.TimeSource);

      res.AddLocalPlayer(local.Name, local.PlayerIndex);

      clientOps.Callbacks.rollback_frame = x =>
      {
        GGPOSharp.Program.RunFrame(res, local.InputBuffer);
      };

      var remoteOps = new RemoteEndpointData(remote.Host, remote.Port, (byte)(remote.PlayerIndex + 1));
      res.AddRemotePlayer(remoteOps);

      return res;
    }
  }

  // ==============================================================================================================================
  public class PlayerOptions
  {
    public byte PlayerIndex { get; set; }
    public string Host { get; set; }
    public int Port { get; set; }
    public SimTimer TimeSource { get; set; }
    public byte[] InputBuffer { get; set; }
    public string Name { get; set; }
  }

  //// ==============================================================================================================================
  //public class CreateGGPOClient
  //{
  //  public PlayerOptions PlayerOptions { get; private set; } = default!;
  //}

  // ==============================================================================================================================
  public class TestContext
  {
    const int TIME_INTERVAL = 1;
    const int FRAME_INTERVAL = 16;

    private SimTimer TimeSource = default!;
    private List<GGPOClient> AllClients = new List<GGPOClient>();
    private List<byte[]> InputBuffers = new List<byte[]>();

    // --------------------------------------------------------------------------------------------------------------------------
    public TestContext(SimTimer timeSource_, IList<GGPOClient> allClients_, IList<byte[]> inputBuffers_)
    {
      TimeSource = timeSource_;
      AllClients.AddRange(allClients_);
      InputBuffers.AddRange(inputBuffers_);
    }


    // --------------------------------------------------------------------------------------------------------------------------
    public void RunGame(int totalFrameCount)
    {

      // The total number of 'frames' that we want to simulate in this case.
      //  const int MAX_FRAMES = 50;
      for (int i = 0; i < totalFrameCount; i++)
      {
        TimeSource.AddTime(TIME_INTERVAL);

        if (i % FRAME_INTERVAL == 0)
        {
          // TODO: I want to change the inputs per frame.  Data doesn't matter, just that it can be exchanged.
          // Probably just increment the bits....
          // p1Input[0] = (byte)(i & 0xFF);

          int len = AllClients.Count;
          for (int j = 0; j < len; j++)
          {
            var c = AllClients[j];
            Program.RunFrame(c, InputBuffers[j]);
          }

        }
        else
        {
          // TODO: A proper idle() function.....    (see Program.cs for example)
          // This is where we would send out the player inputs and so on....
          int len = AllClients.Count;
          for (int j = 0; j < len; j++)
          {
            var c = AllClients[j];
            c.Idle();
          }
        }
      }

    }
  }

  // ==============================================================================================================================
  public class SimPlayerOptions
  {

  }

  // ==============================================================================================================================
  public class SimGameOptions
  {

  }




}
