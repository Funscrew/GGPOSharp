using GGPOSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace GGPOSharpTesters
{

  // ==================================================================================================================
  internal class SimUdp : IUdpBlaster
  {
    public ITimeSource TimeSource { get; private set; }
    public TestMessageQueue MsgQueue { get; private set; }

    public uint AvgPing { get; set; }
    public uint PingJitter { get; set; }

    // ----------------------------------------------------------------------------------------------------------------
    public SimUdp(string host_, int port_, ITimeSource timeSource_, TestMessageQueue msgQueue_, uint avgPing_, uint pingJitter_ = 0)
    {
      Host = host_;
      Port = port_;
      TimeSource = timeSource_;
      MsgQueue = msgQueue_;

      AvgPing = avgPing_;
      PingJitter = pingJitter_;
    }

    // NOTE: This doesn't really matter, just a name / IP will do.
    public string Host { get; private set; }

    // NOTE: This does matter as it is how we are going to track the replays...
    public int Port { get; private set; }


    // ----------------------------------------------------------------------------------------------------------------
    public int Receive(byte[] receiveBuffer, ref EndPoint remoteEP)
    {
      SimUdpMessage? msg = MsgQueue.GetNextMessage(this);
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
      uint usePing = ComputePing();

      throw new Exception("get the target ports....");

      var msg = new SimUdpMessage()
      {
        Data = CopyBytes(sendBuffer, packetSize),
        ReceiveTime = (int)(TimeSource.CurTime + usePing),

        // HMMMMM.... I need to set the target host/port....
        DestHost = this.Host,
        DestPort = this.Port
      };
      MsgQueue.AddMessage(msg);

      // I need to have the ping times so I can make this work.....
      throw new NotImplementedException();
    }

    // ----------------------------------------------------------------------------------------------------------------
    // SHARE: This has utility function written all over it...
    public static byte[] CopyBytes(byte[] sendBuffer, int packetSize)
    {
      var res = new byte[packetSize];
      for (int i = 0; i < packetSize; i++) { 
        res[i]= sendBuffer[i];
      }
      return res;
    }

    // ----------------------------------------------------------------------------------------------------------------
    private uint ComputePing()
    {
      uint res = this.AvgPing;
      if (this.PingJitter > 0) {
        throw new NotSupportedException("Ping jitter is not supported at this time!");
        // TODO: LATER:
        // Do a normal distribution with the jitter (variance) so that
        // the ping times aren't always the same.
      }

      return res;
    }

    // ----------------------------------------------------------------------------------------------------------------
    public void Dispose()
    {
      // NOOP
    }


    // ----------------------------------------------------------------------------------------------------------------


  }
}
