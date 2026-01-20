using GGPOSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace GGPOSharpTesters
{

  // ==================================================================================================================
  internal class SimUdp : IUdpBlaster
  {
    public ITimeSource TimeSource { get; private set; }
    public TestMessageQueue MsgQueue { get; private set; }

    // ----------------------------------------------------------------------------------------------------------------
    public SimUdp(string host_, int port_, ITimeSource timeSource_, TestMessageQueue msgQueue_)
    {
      Host = host_;
      Port = port_;
      TimeSource = timeSource_;
      MsgQueue = msgQueue_;
    }

    // NOTE: This doesn't really matter, just a name / IP will do.
    public string Host { get; private set; }

    // NOTE: This does matter as it is how we are going to track the replays...
    public int Port { get; private set; }


    // ----------------------------------------------------------------------------------------------------------------
    public int Receive(byte[] receiveBuffer, ref EndPoint remoteEP)
    {
      var msg = MsgQueue.GetNextMessage(this);
      if (msg == null)
      {
        return 0;
      }

      int res = msg.Data.Length;
      for (int i = 0; i < res; i++)
      {
        receiveBuffer[i] = msg.Data[i];
      }
      return res;
    }

    // ----------------------------------------------------------------------------------------------------------------
    public int Send(byte[] sendBuffer, int packetSize, SocketAddress useRemote)
    {
      // I need to have the ping times so I can make this work.....
      throw new NotImplementedException();
    }

    // ----------------------------------------------------------------------------------------------------------------
    public void Dispose()
    {
      // NOOP
    }


    // ----------------------------------------------------------------------------------------------------------------


  }
}
