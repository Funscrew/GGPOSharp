using drewCo.Tools;
using drewCo.Tools.Logging;
using System.Diagnostics;
using System.Net;

namespace GGPOSharp.Clients
{

  // ==============================================================================================================================
  /// <summary>
  /// This is the class that will be responsible for receiving and logging input data from
  /// two or more players.
  /// </summary>
  public class ReplayAppliance : GGPOClient
  {
    private ReplayListenOptions ReplayOptions = default!;

    // FROM: GGPOEndpoint
    private IPEndPoint RemoteIP;
    private EndPoint RemoteEP;

    // The two clients that we expect to receive data from.  These will be the remote endpoints that we
    // then set up.
    private HashSet<SocketAddress> ConnectedClients = new HashSet<SocketAddress>();
    private List<GGPOEndpoint> Endpoints = new List<GGPOEndpoint>();
    private bool AllConnected = false;
    private List<int> ConnectedPlayerIndexes = new List<int>();
    public int ClientCount { get { return this.ConnectedClients.Count; } }

    public List<string> Errors { get; private set; } = new List<string>();

    /// <summary>
    /// Certain endpoints are blacklisted if they send bad player / session ids.
    /// </summary>
    private HashSet<SocketAddress> Blacklisted = new HashSet<SocketAddress>();


    private Stopwatch Clock = default!;

    private GameRecorder Recorder = null!;

    // --------------------------------------------------------------------------------------------------------------------------
    public ReplayAppliance(GGPOClientOptions ggpoOps_, ReplayListenOptions ops_, IUdpBlaster udp_, SimTimer clock_)
      : base(ggpoOps_, udp_, clock_)
    {
      ReplayOptions = ops_;

      // Validate options:
      if (ReplayOptions.SessionId == 0) { throw new InvalidOperationException("Invalid session id!"); }

      RemoteIP = new IPEndPoint(IPAddress.Any, 0);
      RemoteEP = RemoteIP;

      Clock = Stopwatch.StartNew();

      InitGameRecorder();
    }

    // --------------------------------------------------------------------------------------------------------------------------
    private void InitGameRecorder()
    {
      if (string.IsNullOrWhiteSpace(ReplayOptions.GameName))
      {
        throw new InvalidOperationException("Invalid game name!");
      }
      if (string.IsNullOrWhiteSpace(ReplayOptions.GameVersion))
      {
        throw new InvalidOperationException("Invalid game version!");
      }

      Recorder = new GameRecorder(new GameData()
      {
        GameName = ReplayOptions.GameName,
        GameVersion = ReplayOptions.GameVersion,
        PlayerCount = ClientOptions.MaxPlayerCount,
        TotalInputSize = ClientOptions.InputSize * ClientOptions.MaxPlayerCount
      },
      ReplayOptions.DataDir,
      ClientOptions.SessionId
      );
    }


    // --------------------------------------------------------------------------------------------------------------------------
    public GGPOEndpoint GetEndpoint(int index)
    {
      return _endpoints[index];
    }

    // --------------------------------------------------------------------------------------------------------------------------
    protected override void DeliverMessage(ref UdpMsg msg, int received, EndPoint receivedFrom)
    {
      // NOTE: This is going to make garbage.... lame!
      SocketAddress ipa = receivedFrom.Serialize();
      if (msg.header.type == EMsgType.SyncRequest && !this.ConnectedClients.Contains(ipa))
      {
        int index = ConnectedClients.Count;
        var ep = ConnectNewClient(ref msg, ipa);
        Endpoints.Add(ep);
      }

      // Now that the end
      base.DeliverMessage(ref msg, received, receivedFrom);

    }

    // --------------------------------------------------------------------------------------------------------------------------
    private GGPOEndpoint ConnectNewClient(ref UdpMsg msg, SocketAddress ipa)
    {
      // JFC can we make this any more of a pain in the ass?
      // TODO: This will probably go away when we fix how we represent this stuff....
      // Also, this won't work with IPV6, booooo
      var bufferData = ipa.Buffer.ToArray();
      byte[] port = new byte[2];
      port[0] = bufferData[3];
      port[1] = bufferData[2];
      var remotePort = BitConverter.ToUInt16(port);
      string remoteHost = $"{bufferData[4]}.{bufferData[5]}.{bufferData[6]}.{bufferData[7]}";

      // Make sure that session id + player index are correct....
      var sid = msg.u.sync_request.session_id;
      if (sid != ReplayOptions.SessionId)
      {
        // We don't want to receive from this endpoint anymore.....
        // How can we block receiving?
        AddError("Connection attempt with invalid session id! [adding to blacklist]");
        UDP.AddToBlacklist(ipa);
        return null;
      }

      // We also want to check to see if we are getting the correct player index.
      // NOTE: If a certain player index is already connected, then we want to
      // reject those other connections that are reporting the wrong one!
      var pi = msg.u.sync_request.player_index;
      if (ConnectedPlayerIndexes.Contains(pi))
      {
        AddError($"The player with index: {pi} has already been connected! [adding to blacklist]");
        UDP.AddToBlacklist(ipa);
        return null;
      }

      // NOTE: We should have a sync request with the correct request ID set!
      // Don't know what to do if we don't... probably just ignore it...
      var rip = (IPEndPoint)RemoteEP;
      var newEndpoint = AddReplayEndpoint(remoteHost, remotePort, msg);

      Log.Info("A remote endpoint was added...");

      this.ConnectedClients.Add(ipa);
      if (this.ConnectedClients.Count == 2)
      {
        AllConnected = true;
        Log.Info("All clients are setup...");
      }

      return newEndpoint;

      // Send the sync reply, immediately.
      // newEndpoint.OnSyncRequest(ref msg, received);

    }

    // --------------------------------------------------------------------------------------------------------------------------
    protected override int PollPlayers(int current_frame)
    {
      // Replay appliance doesn't really do anything at this point, tho maybe this is where
      // we do stuff like confim inputs or whatever.....?
      // return base.PollPlayers(current_frame);
      return current_frame;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    private void AddError(string msg)
    {
      Log.Error(msg);
      this.Errors.Add(msg);
    }

    // --------------------------------------------------------------------------------------------------------------------------
    protected override void CheckInitialSync()
    {
      if (_synchronizing)
      {
        int epLen = _endpoints.Count;
        if (epLen < 2) { return; }

        for (int i = 0; i < epLen; i++)
        {
          var ep = _endpoints[i];
          if (!ep.IsSynchronized() && !_local_connect_status[ep.PlayerIndex].disconnected)
          {
            return;
          }
        }

        GGPOEvent info = new GGPOEvent();
        info.event_code = EEventCode.GGPO_EVENTCODE_RUNNING;
        _callbacks.on_event(ref info);
        _synchronizing = false;
      }
    }

    // --------------------------------------------------------------------------------------------------------------------------
    private GGPOEndpoint AddReplayEndpoint(string remoteHost, int remotePort, UdpMsg msg)
    {
      if (remoteHost == "0.0.0.0") { throw new InvalidOperationException("Invalid host!"); }
      if (remotePort == 0) { throw new InvalidOperationException("Invalid port!"); }

      var playerIndex = msg.u.sync_request.player_index;
      var ops = new GGPOEndpointOptions()
      {
        Delay = 0,
        IsLocal = false,
        PlayerIndex = playerIndex, // GGPOConsts.REPLAY_APPLIANCE_PLAYER_INDEX,
        PlayerName = "REPLAY_APP",
        RemoteHost = remoteHost,
        RemotePort = remotePort,
        Runahead = 0,
        IsReplayClient = true,
        TestOptions = new TestOptions()
      };

      // NOTE: We may not want to send out the sync request immediately on these endpoints?
      // Nah -> it should be OK that they bounce around.....
      var remote = new ReplayEndpoint(this, ops, _local_connect_status);

      this._endpoints.Add(remote);
      ConnectedPlayerIndexes.Add(playerIndex);

      return remote;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public override bool SyncInput(in byte[] values, int isize, int maxPlayers)
    {
      // TODO: Maybe this is where we merge + ACK inputs?
      return true;
    }

    // --------------------------------------------------------------------------------------------------------------------------
    protected override bool AddLocalInput(byte[] values, int isize)
    {
      // Do nothing, we don't have local inputs!
      return true;
    }


    // --------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// This is where the inputs for the different frames will get merged, recorded, and later sent out.
    /// </summary>
    // private bool _WarningSent = false;
    internal void MergeInput(ref GameInput input, int playerIndex)
    {
      if (Recorder.HasError)
      {
        int x = 10;
        // We have detected an error in the recorder.  We will log this, and send disconnect
        // notices to all active clients.
        Log.Error($"There was a recording error: {Recorder.ErrorReason} : {Recorder.ErrorMessage}");
        DisconnectAll();
      }
      Recorder.AddInput(playerIndex, ref input);
    }

    // --------------------------------------------------------------------------------------------------------------------------
    protected virtual void DisconnectAll()
    {
      for (int i = 0; i < this.ClientCount; i++)
      {
        this.Endpoints[i].Disconnect();
      }
      this.AllConnected = false;
      this.Endpoints.Clear();
      this.ConnectedPlayerIndexes.Clear();

      this.IsDisconnected = true;
    }
  }

}
