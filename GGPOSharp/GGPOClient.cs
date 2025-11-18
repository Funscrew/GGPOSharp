using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography.X509Certificates;
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
  private List<GGPOEndpoint> _endpoints = new List<GGPOEndpoint>();
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
  public bool _synchronizing { get; private set; } = true;

  private Sync _sync = null!;

  ConnectStatus[] _local_connect_status = null!;

  private string[] _PlayerNames = new string[GGPOConsts.UDP_MSG_MAX_PLAYERS];
  private GGPOSessionCallbacks _callbacks;

  private int _next_recommended_sleep = 0;

  // ----------------------------------------------------------------------------------------
  public GGPOClient(GGPOClientOptions options_)
  {
    Options = options_;

    ValidateOptions();

    UdpClient = new UdpBlaster(Options.LocalPort);

    _local_connect_status = new ConnectStatus[GGPOConsts.UDP_MSG_MAX_PLAYERS];

    var ops = new SyncOptions()
    {
      callbacks = Options.Callbacks,
      input_size = Options.InputSize,
      num_players = Options.MaxPlayerCount,
      num_prediction_frames = GGPOConsts.MAX_PREDICTION_FRAMES
    };
    _sync = new Sync(_local_connect_status, ops);

    _callbacks = Options.Callbacks;

    // I'm implementing this out of a sense of tradition....
    // Not really sure if this matters here, or if this is even the best place to
    // fire this off.
    _callbacks.begin_game(string.Empty);
  }

  // ----------------------------------------------------------------------------------------
  private void ValidateOptions()
  {
    if (Options.LocalPlayerName.Length > GGPOConsts.MAX_NAME_SIZE)
    {
      throw new InvalidOperationException("Invalid player name!");
    }
    if (Options.Callbacks == null)
    {
      throw new InvalidOperationException("Callbacks are null!");
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

    var res = new GGPOEndpoint(this, ops, _local_connect_status);
    this._endpoints.Add(res);

    return res;
  }

  // ----------------------------------------------------------------------------------------
  internal void Idle()
  {
    DoPoll(1);
  }

  // ----------------------------------------------------------------------------------------
  public void DoPoll(int timeout)
  {
    // Endpoints get updated first so that we can get events, inputs, etc.
    int len = _endpoints.Count;
    for (int i = 0; i < len; i++)
    {
      _endpoints[i].RunFrame();
    }

    // Now we can handle the results of the endpoint updates (events, etc.)
    // Handle events!
    PollUdpProtocolEvents();

    if (!_synchronizing)
    {
      _sync.CheckSimulation(timeout);

      // notify all of our endpoints of their local frame number for their
      // next connection quality report
      int current_frame = _sync.GetFrameCount();

      int epCount = _endpoints.Count;
      for (int i = 0; i < epCount; i++)
      {
        _endpoints[i].SetLocalFrameNumber(current_frame);
      }

      int total_min_confirmed;
      if (_endpoints.Count == 1)
      {
        // We are connected to one other player....
        total_min_confirmed = Poll2Players(current_frame);
      }
      else
      {
        total_min_confirmed = PollNPlayers(current_frame);
      }

      Utils.Log("last confirmed frame in p2p backend is %d.", total_min_confirmed);
      if (total_min_confirmed >= 0)
      {
        Utils.ASSERT(total_min_confirmed != int.MaxValue);

        Utils.Log("setting confirmed frame in sync to %d.", total_min_confirmed);
        _sync.SetLastConfirmedFrame(total_min_confirmed);
      }

      // send timesync notifications if now is the proper time
      if (current_frame > _next_recommended_sleep)
      {
        int interval = 0;
        for (int i = 0; i < _endpoints.Count; i++)
        {
          interval = Math.Max(interval, _endpoints[i].RecommendFrameDelay());
        }

        if (interval > 0)
        {
          GGPOEvent info = new GGPOEvent();
          info.code = EEventCode.GGPO_EVENTCODE_TIMESYNC;
          info.u.timesync.frames_ahead = interval;
          _callbacks.on_event(ref info);
          _next_recommended_sleep = current_frame + GGPOConsts.RECOMMENDATION_INTERVAL;
        }
      }

      // NOTE: Not sure what that means.... we should use the timeout for the sleep value,
      // or we should not sleep it here?
      // XXX: this is obviously a farce...
      if (timeout > 0)
      {
        Thread.Sleep(1);
      }
    }
  }

  // ----------------------------------------------------------------------------------------------------------
  private unsafe int Poll2Players(int current_frame)
  {
    UInt16 i;

    // discard confirmed frames as appropriate
    int total_min_confirmed = int.MaxValue;
    for (i = 0; i < _endpoints.Count; i++)
    {
      bool queue_connected = true;
      if (_endpoints[i].IsRunning())
      {
        int ignore;
        queue_connected = _endpoints[i].GetPeerConnectStatus(i, &ignore);
      }
      if (!_local_connect_status[i].disconnected)
      {
        total_min_confirmed = Math.Min(_local_connect_status[i].last_frame, total_min_confirmed);
      }
      Utils.Log("  local endp: connected = %d, last_received = %d, total_min_confirmed = %d.\n", !_local_connect_status[i].disconnected, _local_connect_status[i].last_frame, total_min_confirmed);
      if (!queue_connected && !_local_connect_status[i].disconnected)
      {
        Utils.Log("disconnecting i %d by remote request.\n", i);
        DisconnectPlayer(i, total_min_confirmed);
      }
      Utils.Log("  total_min_confirmed = %d.\n", total_min_confirmed);
    }
    return total_min_confirmed;
  }


  // ----------------------------------------------------------------------------------------------------------
  private int PollNPlayers(int current_frame)
  {
    // I'm not really sure how we want to handle this in the future...
    throw new NotSupportedException("Only two players are currently supported!");
    //uint16 i, queue;
    //int last_received;

    //// discard confirmed frames as appropriate
    //int total_min_confirmed = MAX_INT;
    //for (queue = 0; queue < _num_players; queue++)
    //{
    //  bool queue_connected = true;
    //  int queue_min_confirmed = MAX_INT;
    //  Utils.Log("considering playerIndex %d.\n", queue);
    //  for (i = 0; i < _endpoints.Count; i++)
    //  {
    //    // we're going to do a lot of logic here in consideration of endpoint i.
    //    // keep accumulating the minimum confirmed point for all n*n packets and
    //    // throw away the rest.
    //    if (_endpoints[i].IsRunning())
    //    {
    //      bool connected = _endpoints[i].GetPeerConnectStatus((int)queue, &last_received);

    //      queue_connected = queue_connected && connected;
    //      queue_min_confirmed = Math.Min(last_received, queue_min_confirmed);
    //      Utils.Log("  endpoint %d: connected = %d, last_received = %d, queue_min_confirmed = %d.\n", i, connected, last_received, queue_min_confirmed);
    //    }
    //    else
    //    {
    //      Utils.Log("  endpoint %d: ignoring... not running.\n", i);
    //    }
    //  }
    //  // merge in our local status only if we're still connected!
    //  if (!_local_connect_status[queue].disconnected)
    //  {
    //    queue_min_confirmed = Math.Min(_local_connect_status[queue].last_frame, queue_min_confirmed);
    //  }
    //  Utils.Log("  local endp: connected = %d, last_received = %d, queue_min_confirmed = %d.\n", !_local_connect_status[queue].disconnected, _local_connect_status[queue].last_frame, queue_min_confirmed);

    //  if (queue_connected)
    //  {
    //    total_min_confirmed = Math.Min(queue_min_confirmed, total_min_confirmed);
    //  }
    //  else
    //  {
    //    // check to see if this disconnect notification is further back than we've been before.  If
    //    // so, we need to re-adjust.  This can happen when we detect our own disconnect at frame n
    //    // and later receive a disconnect notification for frame n-1.
    //    if (!_local_connect_status[queue].disconnected || _local_connect_status[queue].last_frame > queue_min_confirmed)
    //    {
    //      Utils.Log("disconnecting playerIndex %d by remote request.\n", queue);
    //      DisconnectPlayer(queue, queue_min_confirmed);
    //    }
    //  }
    //  Utils.Log("  total_min_confirmed = %d.\n", total_min_confirmed);
    //}
    //return total_min_confirmed;
  }

  // ----------------------------------------------------------------------------------------------------------
  public bool IncrementFrame()
  {
    Utils.Log("End of frame (%d)...", _sync.GetFrameCount());
    _sync.IncrementFrame();
    DoPoll(0);
    PollSyncEvents();

    return true;
  }

  // ----------------------------------------------------------------------------------------------------------
  private void PollSyncEvents()
  {
    SyncEvent e = new SyncEvent();
    while (_sync.GetEvent(ref e))
    {
      OnSyncEvent(e);
    }
    return;
  }

  // ----------------------------------------------------------------------------------------------------------
  // NOTE: Referefence implementation (p2p.cpp) does not implement this function
  // either.  I think that this is more stuff that was left out or not needed....
  private void OnSyncEvent(SyncEvent e)
  {
    throw new NotImplementedException();
  }

  // ----------------------------------------------------------------------------------------
  /// <summary>
  /// Sync the inputs for all players for the current frame.
  /// This sends the local inputs, receives the remote ones, and intiates any rollbacks if needed.
  /// </summary>
  public bool SyncInputs(in byte[] values, int isize, int playerCount)
  {
    if (_synchronizing) { return false; }


    // If we are rolling back, there is no need to attempt to add a local input.
    // The call will result in an error code anyway....
    if (!_sync.InRollback())
    {
      if (!AddLocalInput(values, isize)) { return false; }
    }

    // NOTE: We aren't doing anything with the flags... I think the system is probably using the event codes
    // to playerIndex this kind of thing......
    _sync.SynchronizeInputs(values, isize * playerCount);

    return true;

  }

  // ----------------------------------------------------------------------------------------
  private bool AddLocalInput(byte[] values, int isize)
  {

    // NOTE: When this function is called, we already know that we aren't in rollback!
    // REDUNDANT CHECK:
    if (_sync.InRollback())
    {
      return false;
    }
    // REDUNDANT CHECK:
    if (_synchronizing)
    {
      return false;
    }

    GameInput input = new GameInput();
    input.init(-1, values, isize);

    // Feed the input for the current frame into the synchronzation layer.
    if (!_sync.AddLocalInput(Options.PlayerIndex, ref input))
    {
      // return GGPO_ERRORCODE_PREDICTION_THRESHOLD;
      Utils.Log("Prediction threshold met!");
      return false;
    }

    if (input.frame != GameInput.NULL_FRAME)
    { // xxx: <- comment why this is the case
      // Update the local connect status state to indicate that we've got a
      // confirmed local frame for this player.  this must come first so it
      // gets incorporated into the next packet we send.

      // NOTE: All endpoints send out the _local_connect_status data with each message.
      // An ideal implemetation would have a single 'client' that we set this data on,
      // and then all endpoints would also be contained internally.
      Utils.Log("setting local connect status for local player %d to %d", Options.PlayerIndex, input.frame);
      _local_connect_status[Options.PlayerIndex].last_frame = input.frame;

      // Send the input to all the remote players.
      // NOTE: This queues input, and it gets pumped out later....
      // NOTE: In a two player game, only one of these endpoints has the 'udp' member set, and so
      // only one of them will actully do anything.....
      int epLen = _endpoints.Count;
      for (int i = 0; i < epLen; i++)
      {
        var ep = _endpoints[i];
        ep.SendInput(ref input);
      }
      // C++ Style:
      //for (int i = 0; i < _num_players; i++)
      //{
      //  if (_endpoints[i].IsInitialized())
      //  {
      //    _endpoints[i].SendInput(input);
      //  }
      //}
    }

    return true;
  }


  // ----------------------------------------------------------------------------------------
  // REFACTOR: 'HandleEvents'
  private void PollUdpProtocolEvents()
  {
    // throw new NotImplementedException();
    var evt = new Event();
    for (UInt16 i = 0; i < _endpoints.Count; i++)
    {
      var ep = _endpoints[i];
      while (ep.GetEvent(ref evt))
      {
        OnUdpProtocolPeerEvent(ref evt, i);
      }
    }

    //for (int i = 0; i < _num_players; i++)
    //{
    //  while (_endpoints[i].GetEvent(evt))
    //  {
    //    OnUdpProtocolPeerEvent(evt, i);
    //  }
    //}
  }

  // ----------------------------------------------------------------------------------------------------------
  void OnUdpProtocolPeerEvent(ref Event evt, UInt16 playerIndex)
  {
    // int playerIndex = -1;
    OnUdpProtocolEvent(ref evt, playerIndex);
    switch (evt.type)
    {
      case EEventType.Input:
        if (!_local_connect_status[playerIndex].disconnected)
        {

          int current_remote_frame = _local_connect_status[playerIndex].last_frame;
          int new_remote_frame = evt.u.input.frame;
          Utils.ASSERT(current_remote_frame == -1 || new_remote_frame == (current_remote_frame + 1));

          _sync.AddRemoteInput(playerIndex, ref evt.u.input);
          // Notify the other endpoints which frame we received from a peer
          Utils.Log("setting remote connect status for playerIndex %d to %d", playerIndex, evt.u.input.frame);
          _local_connect_status[playerIndex].last_frame = evt.u.input.frame;
        }
        break;

      case EEventType.Disconnected:
        DisconnectPlayer(playerIndex);
        break;

    }
  }

  // ----------------------------------------------------------------------------------------------------------
  bool DisconnectPlayer(UInt16 playerIndex)
  {
    UInt16 queue = playerIndex;
    //	GGPOErrorCode result;

    // if (player > MAX_PLA
    //result = PlayerHandleToQueue(player, &playerIndex);
    //if (!GGPO_SUCCEEDED(result)) {
    //	return result;
    //}

    if (_local_connect_status[queue].disconnected)
    {
      // TODO: Log this !
      return false; //GGPO_ERRORCODE_PLAYER_DISCONNECTED;
    }

    if (!_endpoints[queue].IsInitialized())
    {
      int current_frame = _sync.GetFrameCount();
      // xxx: we should be tracking who the local player is, but for now assume
      // that if the endpoint is not initalized, this must be the local player.
      Utils.Log("Disconnecting local player %d at frame %d by user request.", queue, _local_connect_status[queue].last_frame);
      int epLen = _endpoints.Count;
      for (UInt16 i = 0; i < epLen; i++)
      {
        if (_endpoints[i].IsInitialized())
        {
          DisconnectPlayer(i, current_frame);
        }
      }
    }
    else
    {
      Utils.Log("Disconnecting playerIndex %d at frame %d by user request.", queue, _local_connect_status[queue].last_frame);
      DisconnectPlayer(queue, _local_connect_status[queue].last_frame);
    }

    return true;
  }


  // --------------------------------------------------------------------------------------------------------------
  void DisconnectPlayer(int playerIndex, int syncto)
  {
    GGPOEvent info = new GGPOEvent();
    int framecount = _sync.GetFrameCount();

    _endpoints[playerIndex].Disconnect();

    Utils.Log("Changing playerIndex %d local connect status for last frame from %d to %d on disconnect request (current: %d).",
      playerIndex, _local_connect_status[playerIndex].last_frame, syncto, framecount);

    _local_connect_status[playerIndex].disconnected = true;
    _local_connect_status[playerIndex].last_frame = syncto;

    if (syncto < framecount)
    {
      Utils.Log("adjusting simulation to account for the fact that %d disconnected @ %d.", playerIndex, syncto);
      _sync.AdjustSimulation(syncto);
      Utils.Log("finished adjusting simulation.");
    }

    info.code = EEventCode.GGPO_EVENTCODE_DISCONNECTED_FROM_PEER;
    info.u.disconnected.player_index = playerIndex;
    _callbacks.on_event(ref info);

    CheckInitialSync();
  }

  // ----------------------------------------------------------------------------------------------------------
  void OnUdpProtocolEvent(ref Event evt, int playerIndex)
  {
    GGPOEvent info = new GGPOEvent();

    switch (evt.type)
    {
      case EEventType.Connected:
        info.code = EEventCode.GGPO_EVENTCODE_CONNECTED_TO_PEER;
        info.u.connected.player_index = playerIndex;

        _PlayerNames[playerIndex] = evt.u.connected.GetText();
        // strcpy_s(_PlayerNames[playerIndex], evt.u.connected.playerName);

        // strcpy_s(info.u.connected.playerName, evt.u.connected.playerName);

        _callbacks.on_event(ref info);
        break;
      case EEventType.Synchronizing:
        info.code = EEventCode.GGPO_EVENTCODE_SYNCHRONIZING_WITH_PEER;
        info.u.synchronizing.player_index = playerIndex;
        info.u.synchronizing.count = evt.u.synchronizing.count;
        info.u.synchronizing.total = evt.u.synchronizing.total;
        _callbacks.on_event(ref info);
        break;
      case EEventType.Synchronized:
        info.code = EEventCode.GGPO_EVENTCODE_SYNCHRONIZED_WITH_PEER;
        info.u.synchronized.player_index = playerIndex;
        _callbacks.on_event(ref info);

        CheckInitialSync();
        break;

      case EEventType.NetworkInterrupted:
        info.code = EEventCode.GGPO_EVENTCODE_CONNECTION_INTERRUPTED;
        info.u.connection_interrupted.player_index = playerIndex;
        info.u.connection_interrupted.disconnect_timeout = evt.u.network_interrupted.disconnect_timeout;
        _callbacks.on_event(ref info);
        break;

      case EEventType.NetworkResumed:
        info.code = EEventCode.GGPO_EVENTCODE_CONNECTION_RESUMED;
        info.u.connection_resumed.player_index = playerIndex;
        _callbacks.on_event(ref info);
        break;

      case EEventType.ChatCommand:

        // char[] text = new char[GGPOConsts.MAX_GGPOCHAT_SIZE + 1];
        var userName = _PlayerNames[playerIndex];
        string text = evt.u.chat.GetText();

        // evt.u.chat.SetText(text);
        // strcpy_s(text, evt.u.chat.text);

        info.code = EEventCode.GGPO_EVENTCODE_CHATCOMMAND;
        info.u.chat.SetUsername(userName);
        info.u.chat.SetText(text);

        _callbacks.on_event(ref info);

        break;
    }
  }

  // ----------------------------------------------------------------------------------------------------------
  void CheckInitialSync()
  {
    int i;

    if (_synchronizing)
    {
      // Check to see if everyone is now synchronized.  If so,
      // go ahead and tell the client that we're ok to accept input.
      int epLen = _endpoints.Count;
      for (i = 0; i < epLen; i++)
      {
        var ep = _endpoints[i];
        // xxx: IsInitialized() must go... we're actually using it as a proxy for "represents the local player"
        if (ep.IsInitialized() && !ep.IsSynchronized() && !_local_connect_status[i].disconnected)
        {
          return;
        }
      }

      GGPOEvent info = new GGPOEvent();
      info.code = EEventCode.GGPO_EVENTCODE_RUNNING;
      _callbacks.on_event(ref info);
      _synchronizing = false;
    }
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
  private const int DEFAULT_INPUT_SIZE = 5;   // This is for 3rd strike.
  private const int MAX_PLAYER_COUNT = 4;

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
  public int InputSize { get; set; } = DEFAULT_INPUT_SIZE;
  public int MaxPlayerCount { get; set; } = MAX_PLAYER_COUNT;
  public GGPOSessionCallbacks Callbacks { get; set; } = null!;
}

// ==========================================================================================
public static class Defaults
{
  public const int LOCAL_PORT = 7001;
  public const int REMOTE_PORT = 7000;
  public const string REMOTE_HOST = "127.0.0.1";
}


