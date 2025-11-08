using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace GGPOSharp
{
  internal class Program
  {
    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    public static extern void TimeBeginPeriod(int t);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    public static extern void TimeEndPeriod(int t);

    static void Main(string[] args)
    {
      Console.WriteLine("Welcome to GGPO Example Client!");

      var ops  = new GGPOClientOptions() { };
      var c = new GGPOClient(ops);


      // Game loop:
      // No, this isn't meant to be a sophisticated timing scenario, just get us in the ballpark...
      TimeBeginPeriod(1);

      var sw = Stopwatch.StartNew();
      const double FPS = 60.0d;
      double lastTime = double.MinValue;
      double frameTime = 1.0d / FPS;

      int frameCount = 0;
      while (true) {
        double elapsed = sw.Elapsed.TotalSeconds;
        double remainder = frameTime - (elapsed - lastTime);
        if (remainder <= 0.0d) {
          ++frameCount;
          c.RunFrame();
          // double curFPS = frameCount / elapsed;
          //if (frameCount % 60 == 0) {
          //  Console.WriteLine($"FPS:{curFPS:f2}");
          //}
          lastTime = elapsed;
        }
        else {
          // int sleepFor = (int)remainder * 1000;
          Thread.Sleep((int)(remainder * 1000.0d));
        }

      }

      //TimeEndPeriod(1);
      //return;

      //// OPTIONS:
      //const int LISTEN_PORT = 7001;
      //const int REMOTE_PORT = 7000;
      //Console.WriteLine("Waiting for incoming data...");

      //var client = new UdpClient(LISTEN_PORT);
      //var remote = new IPEndPoint(IPAddress.Any, REMOTE_PORT);

      //string useHost = args[0];
      //var remoteHost = new IPEndPoint(IPAddress.Parse(useHost), REMOTE_PORT);
      //// NOTE: If we don't call this, then we can't connect!
      //client.Connect(remoteHost);

      //while (true)
      //{
      //  // if (client.Available > 0) {
      //  byte[] data = client.Receive(ref remote);

      //  UdpMsg msg = new UdpMsg();
      //  UdpMsg.FromBytes(data, ref msg);
      //  Console.WriteLine($"Received content: {data.Length} bytes long....");

      //  // Handle the message.  What we get + how we handle depends on the current state....
      //  switch (msg.header.type)
      //  {
      //  case EMsgType.SyncRequest:
      //    Console.WriteLine("We received a sync request!");
      //  break;
      //    default:
      //      throw new InvalidOperationException($"Unsupported message type: {msg.header.type}");
      //  }
      //  // msg.Header.Type == EMsgType.SyncRequest;

      //  // NOTE: We are assuming that we are in sync mode......
      //  // Let's send a message back....
      //  UdpMsg reply = new UdpMsg();
      //  byte[] toSend = new byte[32];

      //  reply.ToBytes(toSend, out int length);

      //  client.Send(toSend, length);

      //  /// string data = msg.ad
      //}

    }

    // ----------------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// Demonstrates some interesting properties of "unions" in C#!
    /// </summary>
    private static void UnionExample()
    {
      var u = new TestUnion()
      {
        Double = 0.0d,
        Single = 0.0f
      };

      Console.WriteLine($"d:{u.Double}");
      Console.WriteLine($"s:{u.Single}");

      // Add a number.....
      u.Double = 3.14159267d;

      Console.WriteLine($"d:{u.Double} [2]");
      Console.WriteLine($"s:{u.Single} [2]");

      u.Single = 0.0f;
      Console.WriteLine($"d:{u.Double} [3]");
      Console.WriteLine($"s:{u.Single} [3]");
    }
  }
}
