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

    // --------------------------------------------------------------------------------------------------------------------------
    public ReplayAppliance(GGPOClientOptions ggpoOps_, ReplayListenOptions ops_)
      : base(ggpoOps_)
    {
      Options = ops_;


      RemoteIP = new IPEndPoint(IPAddress.Any, 0);
      RemoteEP = RemoteIP;

      Clock = Stopwatch.StartNew();


    }

    // --------------------------------------------------------------------------------------------------------------------------
    private void SetupEndpoints()
    {
      //// This is so we can get data from whatever remote....
      //RemoteIP = new IPEndPoint(IPAddress.Any, 0);
      //RemoteEP = RemoteIP;

      //// this.AddRemotePlayer(
      //var remoteOps = new GGPOEndpointOptions()
      //{
      //  IsLocal = true,
      //  PlayerIndex = GGPOConsts.REPLAY_APPLIANCE_PLAYER_INDEX,
      //  TestOptions = new TestOptions()
      //};
      //var remote = new GGPOEndpoint(this, remoteOps, _local_connect_status);

      //this._endpoints.Add(remote);
      //// OK... so we kind of need to have our own special endpoint for this.
      //// Would be a lot easier if we could just have a stupid soft-router....
      //// Well.... actually we can.... sort of, all I need are the messages and I am recording them... that is all....
      //// Of course, we have to do all the handshaking stuff toooo....

      //// throw new NotImplementedException();
    }

    // --------------------------------------------------------------------------------------------------------------------------
    public override void DoPoll(int timeout)
    {
      if (Options.StartupTimeout != -1 && Clock.ElapsedMilliseconds > Options.StartupTimeout)
      {
        throw new InvalidOperationException("Startup timeout exceeded!");
      }

      if (AllConnected)
      {
        base.DoPoll(timeout);
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
              }
              else
              {
                Log.Info("The message should be a sync request!");
              }

              // NOTE: We should have a sync request with the correct request ID set!
              // Don't know what to do if we don't... probably just ignore it...

              this.ConnectedClients.Add(ipa);
              if (this.ConnectedClients.Count == 2)
              {
                AllConnected = true;
                Log.Info("All clients are setup...");
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
