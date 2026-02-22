using drewCo.Tools.Logging;
using GGPOSharp;

namespace GGPOSharpTesters
{

  // ==============================================================================================================================
  public class TestBase
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