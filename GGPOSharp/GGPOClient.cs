using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GGPOSharp;

// ==========================================================================================
/// <summary>
/// Main client that is used to connect to one or more other players ove the network.
/// </summary>
public class GGPOClient
{
  private GGPOClientOptions Options = null!;
  private List<GGPOEndpoint> Endpoints = new List<GGPOEndpoint>();
  internal UdpBlaster UdpClient = null!;

  public string PlayerName { get { return Options.LocalPlayerName; } }

  /// <summary>
  /// Indicates that the client is officially started, and no new connections can be added.
  /// </summary>
  private bool IsLocked = false;

  /// <summary>
  /// Are we currently processing a rollback?
  /// </summary>
  private bool InRollback = false;
  private bool _synchronizing = false;

  // ----------------------------------------------------------------------------------------
  public GGPOClient(GGPOClientOptions options_)
  {
    Options = options_;

    ValidateOptions();

    UdpClient = new UdpBlaster(Options.LocalPort);
  }

  // ----------------------------------------------------------------------------------------
  private void ValidateOptions()
  {
    if (Options.LocalPlayerName.Length > GGPOConsts.MAX_NAME_SIZE)
    {
      throw new InvalidOperationException("Invalid player name!");
    }
  }

  // ----------------------------------------------------------------------------------------
  public GGPOEndpoint AddRemote(string remoteHost, int remotePort, TestOptions? testOptions = null)
  {
    if (IsLocked)
    {
      throw new InvalidOperationException("New connections are not allowed at this time!");
    }

    var ops = new GGPOEndpointOptions()
    {
      RemoteHost = remoteHost,
      RemotePort = remotePort,
      TestOptions = testOptions ?? new TestOptions()
    };

    var res = new GGPOEndpoint(this, ops);
    this.Endpoints.Add(res);

    return res;
  }

  // ----------------------------------------------------------------------------------------
  public void DoPoll()
  {
    // Endpoints get updated first so that we can get events, inputs, etc.
    int len = Endpoints.Count;
    for (int i = 0; i < len; i++)
    {
      Endpoints[i].RunFrame();
    }

    // Now we can handle the results of the endpoint updates (events, etc.)
    // Handle events!
    PollUdpProtocolEvents();


    //// Original, sync code, etc.
    //if (!_synchronizing)
    //{
    //  _sync.CheckSimulation(timeout);

    //  // notify all of our endpoints of their local frame number for their
    //  // next connection quality report
    //  int current_frame = _sync.GetFrameCount();
    //  for (int i = 0; i < _num_players; i++)
    //  {
    //    _endpoints[i].SetLocalFrameNumber(current_frame);
    //  }

    //  int total_min_confirmed;
    //  if (_num_players <= 2)
    //  {
    //    total_min_confirmed = Poll2Players(current_frame);
    //  }
    //  else
    //  {
    //    total_min_confirmed = PollNPlayers(current_frame);
    //  }

    //  Log("last confirmed frame in p2p backend is %d.\n", total_min_confirmed);
    //  if (total_min_confirmed >= 0)
    //  {
    //    ASSERT(total_min_confirmed != INT_MAX);

    //    Log("setting confirmed frame in sync to %d.\n", total_min_confirmed);
    //    _sync.SetLastConfirmedFrame(total_min_confirmed);
    //  }

    //  // send timesync notifications if now is the proper time
    //  if (current_frame > _next_recommended_sleep)
    //  {
    //    int interval = 0;
    //    for (int i = 0; i < _num_players; i++)
    //    {
    //      interval = MAX(interval, _endpoints[i].RecommendFrameDelay());
    //    }

    //    if (interval > 0)
    //    {
    //      GGPOEvent info;
    //      info.code = GGPO_EVENTCODE_TIMESYNC;
    //      info.u.timesync.frames_ahead = interval;
    //      _callbacks.on_event(&info);
    //      _next_recommended_sleep = current_frame + RECOMMENDATION_INTERVAL;
    //    }
    //  }
    //  // XXX: this is obviously a farce...
    //  if (timeout)
    //  {
    //    Sleep(1);
    //  }
    //}
  }

  // ----------------------------------------------------------------------------------------
  /// <summary>
  /// Sync the inputs for all players for the current frame.
  /// This sends the local inputs, and receives the remote ones.
  /// </summary>
  public bool SyncInputs(in GameInput input)
  {
    if (_synchronizing) { return false; }


    // If we are rolling back, there is no need to attempt to add a local input.
    // The call will result in an error code anyway....
    if (!_sync.InRollback())
    {
      if (!AddLocalInput(input)) { return false; }
    }

    // NOTE: We aren't doing anything with the flags... I think the system is probably using the event codes
    // to playerIndex this kind of thing......
    _sync.SynchronizeInputs(values, isize * playerCount);

    return true;

  }

  // ----------------------------------------------------------------------------------------
  private bool AddLocalInput(in GameInput input)
  {
    throw new NotImplementedException();
  }


  // ----------------------------------------------------------------------------------------
  // REFACTOR: 'HandleEvents'
  private void PollUdpProtocolEvents()
  {
    // throw new NotImplementedException();
  }

  // ----------------------------------------------------------------------------------------
  /// <summary>
  /// Disallow new remotes / connections from being added.
  /// It is recommended that you call this after all endpoints are setup and you don't have a
  /// good reason to reconnect them.
  /// </summary>
  public void Lock()
  {
    IsLocked = true;
  }



}

// ==========================================================================================
public class GGPOClientOptions
{

  // ----------------------------------------------------------------------------------------
  public GGPOClientOptions(int playerIndex_, string localPlayerName_, int localPort_)
  {
    PlayerIndex = playerIndex_;
    LocalPlayerName = localPlayerName_;
    LocalPort = localPort_;
  }

  /// <summary>
  /// Index of the player, coresponding to 0 == player 1, 1 == player 2, etc.
  /// </summary>
  public int PlayerIndex { get; set; }
  public string LocalPlayerName { get; set; }
  public int LocalPort { get; set; } = Defaults.LOCAL_PORT;

}

// ==========================================================================================
public static class Defaults
{
  public const int LOCAL_PORT = 7001;
  public const int REMOTE_PORT = 7000;
  public const string REMOTE_HOST = "127.0.0.1";
}


