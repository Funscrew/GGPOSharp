using GGPOSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace GGPOSharpTesters
{


  // ==============================================================================================================================
  public class SimTimer : ITimeSource
  {
    private int _CurTime = 0;
    public int CurTime { get { return this._CurTime; } }

    // ----------------------------------------------------------------------------------------------------------------
    public void AddTime(int ms)
    {
      _CurTime += ms;
    }
  }

  // ==============================================================================================================================
  public class TestUDP : IUdpBlaster
  {
    public TestClient Client { get; private set; }
    public ITimeSource TimeSource { get; private set; }

    // ----------------------------------------------------------------------------------------------------------------
    public TestUDP(TestClient client_,   ITimeSource timeSource_)
    {
      Client = client_;
      TimeSource = timeSource_;
    }

    // ----------------------------------------------------------------------------------------------------------------
    public void Dispose()
    {
      // NOOP
    }

    // ----------------------------------------------------------------------------------------------------------------
    public int Receive(byte[] receiveBuffer, ref EndPoint remoteEP)
    {
      throw new NotImplementedException();
    }

    // ----------------------------------------------------------------------------------------------------------------
    public int Send(byte[] sendBuffer, int packetSize, SocketAddress useRemote)
    {
      throw new NotImplementedException();
    }
  }

  // ==============================================================================================================================
  public class TestClient : IGGPOClient
  {
    public int CurTime { get { return (int)Clock.ElapsedMilliseconds; } }
    public Stopwatch Clock { get; private set; } = Stopwatch.StartNew();
    public uint ClientVersion { get; private set; } = Defaults.PROTOCOL_VERSION;
    public IUdpBlaster UDP { get; set; }

    // ---------------------------------------------------------------------------------------------------------------------------
    public TestClient(int localPort, IUdpBlaster udp_)
    {
      UDP = udp_; // new UdpBlaster(localPort);
    }

  }
}
