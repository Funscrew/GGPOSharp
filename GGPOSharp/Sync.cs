using GGPOSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace GGPOSharp;

// ================================================================================================
// REFACTOR: InputSynchronizer
internal class Sync
{
  #region Internal Structures, etc.

  // ==============================================================================================
  [StructLayout(LayoutKind.Sequential, Pack = 1)]
  protected unsafe struct SavedFrame
  {
    public byte* buf;
    public int cbuf;
    public int frame;
    public int checksum;

    public SavedFrame()
    {
      buf = null;
      cbuf = 0;
      frame = -1;
      checksum = 0;
    }
  };

  // ==============================================================================================
  protected unsafe struct SavedState
  {
    // NOTE: We are going to have to do some memory stuff to get this data out correctly....
    // public SavedFrame[] frames = new SavedFrame[SyncConsts.MAX_PREDICTION_FRAMES + 2];
    // public const int savedFrameStride = 4 + (sizeof(int) * 3);    // NOTE: This assumes that sizeof(byte*) is 8.

    //public fixed byte _SavedStateData[(SyncConsts.MAX_PREDICTION_FRAMES + 2) * savedFrameStride];
    //public int head = 0;

    // TODO: Find a better way to deal with this / reduce garbage.  Maybe spans from a memory pool?
    public const int SAVED_FRAME_COUNT = GGPOConsts.MAX_PREDICTION_FRAMES + 2;
    public SavedFrame[] frames = new SavedFrame[SAVED_FRAME_COUNT];
    public int head = 0;

    public unsafe SavedState() { }

  };

  #endregion


  protected GGPOSessionCallbacks _callbacks;
  protected SavedState _savedstate;
  protected SyncOptions _config;

  protected bool _rollingback;
  protected int _last_confirmed_frame;
  protected int _curFrame;                         // Number of the current frame.  This can be adjusted during rollbacks.
  protected int _max_prediction_frames;

  InputQueue[] _input_queues;

  RingBuffer<Event> _event_queue = new RingBuffer<Event>(32);
  ConnectStatus[] _local_connect_status = null!;

  // ----------------------------------------------------------------------------------------------
  public Sync(ConnectStatus[] connect_status, SyncOptions config)
  {
    _local_connect_status = connect_status;
    _input_queues = null;

    //  _local_connect_status(connect_status),
    //_input_queues(NULL)

    _curFrame = 0;
    _last_confirmed_frame = -1;
    _max_prediction_frames = 0;

    // memset(&_savedstate, 0, sizeof(_savedstate));
    _savedstate = new SavedState();

    _config = config;
    _callbacks = config.callbacks;
    _curFrame = 0;
    _rollingback = false;

    _max_prediction_frames = config.num_prediction_frames;

    CreateQueues(config);
  }

  public bool InRollback() { return _rollingback; }
  public int GetFrameCount() { return _curFrame; }


  // ------------------------------------------------------------------------------------------------------------------------
  // Originally a destructor.
  public unsafe void Dispose()
  {
    /*
     * Delete frames manually here rather than in a destructor of the SavedFrame
     * structure so we can efficently copy frames via weak references.
     */
    for (int i = 0; i < SavedState.SAVED_FRAME_COUNT; i++)
    {
      if (_callbacks.free_buffer != null)
      {
        _callbacks.free_buffer(_savedstate.frames[i].buf);
      }
    }
    // delete[] _input_queues;
    // _input_queues = NULL;
  }

  //// ------------------------------------------------------------------------------------------------------------------------
  //void Init(Config config)
  //{
  //  _config = config;
  //  _callbacks = config.callbacks;
  //  _curFrame = 0;
  //  _rollingback = false;

  //  _max_prediction_frames = config.num_prediction_frames;

  //  CreateQueues(config);
  //}

  // ------------------------------------------------------------------------------------------------------------------------
  void SetLastConfirmedFrame(int frame)
  {
    _last_confirmed_frame = frame;
    if (_last_confirmed_frame > 0)
    {
      for (int i = 0; i < _config.num_players; i++)
      {
        _input_queues[i].DiscardConfirmedFrames(frame - 1);
      }
    }
  }

  // ------------------------------------------------------------------------------------------------------------------------
  internal bool AddLocalInput(int playerIndex, ref GameInput input)
  {
    int frames_behind = _curFrame - _last_confirmed_frame;
    if (_curFrame >= _max_prediction_frames && frames_behind >= _max_prediction_frames)
    {
      Utils.Log("Rejecting input from emulator: reached prediction barrier.\n");
      return false;
    }

    if (_curFrame == 0)
    {
      SaveCurrentFrame();
    }

    Utils.Log("Sending undelayed local frame %d to queue %d.\n", _curFrame, playerIndex);
    input.frame = _curFrame;
    _input_queues[playerIndex].AddInput(ref input);

    return true;
  }

  // ------------------------------------------------------------------------------------------------------------------------
  internal void AddRemoteInput(int playerIndex, ref GameInput input)
  {
    _input_queues[playerIndex].AddInput(ref input);
  }

  // ------------------------------------------------------------------------------------------------------------------------
  internal unsafe int GetConfirmedInputs(byte* values, int size, int frame)
  {
    throw new NotImplementedException();

    //int disconnect_flags = 0;
    //char* output = (char*)values;

    //Utils.ASSERT(size >= _config.num_players * _config.input_size);

    //memset(output, 0, size);
    //for (int i = 0; i < _config.num_players; i++)
    //{
    //  GameInput input;
    //  if (_local_connect_status[i].disconnected && frame > _local_connect_status[i].last_frame)
    //  {
    //    disconnect_flags |= (1 << i);
    //    input.erase();
    //  }
    //  else
    //  {
    //    _input_queues[i].GetConfirmedInput(frame, &input);
    //  }
    //  memcpy(output + (i * _config.input_size), input.bits, _config.input_size);
    //}
    //return disconnect_flags;
  }

  // ------------------------------------------------------------------------------------------------------------------------
  unsafe int SynchronizeInputs(byte* values, int totalSize)
  {
    throw new NotImplementedException();
    //int disconnect_flags = 0;
    //char* output = (char*)values;

    //// Ensure a minimum amount of data so we don't overrun the buffer...
    //// Shouldn't we expect that totalSize is always the same... ??
    //Utils.ASSERT(totalSize >= _config.num_players * _config.input_size);

    //memset(output, 0, totalSize);
    //for (int i = 0; i < _config.num_players; i++)
    //{
    //  GameInput input;
    //  if (_local_connect_status[i].disconnected && _curFrame > _local_connect_status[i].last_frame)
    //  {
    //    disconnect_flags |= (1 << i);
    //    input.erase();
    //  }
    //  else
    //  {
    //    _input_queues[i].GetInput(_curFrame, &input);
    //  }
    //  memcpy(output + (i * _config.input_size), input.bits, _config.input_size);
    //}
    //return disconnect_flags;
  }

  // ------------------------------------------------------------------------------------------------------------------------
  void CheckSimulation(int timeout)
  {
    throw new NotImplementedException();

    //int seek_to;
    //if (!CheckSimulationConsistency(&seek_to))
    //{
    //  AdjustSimulation(seek_to);
    //}
  }

  // ------------------------------------------------------------------------------------------------------------------------
  void IncrementFrame()
  {
    _curFrame++;
    SaveCurrentFrame();
  }

  // ------------------------------------------------------------------------------------------------------------------------
  internal void AdjustSimulation(int seek_to)
  {
    int prevFrame = _curFrame;
    int count = _curFrame - seek_to;   // This is assumed to be positive b/c we are rolling back to an earlier frame.  Therefore, _framecount is always > seek_to.

    Utils.Log("Catching up\n");
    _rollingback = true;

    /*
     * Flush our input queue and load the last frame.
     */
    LoadFrame(seek_to);
    Utils.ASSERT(_curFrame == seek_to);

    // Now that we have updated _framecount to seek_to, it will be == to (oldFrameCount - count).

    /*
     * Advance frame by frame (stuffing notifications back to 
     * the master).
     */
    ResetPrediction(_curFrame);
    for (int i = 0; i < count; i++)
    {
      _callbacks.rollback_frame(0);
    }

    // NOTE: This assert will fail if _framecount is not correctly incremented in the above for loop.  rollback_frame should increment it!
    Utils.ASSERT(_curFrame == prevFrame);

    _rollingback = false;

    Utils.Log("---\n");
  }

  // ------------------------------------------------------------------------------------------------------------------------
  void LoadFrame(int frame)
  {
    throw new NotImplementedException();

    //// find the frame in question
    //if (frame == _curFrame)
    //{
    //  Utils.Log("Skipping NOP.\n");
    //  return;
    //}

    //// Move the head pointer back and load it up
    //_savedstate.head = FindSavedFrameIndex(frame);
    //SavedFrame* state = _savedstate.frames + _savedstate.head;

    //Utils.Log("=== Loading frame info %d (size: %d  checksum: %08x).\n",
    //    state->frame, state->cbuf, state->checksum);

    //Utils.ASSERT(state->buf && state->cbuf);
    //_callbacks.load_game_state(state->buf, state->cbuf);

    //// Reset framecount and the head of the state ring-buffer to point in
    //// advance of the current frame (as if we had just finished executing it).
    //_curFrame = state->frame;
    //_savedstate.head = (_savedstate.head + 1) % SavedState.SAVED_FRAME_COUNT;
  }

  // ------------------------------------------------------------------------------------------------------------------------
  void SaveCurrentFrame()
  {
  throw new NotImplementedException();
    ///*
    // * See StateCompress for the real save feature implemented by FinalBurn.
    // * Write everything into the head, then advance the head pointer.
    // */
    //SavedFrame* state = _savedstate.frames + _savedstate.head;
    //if (state->buf)
    //{
    //  _callbacks.free_buffer(state->buf);
    //  state->buf = NULL;
    //}
    //state->frame = _curFrame;
    //_callbacks.save_game_state(&state->buf, &state->cbuf, &state->checksum, state->frame);

    //Utils.Log("=== Saved frame info %d (size: %d  checksum: %08x).\n", state->frame, state->cbuf, state->checksum);
    //_savedstate.head = (_savedstate.head + 1) % SavedState.SAVED_FRAME_COUNT;
  }

  // ------------------------------------------------------------------------------------------
  //SavedFrame& GetLastSavedFrame()
  //{
  //  int i = _savedstate.head - 1;
  //  if (i < 0)
  //  {
  //    i = SavedState.SAVED_FRAME_COUNT - 1;
  //  }
  //  return _savedstate.frames[i];
  //}


  // ------------------------------------------------------------------------------------------
  int FindSavedFrameIndex(int frame)
  {
    int i, count = SavedState.SAVED_FRAME_COUNT;
    for (i = 0; i < count; i++)
    {
      if (_savedstate.frames[i].frame == frame)
      {
        break;
      }
    }
    if (i == count)
    {
      Utils.ASSERT(false);
    }
    return i;
  }


  // ------------------------------------------------------------------------------------------
  bool CreateQueues(in SyncOptions config)
  {
    // delete[] _input_queues;
    _input_queues = new InputQueue[_config.num_players];

    for (int i = 0; i < _config.num_players; i++)
    {
      _input_queues[i] = new InputQueue(i, _config.input_size);
      // _input_queues[i].Init(i, _config.input_size);
    }
    return true;
  }

  // ------------------------------------------------------------------------------------------
  internal unsafe bool CheckSimulationConsistency(int* seekTo)
  {
    int first_incorrect = GameInput.NULL_FRAME;
    for (int i = 0; i < _config.num_players; i++)
    {
      int incorrect = _input_queues[i].GetFirstIncorrectFrame();
      Utils.Log("considering incorrect frame %d reported by queue %d.\n", incorrect, i);

      if (incorrect != GameInput.NULL_FRAME && (first_incorrect == GameInput.NULL_FRAME || incorrect < first_incorrect))
      {
        first_incorrect = incorrect;
      }
    }

    if (first_incorrect == GameInput.NULL_FRAME)
    {
      Utils.Log("prediction ok.  proceeding.\n");
      return true;
    }
    *seekTo = first_incorrect;
    return false;
  }

  // ------------------------------------------------------------------------------------------
  internal void SetFrameDelay(int queue, int delay)
  {
    throw new NotImplementedException();
    // _input_queues[queue].SetFrameDelay(delay);
  }


  // ------------------------------------------------------------------------------------------
  void ResetPrediction(int frameNumber)
  {
    for (int i = 0; i < _config.num_players; i++)
    {
      _input_queues[i].ResetPrediction(frameNumber);
    }
  }

  // ------------------------------------------------------------------------------------------
  bool GetEvent(ref Event e)
  {
    if (_event_queue.Size != 0)
    {
      e = _event_queue.Front();
      _event_queue.Pop();
      return true;
    }
    return false;
  }

}



// ================================================================================================
struct SyncOptions
{
  public GGPOSessionCallbacks callbacks;
  public int num_prediction_frames;
  public int num_players;
  public int input_size;
};

// ================================================================================================
enum ESyncType
{
  ConfirmedInput = 0
}

// ================================================================================================
struct SyncEvent
{
  ESyncType type;
  GameInput confirmedInput;
}

public unsafe delegate bool SessionPointerCallback<T>(T* arg);
public delegate bool SessionRefCallback<T>(ref T arg);

// ================================================================================================
public unsafe class GGPOSessionCallbacks
{
  public SessionRefCallback<GGPOEvent>? on_event = null;
  public SessionPointerCallback<byte>? free_buffer = null;
  public Action<int>? rollback_frame = null;
}