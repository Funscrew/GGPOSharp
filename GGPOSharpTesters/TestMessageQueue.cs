
namespace GGPOSharpTesters
{
  // ==================================================================================================================
  /// <summary>
  /// This is how we are simulating packets across a network.
  /// Using this approach we can setup all kinds of different test scenarios for our UDP
  /// based GGPO client, and others.
  /// </summary>
  internal class TestMessageQueue
  {
    // NOTE: We shouldn't expect to see a huge number of entries in this as we will
    // tend to receive them in order, and will remove all messages that have been received, etc.
    private List<SimUdpMessage> MsgQueue = new List<SimUdpMessage>();

    // ----------------------------------------------------------------------------------------------------------------
    public SimUdpMessage? GetNextMessage(SimUdp udp)
    {
      int minTime = int.MaxValue;
      SimUdpMessage? res = null;

      int curTime = udp.TimeSource.CurTime;

      // Grab all messages in the queue up to the current time.
      // only include those messages that have the matching port...
      int len = MsgQueue.Count;
      for (int i = 0; i < len; i++)
      {
        SimUdpMessage next = MsgQueue[i];
        if (next.ReceiveTime <= curTime &&
          next.DestPort == udp.Port &&
          next.DestHost == udp.Host && next.ReceiveTime < minTime)
        {
          res = next;
          minTime = res.ReceiveTime;
        }
      }

      return res;
    }

    // ---------------------------------------------------------------------------------------------------------------------------
    internal void AddMessage(SimUdpMessage msg)
    {
      throw new NotImplementedException();
    }
  }


  // ==============================================================================================================================
  public class SimUdpMessage
  {
    public const int MAX_MSG_SIZE = 1024;

    // NOTE: Host + port are used for sending the message to the correct place...
    public string DestHost { get; set; }
    public int DestPort { get; set; }

    public byte[] Data { get; set; } = null;
    public int ReceiveTime { get; set; }
  }

}
