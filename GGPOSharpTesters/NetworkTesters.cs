using drewCo.Tools.Logging;
using GGPOSharp;

namespace GGPOSharpTesters
{

  // ==============================================================================================================================
  public class NetworkTesters : TestBase
  {


    // --------------------------------------------------------------------------------------------------------------------------
    [SetUp]
    public void Setup()
    { }

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
  }

}
