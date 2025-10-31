using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata.Ecma335;

namespace GGPOClient
{
  internal class Program
  {
    static void Main(string[] args)
    {
      Console.WriteLine("Welcome to GGPO Example Client!");


      // OPTIONS:
      const int LISTEN_PORT = 7001;
      const int REMOTE_PORT = 7000;
      Console.WriteLine("Waiting for incoming data...");

      var client = new UdpClient(LISTEN_PORT);
      var remote = new IPEndPoint(IPAddress.Any, REMOTE_PORT);

      string useHost = args[0];
      var remoteHost = new IPEndPoint(IPAddress.Parse(useHost), REMOTE_PORT);
      // NOTE: If we don't call this, then we can't connect!
      client.Connect(remoteHost);

      while (true)
      {
        byte[] data = client.Receive(ref remote);

        UdpMsg msg = new UdpMsg();
        UdpMsg.FromBytes(data, ref msg);
        Console.WriteLine($"Received content: {data.Length} bytes long....");

        // Handle the message.  What we get + how we handle depends on the current state....
        switch (msg.header.type)
        {
        case EMsgType.SyncRequest:
          Console.WriteLine("We received a sync request!");
        break;
          default:
            throw new InvalidOperationException($"Unsupported message type: {msg.header.type}");
        }
        // msg.Header.Type == EMsgType.SyncRequest;

        // NOTE: We are assuming that we are in sync mode......
        // Let's send a message back....
        UdpMsg reply = new UdpMsg();
        byte[] toSend = new byte[32];

        reply.ToBytes(toSend, out int length);

        client.Send(toSend, length);

        /// string data = msg.ad
      }

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
