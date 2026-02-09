using drewCo.Tools.Logging;
using GGPOSharp;
using GGPOSharp.Clients;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using NUnit.Framework.Constraints;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace GGPOSharpTesters
{


  // ==============================================================================================================================
  public class ReplayApplianceTesters
  {
    // --------------------------------------------------------------------------------------------------------------------------
    [SetUp]
    public void Setup()
    {
    }

    //// --------------------------------------------------------------------------------------------------------------------------
    //private static void NoOp_Rollback(int flags)
    //{
    //  // TODO: Logging...
    //  GGPOSharp.Program.RunFrame(Client, TestInput);
    //}

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

      const int PLAYER1_INDEX = 0;
      const int PLAYER2_INDEX = 1;

      const int PLAYER1_PORT = 7000;
      const int PLAYER2_PORT = 7001;
      const int RA_PORT = 7002;
      const UInt64 SESSION_ID = 12345;

      // This is how we actually move the messages around....
      var timeSource = new SimTimer();
      var testQueue = new TestMessageQueue();


      // So we have the basics all setup, but there needs to be some streamlining in terms of
      // being able to have the SimUdp devices sending + receiving to the correct places.
      // We might be able to use the 'SocketAddress useRemote' argument in IUdpBlaster.Send
      // to get the actual, correct remote address, but that will need a bit of research.
      // All the same, I need to review how we setup + deal with the remote/local ports, just so
      // that I know.
      // I may want to look into a better way to setup the appliance + its clients so that there
      // are less options, etc. flying all over the place.
      // throw new NotImplementedException();

      //// This is the replay appliance.  It accepts connections from one of the normal clients.
      //var applianceOps = new GGPOClientOptions(GGPOConsts.REPLAY_APPLIANCE_PLAYER_INDEX, RA_PORT, Defaults.PROTOCOL_VERSION, SESSION_ID);
      //applianceOps.Callbacks = new GGPOSessionCallbacks()
      //{
      //  free_buffer = NoOp,
      //  rollback_frame = NoOp,
      //  begin_game = NoOp,
      //};

      // This is typical of a local network.
      // NOTE: In reality we should have a way to register the simulate ping + jitter for EACH port -> port connection.
      // we can get all fany with that at some other point in time..
      const int SIM_PING = 4;
      const int SIM_JITTER = 0;

      // NOTE: The hosts don't actually matter.  Just make them IP addresses.
      const string PLAYER1_HOST = "127.0.0.1";
      const string PLAYER2_HOST = "192.168.1.3";

      byte[] p1Input = new byte[5 * MAX_PLAYERS];
      byte[] p2Input = new byte[5 * MAX_PLAYERS];


      var p1Udp = new SimUdp(PLAYER1_HOST, PLAYER1_PORT, timeSource, testQueue, SIM_PING, SIM_JITTER);
      var p1Ops = new GGPOClientOptions(PLAYER1_INDEX, PLAYER1_PORT, Defaults.PROTOCOL_VERSION, SESSION_ID);
      p1Ops.IdleTimeout = 0;
      p1Ops.Callbacks = CreateCallbacks(PLAYER1_INDEX);
      var p1GGPO = new GGPOClient(p1Ops, p1Udp, timeSource);
      p1GGPO.AddLocalPlayer("Joe", PLAYER1_INDEX);

      p1Ops.Callbacks.rollback_frame = x =>
      {
        GGPOSharp.Program.RunFrame(p1GGPO, p1Input);
      };


      var p2Udp = new SimUdp(PLAYER2_HOST, PLAYER2_PORT, timeSource, testQueue, SIM_PING, SIM_JITTER);
      var p2Ops = new GGPOClientOptions(PLAYER2_INDEX, PLAYER2_PORT, Defaults.PROTOCOL_VERSION, SESSION_ID);
      p2Ops.IdleTimeout = 0;
      p2Ops.Callbacks = CreateCallbacks(PLAYER2_INDEX);

      var p2GGPO = new GGPOClient(p2Ops, p2Udp, timeSource);
      p2GGPO.AddLocalPlayer("Archie", PLAYER2_INDEX);

      p2Ops.Callbacks.rollback_frame = x =>
      {
        GGPOSharp.Program.RunFrame(p2GGPO, p2Input);
      };


      // Add the remotes to the test clients:
      var p2 = p1GGPO.AddRemotePlayer(new RemoteEndpointData(PLAYER2_HOST, PLAYER2_PORT, PLAYER2_INDEX + 1));
      var p1 = p2GGPO.AddRemotePlayer(new RemoteEndpointData(PLAYER1_HOST, PLAYER1_PORT, PLAYER1_INDEX + 1));

      const int TIME_INTERVAL = 1;
      const int FRAME_INTERVAL = 16;

      // The total number of 'frames' that we want to simulate in this case.
      const int MAX_FRAMES = 50;
      for (int i = 0; i < MAX_FRAMES; i++)
      {
        timeSource.AddTime(TIME_INTERVAL);

        if (i % FRAME_INTERVAL == 0)
        {
          // TODO: I want to change the inputs per frame.  Data doesn't matter, just that it can be exchanged.
          // Probably just increment the bits....
          p1Input[0] = (byte)(i & 0xFF);

          GGPOSharp.Program.RunFrame(p1GGPO, p1Input);
          GGPOSharp.Program.RunFrame(p2GGPO, p2Input);
        }
        else
        {
          // TODO: A proper idle() function.....    (see Program.cs for example)
          // This is where we would send out the player inputs and so on....
          p1GGPO.Idle();
          p2GGPO.Idle();
        }
      }

      // NOTE: We could hook up event listeners and count rollbacks and things of that nature.
      // One thing I would really like to be able to simulate is the 'start time' of each of the
      // clients, which is a thing that I've seen happen in real life.  The simulations don't
      // start at the same universal clock time, so one is always kind of behind, and that can
      // trigger a lot of rollbacks from what I see.

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
        // rollback_frame = NoOp_Rollback,
        begin_game = NoOp_BeginGame,
        // one day....
        //on_event = (ref x) => {
        //  return true;
        //}
        on_event = NoOp_Event,
        save_game_state = NoOp_SaveGame,
        load_game_state = NoOp_LoadGame,
      };

      return callbacks;
    }
  }
}