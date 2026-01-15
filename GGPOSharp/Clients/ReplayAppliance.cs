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
  internal class ReplayAppliance : GGPOClient
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
    private bool AllConnected = false;

    private Stopwatch Clock = default!;

    // OPTIONS:
    const int PLAYER_COUNT = 2;
    private ReplayEndpoint[] Endpoints = new ReplayEndpoint[PLAYER_COUNT];

    // --------------------------------------------------------------------------------------------------------------------------
    public ReplayAppliance(GGPOClientOptions ggpoOps_, ReplayListenOptions ops_)
      : base(ggpoOps_)
    {
      Options = ops_;


      RemoteIP = new IPEndPoint(IPAddress.Any, 0);
      RemoteEP = RemoteIP;

      Clock = Stopwatch.StartNew();

      for (int i = 0; i < Endpoints.Length; i++)
      {
        Endpoints[i] = null!;
      }
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
        ReplayPoll(timeout);
      }
      else
      {
        while (true)
        {
          int msgSize = UDP.Receive(ReceiveBuffer, ref RemoteEP);
          if (msgSize > 0)
          {
            var ipa = RemoteEP.Serialize();
            if (!this.ConnectedClients.Contains(ipa))
            {
              // We will add the remote endpoint here.....
              Log.Info("Got packet from remote... checking for session id...");

              UdpMsg msg = new UdpMsg();
              UdpMsg.FromBytes(ReceiveBuffer, ref msg, msgSize);

              if (msg.header.type == EMsgType.SyncRequest)
              {
                Log.Info($"Received a sync request with session id: {msg.u.sync_request.session_id}");

                // NOTE: We should have a sync request with the correct request ID set!
                // Don't know what to do if we don't... probably just ignore it...
                var rip = (IPEndPoint)RemoteEP;
                AddReplayEndpoint(rip.Address.ToString(), rip.Port, msg);

                Log.Info("A remote endpoint was added...");

                this.ConnectedClients.Add(ipa);
                if (this.ConnectedClients.Count == 2)
                {
                  AllConnected = true;
                  Log.Info("All clients are setup...");
                }


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



      //// this is where we can check the endpoint for data / connection?
      //base.DoPoll(timeout);
    }

    // --------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// This is modeled after the base class's DoPoll function.
    /// </summary>
    private void ReplayPoll(int timeout)
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
    private void AddReplayEndpoint(string remoteHost, int remotePort, UdpMsg msg)
    {
      var playerIndex = msg.u.sync_request.player_index;
      var ops = new GGPOEndpointOptions()
      {
        Delay = 0,
        IsLocal = false,
        PlayerIndex = playerIndex,
        PlayerName = "REPLAY_APP",
        RemoteHost = remoteHost,
        RemotePort = remotePort,
        Runahead = 0,
        TestOptions = new TestOptions()
      };


      var remote = new ReplayEndpoint(this, ops, _local_connect_status);
      this.Endpoints[msg.u.sync_request.player_index] = remote;
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
