using drewCo.Tools.Logging;
using GGPOSharp;
using GGPOSharp.Clients;

namespace GGPOSharpTesters
{

  // ==============================================================================================================================
  public class NetworkTesters : TestBase
  {

    // --------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// Shows that two clients can connect to the same replay client, and that client is able to record
    /// their inputs.
    /// NOTE: This test in particular doesn't do anything about handling dropped or OO packets.
    /// --> edge case, an input that is sent to player 1 could be dropped, but the replay client could receive it.  How do we handle that case?
    /// </summary>
    [Test]
    public unsafe void CanUseReplayAppliance()
    {
      const string P1_NAME = "Joe";
      const string P2_NAME = "Archie";
      TestContext context = CreateTestContext(P1_NAME, P2_NAME);
      

      var ops = new GGPOClientOptions(0, REPLAY_APPLIANCE_PORT, Defaults.PROTOCOL_VERSION, SESSION_ID);
      ops.Callbacks = CreateDefaultCallbacks();

      var replayOps = new ReplayListenOptions()
      {

      };
      var blaster = new SimUdp(REPLAY_APPLIANCE_HOST, REPLAY_APPLIANCE_PORT, context.TimeSource, context.MsgQueue, SIM_PING, SIM_JITTER);
      var replayAppliance = new ReplayAppliance(ops, replayOps, blaster, context.TimeSource);

      context.SetReplayAppliance(replayAppliance);  

      // Each of the players will need to send their data to the replay appliance.
      var p1 = context.Player1Client;
      p1.AddReplayEndpoint(REPLAY_APPLIANCE_HOST, REPLAY_APPLIANCE_PORT);

      var p2 = context.Player2Client;
      p2.AddReplayEndpoint(REPLAY_APPLIANCE_HOST, REPLAY_APPLIANCE_PORT);


      // NOTE: Choose as few frames as possible to get the clients synced.
      const int STARTUP_FRAMES = 50;
      context.RunGame(STARTUP_FRAMES);

      Assert.That(replayAppliance.Errors.Count, Is.EqualTo(0), "There should be no listed errors!");

      // At this point we should have two connected clients on the replay appliance.
      Assert.That(replayAppliance.ClientCount, Is.EqualTo(2), "There should be two connected clients!");
      
      // Confirm that both of the endpoints are syned.
      var rc1 = replayAppliance.GetEndpoint(0);
      Assert.NotNull(rc1);

      var rc2 = replayAppliance.GetEndpoint(1);
      Assert.NotNull(rc2);

      Assert.That(rc1._current_state, Is.EqualTo(EClientState.Running), "Client 1 should be synced!");
      Assert.That(rc2._current_state, Is.EqualTo(EClientState.Running), "Client 2 should be synced!");

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
      const string P1_NAME = "Joe";
      const string P2_NAME = "Archie";

      TestContext context = CreateTestContext(P1_NAME, P2_NAME);

      var p1 = context.Player1;
      var p2 = context.Player2;

      var p1GGPO = context.Player1Client;
      var p2GGPO = context.Player2Client;

      const int MAX_FRAMES = 50;
      context.RunGame(MAX_FRAMES);

      // Here we can check to see if the players are synced or not...
      Assert.That(p1._current_state == EClientState.Running, "P1 should be listed as running!");
      Assert.That(p2._current_state == EClientState.Running, "P2 should be listed as running!");

      // TODO: consider this logic.  The player names should be exchanged on handshake...
      var p1l = p1GGPO.GetLocalPlayer();
      var p2l = p2GGPO.GetLocalPlayer();

      Assert.That(p2.GetPlayerName(), Is.EqualTo(P2_NAME));
      Assert.That(p1.GetPlayerName(), Is.EqualTo(P1_NAME));
    }




    // --------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// A convenient way to setup a test context, with two clients, replay appliance, etc.
    /// </summary>
    protected TestContext CreateTestContext(string p1Name, string p2Name)
    {

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
      //var p2 = p1GGPO.GetRemotePlayer();
      //var p1 = p2GGPO.GetRemotePlayer();

      var context = new TestContext(timeSource, testQueue, new[] { p1GGPO, p2GGPO }, new[] { ops1.InputBuffer, ops2.InputBuffer });

      return context;
    }
  }

}
