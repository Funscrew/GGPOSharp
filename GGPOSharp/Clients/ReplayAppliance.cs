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

    // --------------------------------------------------------------------------------------------------------------------------
    public ReplayAppliance(GGPOClientOptions ggpoOps_, ReplayListenOptions ops_, IUdpBlaster udp_, SimTimer clock_)
      : base(ggpoOps_, udp_, clock_)
    {
      Options = ops_;

      // Validate options:
      if (Options.SessionId == 0) { throw new InvalidOperationException("Invalid session id!"); }

      RemoteIP = new IPEndPoint(IPAddress.Any, 0);
      RemoteEP = RemoteIP;

      Clock = Stopwatch.StartNew();

    }

    public int ClientCount { get { return this.ConnectedClients.Count; } }

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
      if (sid != Options.SessionId)
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
    private bool _WarningSent = false;
    internal void MergeInput(ref GameInput input, int playerIndex)
    {
      if (!_WarningSent)
      {
        Log.Warning("Input merging is currently unsupported!");
        _WarningSent = true;
      }
      // For now, we will do nothing....
      // Debug.
      // Log.wa
      // throw new NotImplementedException();
    }
  }



  // ==============================================================================================================================
  /// <summary>
  /// This is the thing that records inputs, etc. for a game.
  /// Thre recorded files can then be used for replay, and in some cases, direct streaming.
  /// </summary>
  public class GameRecorder : IDisposable
  {
    private UInt64 SessionId = 0;
    private string DataDir = null!;


    // I need a window of currently active inputs.  We don't want to have them always hanging around.
    // this window of active inputs is also what can be used in cases where we want to stream the game out to
    // active clients.
    private RingBuffer<GameInput> MergedInputs = new RingBuffer<GameInput>(64);

    // Each player will also have their inputs in their own slightly larger buffers.
    // These buffers should never overflow and if they so then there is a problem.
    const int PLAYER_INPUT_BUFFER_SIZE = 0x70;
    const int MAX_PLAYERS = 2;

    /// <summary>
    /// Used to track what should be the expected starting frame on merge operations when a given buffer is empty!
    /// </summary>
    private int BaseFrame = -1;
    private RingBuffer<GameInput>[] PlayerBuffers = null;
    private GameInput[] MergeBuffer = null;

    private Stream DataStream = null!;
    private GameData GameData = null!;

    private byte[] WriteBuffer = new byte[0x800];

    // -----------------------------------------------------------------------------------------------------------------------
    // NOTE: In production environments, game data should not be allowed to be overwritten!
    public GameRecorder(GameData gameData_, string dataDir_, UInt64 sessionId_, bool overwriteExisting = false)
    {
      GameData = gameData_;
      SessionId = sessionId_;
      DataDir = dataDir_;

      // Let's create the file.  If it already exists, then we have a problem / invalid session ID!
      string path = Path.Combine(DataDir, SessionId + ".replay");
      if (File.Exists(path) && !overwriteExisting)
      {
        throw new InvalidOperationException($"Data file for session id: {SessionId} already exists!");
      }

      // TODO: This is where we will create the stream for where the file data will go.
      CreateStream(path);

      PlayerBuffers = new RingBuffer<GameInput>[MAX_PLAYERS];
      for (int i = 0; i < MAX_PLAYERS; i++)
      {
        PlayerBuffers[i] = new RingBuffer<GameInput>(PLAYER_INPUT_BUFFER_SIZE);
      }
      MergeBuffer = new GameInput[MAX_PLAYERS];
    }

    // -----------------------------------------------------------------------------------------------------------------------
    public void Dispose()
    {
      DataStream?.Dispose();
      DataStream = null;
    }

    // -----------------------------------------------------------------------------------------------------------------------
    public void FLush()
    {
      DataStream?.Flush();
    }

    // -----------------------------------------------------------------------------------------------------------------------
    private void CreateStream(string path)
    {
      DataStream = File.Open(path, FileMode.Create, FileAccess.Write);
      WriteHeader(DataStream);
    }

    // -----------------------------------------------------------------------------------------------------------------------
    private void WriteHeader(Stream res)
    {
      // Write the data header.
      // This indicates that it is a replay file, version 1
      EZWriter.Write(res, new[] { 'f', 's', 'n', 'e', 'o', '-', 'r', 'f', '-' });
      EZWriter.WriteBytes(res, new[] { (byte)1 });

      // Now write the game data segment:
      WriteSegment(this.GameData);

    }

    // -----------------------------------------------------------------------------------------------------------------------
    private void WriteSegment(GameData gameData)
    {
      // V1 Segments are of the form:
      // - Type (1B)
      // - DataSize (2B)
      // - Data (DataSize)
      long start = DataStream.Position;

      UInt16 segmentSize = GameData.MAX_GAME_NAME_SIZE + sizeof(UInt64) + sizeof(int) + sizeof(int);
      EZWriter.Write(DataStream, (byte)EDataSegment.GameData);
      EZWriter.Write(DataStream, segmentSize);

      // And now for the data.....
      int size = CopyFixedString(gameData.GameName, GameData.MAX_GAME_NAME_SIZE, WriteBuffer, 0);
      DataStream.Write(WriteBuffer, 0, size);
      EZWriter.Write(DataStream, gameData.GameVersion);
      EZWriter.Write(DataStream, gameData.PlayerCount);
      EZWriter.Write(DataStream, gameData.TotalInputSize);

      // Sanity Check!
      long end = DataStream.Position;
      long total = end - start;
      int expected = segmentSize + 3;
      if (total != expected)
      {
        throw new InvalidOperationException($"Data size mismatch on write: {total} - {expected}!");
      }

    }

    // -----------------------------------------------------------------------------------------------------------------------
    // TODO: This belongs in tools somewhere.....
    private int CopyFixedString(string data, int maxSize, byte[] toBuffer, int offset)
    {
      if (data.Length > maxSize)
      {
        throw new InvalidOperationException();
      }
      int len = data.Length;
      int extra = maxSize - len;
      for (int i = 0; i < len; i++)
      {
        toBuffer[offset + i] = (byte)data[i];
      }
      for (int i = 0; i < extra; i++)
      {
        toBuffer[len + i] = 0;
      }

      return maxSize;
    }

    // -----------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// Use this to complete the recording of the replay + indicate the
    /// reason why it was completed.  This could be through a proper disconnect,
    /// or an error, etc.
    /// </summary>
    public void CompleteReplay(int frame, string reason)
    {
      throw new Exception();
    }

    // -----------------------------------------------------------------------------------------------------------------------
    public void AddChat(int playerIndex, ChatData chat)
    {
      throw new Exception();
    }


    // -----------------------------------------------------------------------------------------------------------------------
    /// <returns>
    /// Boolean value indicating if the frame was accepted.
    /// Returns false if the frame already exists</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public bool AddInput(int playerIndex, ref GameInput input)
    {
      var buf = PlayerBuffers[playerIndex];

      if (buf.IsFull)
      {
        // TODO: Shutdown the recorder correctly!
        // Send other events!
        // We don't want to use exceptions for flow control, rather we need to set an error state on the recorder!
        throw new InvalidOperationException($"Input buffer for player: {playerIndex} is full!");
      }

      // It is OK if we add a duplicate frame.
      // NOTE: This is a copy we cna probably avoid!
      int startFrame = BaseFrame;
      if (buf.Size > 0)
      {
        var f = buf.Front();
        if (f.frame == input.frame) { return false; }
        startFrame = f.frame;
      }
      if (input.frame != startFrame + 1)
      {
        // TODO: Close the recording here, properly!
        throw new InvalidOperationException("Invalid frame number!");
      }
      buf.Push(input);
      startFrame++;

      int len = this.PlayerBuffers.Length;
      bool popIt = true;

      while (true)
      {
        // Now that we have added a frame, we will go to the back, and find
        // all frames that match + do the merge.

        for (int i = 0; i < len; i++)
        {
          if (this.PlayerBuffers[i].Size == 0) { popIt = false; break; }
          if (this.PlayerBuffers[i].Back().frame != startFrame)
          {
            throw new InvalidOperationException($"Invalid (back) frame number at player index: {i}!  Should be {startFrame}!");
          }
        }

        // Nothing left to confirm!
        if (!popIt) { break; }

        // Copy data for the merge!
        for (int i = 0; i < len; i++)
        {
          MergeBuffer[i] = this.PlayerBuffers[i].Front();
          this.PlayerBuffers[i].PopBack();
        }

        // Do the merge here + write the segment!
        BaseFrame = startFrame;
        startFrame++;
      }

      // throw new Exception();
      return true;
    }

  }

  // ==============================================================================================================================
  public class ChatData
  {
    public int PlayerIndex { get; set; }
    /// <summary>
    /// What frame was the message sent on?
    /// </summary>
    public int Frame { get; set; }
    public string Message { get; set; }
  }


  // ==============================================================================================================================
  public class GameData
  {
    public const int MAX_GAME_NAME_SIZE = 32;

    /// <summary>
    /// Name of the game.
    /// </summary>
    public string GameName { get; set; }

    /// <summary>
    /// Reserved
    /// </summary>
    public UInt64 GameVersion { get; private set; } = 1;
    /// <summary>
    /// How many people are playing.
    /// </summary>
    public int PlayerCount { get; set; }

    /// <summary>
    /// Size of inputs for all players.
    /// </summary>
    public int TotalInputSize { get; set; }
  }

  // ==============================================================================================================================
  /// <summary>
  /// What types of data segments are we going to write into our replay files?
  /// </summary>
  public enum EDataSegment
  {
    Invalid = 0,
    GameData,
    InputData,
    ChatData,
  }

}
