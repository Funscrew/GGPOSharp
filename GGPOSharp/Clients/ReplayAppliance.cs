using drewCo.Tools.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace GGPOSharp.Clients
{

  // ==============================================================================================================================
  /// <summary>
  /// This is the class that will be responsible for receiving and logging input data from
  /// two or more players.
  /// </summary>
  public class ReplayAppliance : GGPOClient
  {
    private ReplayListenOptions Options = default!;

    // FROM: GGPOEndpoint
    private IPEndPoint RemoteIP;
    private EndPoint RemoteEP;
    private SocketAddress UseRemote;
    private byte[] ReceiveBuffer = new byte[8192];

    // The two clients that we expect to receive data from.  These will be the remote endpoints that we
    // then set up.
    private HashSet<SocketAddress> ConnectedClients = new HashSet<SocketAddress>();

    public List<string> Errors { get; private set; } = new List<string>();

    /// <summary>
    /// Certain endpoints are blacklisted if they send bad player / session ids.
    /// </summary>
    private HashSet<SocketAddress> Blacklisted = new HashSet<SocketAddress>();

    private List<int> ConnectedPlayerIndexes = new List<int>();

    private bool AllConnected = false;

    private Stopwatch Clock = default!;

    // OPTIONS:
    const int PLAYER_COUNT = 2;
    private ReplayEndpoint[] Endpoints = new ReplayEndpoint[PLAYER_COUNT];

    // TODO: This is something we will care about later....
    // private List<SpectateEndpoints> Spectators = new List<SpectateEndpoints>();

    // --------------------------------------------------------------------------------------------------------------------------
    public ReplayAppliance(GGPOClientOptions ggpoOps_, ReplayListenOptions ops_, IUdpBlaster udp_, SimTimer clock_)
      : base(ggpoOps_, udp_, clock_)
    {
      Options = ops_;

      // Validate options:
      if (Options.SessionId == 0 ) { throw new InvalidOperationException("Invalid session id!"); }

      RemoteIP = new IPEndPoint(IPAddress.Any, 0);
      RemoteEP = RemoteIP;

      Clock = Stopwatch.StartNew();

      for (int i = 0; i < Endpoints.Length; i++)
      {
        Endpoints[i] = null!;
      }
    }

    public int ClientCount { get { return this.ConnectedClients.Count; } }

    // --------------------------------------------------------------------------------------------------------------------------
    public ReplayEndpoint GetEndpoint(int index)
    {
      return Endpoints[index];
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public override void DoPoll(int timeout)
    {
      if (Options.StartupTimeout != -1 && Clock.ElapsedMilliseconds > Options.StartupTimeout)
      {
        throw new InvalidOperationException("Startup timeout exceeded!");
      }

      if (this.ConnectedClients.Count > 0) // AllConnected)
      {
        ReplayPoll();
      }

      // Look for new connections.
      if (this.ConnectedClients.Count < 2)
      {
        while (true)
        {
          int msgLen = UDP.Receive(ReceiveBuffer, ref RemoteEP);
          if (msgLen > 0)
          {
            var ipa = RemoteEP.Serialize();
            if (!this.ConnectedClients.Contains(ipa))
            {
              // We will add the remote endpoint here.....
              Log.Info("Got packet from remote... checking for session id...");

              UdpMsg msg = new UdpMsg();
              UdpMsg.FromBytes(ReceiveBuffer, ref msg, msgLen);

              if (msg.header.type == EMsgType.SyncRequest)
              {
               // Log.Info($"Received a sync request with session id: {msg.u.sync_request.session_id}");

                // Make sure that session id + player index are correct....
                var sid = msg.u.sync_request.session_id;
                if (sid != Options.SessionId)
                {
                  // We don't want to receive from this endpoint anymore.....
                  // How can we block receiving?
                  AddError("Connection attempt with invalid session id! [adding to blacklist]");
                  UDP.AddToBlacklist(ipa);
                  return;
                }

                // We also want to check to see if we are getting the correct player index.
                // NOTE: If a certain player index is already connected, then we want to
                // reject those other connections that are reporting the wrong one!
                var pi = msg.u.sync_request.player_index;
                if (ConnectedPlayerIndexes.Contains(pi)) {
                  AddError($"The player with index: {pi} has already been connected! [adding to blacklist]");
                  UDP.AddToBlacklist(ipa);
                  return;
                }

                // NOTE: We should have a sync request with the correct request ID set!
                // Don't know what to do if we don't... probably just ignore it...
                var rip = (IPEndPoint)RemoteEP;
                var rep = AddReplayEndpoint(rip.Address.ToString(), rip.Port, msg);

                Log.Info("A remote endpoint was added...");

                this.ConnectedClients.Add(ipa);
                if (this.ConnectedClients.Count == 2)
                {
                  AllConnected = true;
                  Log.Info("All clients are setup...");
                }

                // Send the sync reply, immediately.
                rep.OnSyncRequest(ref msg, msgLen);

              }
              else
              {
                Log.Info("The message should be a sync request!");
              }

            }

          }
          else
          {
            // NO MORE DATA
            break;
          }
        }
      }
      //else
      //{
      //}

      //// this is where we can check the endpoint for data / connection?
      //base.DoPoll(timeout);
    }

    // --------------------------------------------------------------------------------------------------------------------------
    private void AddError(string msg)
    {
      this.Errors.Add(msg);
    }

    // --------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// This is modeled after the base class's DoPoll function.
    /// </summary>
    private void ReplayPoll()
    {
      //base.DoPoll(timeout);

      // Endpoints get updated first so that we can get events, inputs, etc.
      int epCount = Endpoints.Length;
      for (int i = 0; i < epCount; i++)
      {
        var ep = Endpoints[i];
        if (ep != null)
        {
          ep.OnLoopPoll();
        }
      }

      // Now we can handle the results of the endpoint updates (events, etc.)
      // Handle events!
      PollUdpProtocolEvents();


      // This is where we will check the sync + the input queues to 

      // Get inputs from all connected clients.
      // Do the merge.
      // NOTE: This is where we may receive redundant inputs if the previous ACKS got lost or whatever...
      // That is OK, we will just plow over them..
      // Send out the ACKS.

      // If there are connected live spectators, send them the latest set of settled inputs.
      // TODO: This is something that will happen way later!

      ///throw new NotImplementedException();

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
      var playerIndex = msg.u.sync_request.player_index;
      var ops = new GGPOEndpointOptions()
      {
        Delay = 0,
        IsLocal = false,
        PlayerIndex = GGPOConsts.REPLAY_APPLIANCE_PLAYER_INDEX,
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
      this.Endpoints[playerIndex] = remote;

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

  }
}
