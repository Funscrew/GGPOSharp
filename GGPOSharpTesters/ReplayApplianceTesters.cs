using GGPOSharp;
using GGPOSharp.Clients;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace GGPOSharpTesters
{

  // ==============================================================================================================================
  public class TestClient : IGGPOClient
  {
    public Stopwatch Clock { get; private set; } = Stopwatch.StartNew();
    public uint ClientVersion { get; private set; } = Defaults.PROTOCOL_VERSION;

    public IUdpBlaster UDP => throw new NotImplementedException();
  }

  // ==============================================================================================================================
  public class ReplayApplianceTesters
  {
    // --------------------------------------------------------------------------------------------------------------------------
    [SetUp]
    public void Setup()
    {
    }


    // --------------------------------------------------------------------------------------------------------------------------
    private static unsafe bool NoOp(byte* arg)
    {
      // TODO: Logging...
      return true;
    }
    // --------------------------------------------------------------------------------------------------------------------------
    private static void NoOp(int flags)
    {
      // TODO: Logging...
    }
    // --------------------------------------------------------------------------------------------------------------------------
    private static void NoOp(string gameName) { }

    // --------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// Show that we are able to create a replay appliance, and a client, and that we are able
    /// to sync the client with the appliance.  This is the first step in being able to send
    /// the replay data to the appliance.
    /// </summary>
    [Test]
    public unsafe void CanSyncReplayClientToAppliance()
    {
      const int RA_PORT = 7000;
      const UInt64 SESSION_ID = 12345;

      var clientOps = new GGPOClientOptions(GGPOConsts.REPLAY_APPLIANCE_PLAYER_INDEX, RA_PORT, Defaults.PROTOCOL_VERSION, SESSION_ID);
      clientOps.Callbacks = new GGPOSessionCallbacks()
      {
        free_buffer = NoOp,
        rollback_frame = NoOp,
        begin_game = NoOp,
      };

      // NOTE: Most of the options here are covered in GGPOClientOptions.  We should defer to those...
      var replayOps = new ReplayListenOptions()
      {

      };
      var udp = new UdpBlaster(clientOps.LocalPort);
      var appliance = new ReplayAppliance(clientOps, replayOps, udp);
      // Assert.Fail("do something!");

      var epOps = new GGPOEndpointOptions()
      {
        PlayerIndex = 0,
        PlayerName = "Joe",
        RemoteHost = "127.0.0.1",
        RemotePort = RA_PORT,
      };

      var testGGPO = new TestClient();
      var client = new ReplayClient(testGGPO, epOps, null);

      int x = 10;

    }

  }
}