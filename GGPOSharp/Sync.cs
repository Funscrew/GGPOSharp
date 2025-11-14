using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GGPOSharp;

// ================================================================================================
// REFACTOR: InputSynchronizer
internal class Sync
{
  #region Internal Structures, etc.

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

  protected unsafe struct SavedState
  {
    // NOTE: We are going to have to do some memory stuff to get this data out correctly....
    // public SavedFrame[] frames = new SavedFrame[SyncConsts.MAX_PREDICTION_FRAMES + 2];
    // public const int savedFrameStride = 4 + (sizeof(int) * 3);    // NOTE: This assumes that sizeof(byte*) is 8.

    //public fixed byte _SavedStateData[(SyncConsts.MAX_PREDICTION_FRAMES + 2) * savedFrameStride];
    //public int head = 0;

    // TODO: Find a better way to deal with this / reduce garbage.  Maybe spans from a memory pool?
    public SavedFrame[] frames = new SavedFrame[GGPOConsts.MAX_PREDICTION_FRAMES + 2];
    int head = 0;

    public unsafe SavedState() { }

  };

  #endregion


  protected GGPOSessionCallbacks _callbacks;
  protected SavedState _savedstate;
  protected Config _config;

  protected bool _rollingback;
  protected int _last_confirmed_frame;
  protected int _curFrame;                         // Number of the current frame.  This can be adjusted during rollbacks.
  protected int _max_prediction_frames;

  InputQueue* _input_queues;

  RingBuffer<Event, 32> _event_queue;
  UdpMsg::connect_status* _local_connect_status;


  // ----------------------------------------------------------------------------------------------
  public Sync(ConnectStatus[] connect_status)
  {
    _local_connect_status = connect_status;
    _input_queues = null;

    //  _local_connect_status(connect_status),
    //_input_queues(NULL)

    _curFrame = 0;
    _last_confirmed_frame = -1;
    _max_prediction_frames = 0;
    memset(&_savedstate, 0, sizeof(_savedstate));

  }


}



// ================================================================================================
struct Config
{
  GGPOSessionCallbacks callbacks;
  int num_prediction_frames;
  int num_players;
  int input_size;
};


enum ESyncType
{
  ConfirmedInput = 0
}

struct SyncEvent
{
  ESyncType type;
  GameInput confirmedInput;
}

// ================================================================================================
class GGPOSessionCallbacks
{
}