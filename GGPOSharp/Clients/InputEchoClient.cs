using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GGPOSharp.Clients
{

  // ==============================================================================================================================
  /// <summary>
  /// This is an example client that can be used to replay inputs from the remote peer at a given time interval.
  /// </summary>
  internal class InputEchoClient : GGPOClient
  {

    struct InputEcho
    {
      public int ReplayTime;
      public GameInput Input;
    }

    private InputEcho[] Echoes = null!;
    private int CurEchoFrame = -1;
    private int NextEchoTime = int.MaxValue;
    private int MaxEchoFrames = 0;

    private InputEchoOptions EchoOptions = null!;
    private Stopwatch Clock = Stopwatch.StartNew();

    // --------------------------------------------------------------------------------------------------------------------------
    public InputEchoClient(GGPOClientOptions options_, InputEchoOptions echoOps_) : base(options_)
    {
      EchoOptions = echoOps_;

      // This will size the echo frames appropriately.
      MaxEchoFrames = (int)((EchoOptions.EchoDelay / 1000.0f) * 60 * 2);

      Echoes = new InputEcho[MaxEchoFrames];
    }

    // --------------------------------------------------------------------------------------------------------------------------
    protected override void OnUdpProtocolPeerEvent(ref UdpEvent evt, ushort playerIndex)
    {
      base.OnUdpProtocolPeerEvent(ref evt, playerIndex);
      if (evt.type == EEventType.Input && CurEchoFrame < MaxEchoFrames - 1)
      {
        int nextTime = (int)Clock.ElapsedMilliseconds + EchoOptions.EchoDelay;

        ++CurEchoFrame;
        Echoes[CurEchoFrame].Input = evt.u.input;
        Echoes[CurEchoFrame].ReplayTime = nextTime;
      }
    }

  }


  // ==============================================================================================================================
  public class InputEchoOptions
  {
    /// <summary>
    /// Should the left / right buttons be reversed?
    /// </summary>
    public bool InvertLeftRightControls { get; set; } = true;

    /// <summary>
    /// Time in milliseconds that the replay client will wait to send inputs.
    /// </summary>
    public int EchoDelay { get; set; } = 500;
  }

}
