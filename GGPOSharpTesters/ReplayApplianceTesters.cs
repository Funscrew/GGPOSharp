using GGPOSharp;
using GGPOSharp.Clients;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;

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
      const int RA_PORT = 7002;
      const UInt64 SESSION_ID = 12345;

      // This is how we actually move the messages around....
      var testQueue = new TestMessageQueue();


      // So we have the basics all setup, but there needs to be some streamlining in terms of
      // being able to have the SimUdp devices sending + receiving to the correct places.
      // We might be able to use the 'SocketAddress useRemote' argument in IUdpBlaster.Send
      // to get the actual, correct remote address, but that will need a bit of research.
      // All the same, I need to review how we setup + deal with the remote/local ports, just so
      // that I know.
      // I may want to look into a better way to setup the appliance + its clients so that there
      // are less options, etc. flying all over the place.
      throw new NotImplementedException();
      


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

      Assert.Fail("complete me!");
      //var testUdp =  new SimUdp("test", 
      //var testGGPO = new TestClient(Defaults.LOCAL_PORT, testUdp);
      //var client = new ReplayClient(testGGPO, epOps, null);


      // Now that the appliance + clients are setup, we need to get them to send / receive messages.
      // Because we are testing, I don't think that we need to go through the network, and
      // can probably save a lot of time by simulating a PERFECT UDP network.  Protocol robustness can
      // then be tested by adding lag, out of order packets (OOP) and dropped packets.

      // So if we want to simulate the netowrk we need:
      // - One or more clients.  In this case two.
      // - The clients will send / receive at certain time intervals.  In the case
      // of the emulator, or a videogame, we send/receive at intervals of 1/60sec (60FPS)
      // so I guess that means each 'client' will have a send / receive queue, and those messages will have
      // some kind of a timestamp so that we can properly simulate ping / lag, etc.
      // --> Our PERFECT network will stick to the timestamps that we define.. when quality
      // testing we can take one of our playbacks and adjust the timestamps to introduce lag/jitter/etc.


      // So then we have a list of all of the 'sent' packets and the time that they should be received.
      // In the case of the packets being sent by the code after being recieved, we will have to have some kind
      // of way to add 'receive' times on them using whatever the ping time is set to, and then some
      // kind of jitter....
    }

  }
}