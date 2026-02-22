using drewCo.Tools.Logging;
using GGPOSharp;

namespace GGPOSharpTesters
{

  // ==============================================================================================================================
  public class NetworkTesters
  {
    // This is typical of a local network.
    // NOTE: In reality we should have a way to register the simulate ping + jitter for EACH port -> port connection.
    // we can get all fany with that at some other point in time..
    public const int SIM_PING = 4;
    public const int SIM_JITTER = 0;

    // NOTE: The hosts don't actually matter.  Just make them IP addresses.
    public const int PLAYER1_INDEX = 0;
    public const string PLAYER1_HOST = "127.0.0.1";
    public const int PLAYER1_PORT = 7000;

    public const int PLAYER2_INDEX = 1;
    public const string PLAYER2_HOST = "192.168.1.3";
    public const int PLAYER2_PORT = 7001;

    public const UInt64 SESSION_ID = 12345;


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

      // This is how we actually move the messages around....
      var timeSource = new SimTimer();
      var testQueue = new TestMessageQueue();

      var ops1 = new TestPlayerOptions()
      {
        PlayerIndex = PLAYER1_INDEX,
        Host = PLAYER1_HOST,
        Port = PLAYER1_PORT,
        TimeSource = timeSource,
        InputBuffer = new byte[5 * MAX_PLAYERS],
        PlayerName = "Joe"
      };
      var ops2 = new TestPlayerOptions()
      {
        PlayerIndex = PLAYER2_INDEX,
        Host = PLAYER2_HOST,
        Port = PLAYER2_PORT,
        TimeSource = timeSource,
        InputBuffer = new byte[5 * MAX_PLAYERS],
        PlayerName = "Archie"
      };
      var p1GGPO = CreateGGPOClient(ops1, ops2, testQueue, SESSION_ID);
      var p2GGPO = CreateGGPOClient(ops2, ops1, testQueue, SESSION_ID);

      // NOTE: If we use 'GetLocalPlayer' then the test fails.  This is part of some weird implementation
      // detail of how the GGPOEndpoints/Client code runs.  I am pretty sure this is by design, and I have
      // no intention of attempting to 'fix' it.
      var p2 = p1GGPO.GetRemotePlayer();
      var p1 = p2GGPO.GetRemotePlayer();

      var context = new TestContext(timeSource, new[] { p1GGPO, p2GGPO }, new[] { ops1.InputBuffer, ops2.InputBuffer });

      const int MAX_FRAMES = 50;
      context.RunGame(MAX_FRAMES);

      // Here we can check to see if the players are synced or not...
      Assert.That(p1._current_state == EClientState.Running, "P1 should be listed as running!");
      Assert.That(p2._current_state == EClientState.Running, "P2 should be listed as running!");

      // TODO: consider this logic.  The player names should be exchanged on handshake...
      var p1l = p1GGPO.GetLocalPlayer();
      var p2l = p2GGPO.GetLocalPlayer();

      Assert.That(p2.GetPlayerName(), Is.EqualTo(ops2.PlayerName));
      Assert.That(p1.GetPlayerName(), Is.EqualTo(ops1.PlayerName));
    }


    // --------------------------------------------------------------------------------------------------------------------------
    protected unsafe GGPOSessionCallbacks CreateDefaultCallbacks(byte playerIndex)
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
    protected GGPOClient CreateGGPOClient(TestPlayerOptions local, TestPlayerOptions remote, TestMessageQueue msgQueue, UInt64 sessionId, GGPOSessionCallbacks? callbacks = null)
    {
      if (callbacks == null) { callbacks = CreateDefaultCallbacks(local.PlayerIndex); }

      var udp = new SimUdp(local.Host, local.Port, local.TimeSource, msgQueue, SIM_PING, SIM_JITTER);
      var clientOps = new GGPOClientOptions(local.PlayerIndex, local.Port, Defaults.PROTOCOL_VERSION, sessionId);

      clientOps.IdleTimeout = 0;
      clientOps.Callbacks = callbacks;
      var res = new GGPOClient(clientOps, udp, local.TimeSource);

      res.AddLocalPlayer(local.PlayerName, local.PlayerIndex);

      clientOps.Callbacks.rollback_frame = x =>
      {
        GGPOSharp.Program.RunFrame(res, local.InputBuffer);
      };

      var remoteOps = new RemoteEndpointData(remote.Host, remote.Port, (byte)(remote.PlayerIndex + 1));
      res.AddRemotePlayer(remoteOps);

      return res;
    }
  }

}
