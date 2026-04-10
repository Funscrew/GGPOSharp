using drewCo.Tools;
using System.Diagnostics;
using System.Xml;

namespace GGPOSharp.Clients
{
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
    private unsafe void WriteSegment(ref GameInput input)
    {
      long start = this.DataStream.Position;
      int inputSize = this.GameData.TotalInputSize;

      EZWriter.Write(DataStream, (byte)EDataSegment.InputData);
      EZWriter.Write(DataStream, (UInt16)inputSize);

      for (int i = 0; i < inputSize; i++)
      {
        WriteBuffer[i] = input.data[i];
      }
      DataStream.Write(WriteBuffer, 0, inputSize);

      // Sanity Check!
      long end = DataStream.Position;
      long total = end - start;
      int expectedSize = inputSize + 3;
      int expected = expectedSize;
      if (total != expected)
      {
        throw new InvalidOperationException($"Data size mismatch on write: {total} - {expected}!");
      }

      DataStream.Flush();
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
    public unsafe bool AddInput(int playerIndex, ref GameInput input)
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

        MergeInputs();


        // Do the merge here + write the segment!
        BaseFrame = startFrame;
        startFrame++;
      }

      // throw new Exception();
      return true;
    }

    // -----------------------------------------------------------------------------------------------------------------------
    private unsafe void MergeInputs()
    {
      // We want to create a single GameInput instance from a full set.
      int len = this.PlayerBuffers.Length;
      int offset = this.GameData.TotalInputSize / len;

      // Reminder:  The original game inputs all come with the data in the P0 slot.
      // Therefore we need to be make sure that they end up in their game-correct
      // positions for playback / correct interpretation later.
      GameInput merged = new GameInput();
      merged.size = this.GameData.TotalInputSize;
      for (int i = 0; i < len; i++)
      {
        for (int j = 0; j < offset; j++)
        {
          merged.data[(i + offset) + j] = MergeBuffer[i].data[j];
        }
      }

      // Write that data to disk!
      WriteSegment(ref merged);

      // Add it to the active window of inputs (which are used for live playback)
      this.MergedInputs.Push(merged);
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
