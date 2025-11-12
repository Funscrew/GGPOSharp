// using MsgHandler = System.Action<GGPOSharp.UdpMsg, int>;
namespace GGPOSharp
{
  // ================================================================================================================
  public class GGPOClientOptions
  {
    public const int LOCAL_PORT = 7001;
    public const int REMOTE_PORT = 7000;

    public int LocalPort { get; set; } = LOCAL_PORT;
    public string RemoteAddress { get; set; } = "127.0.0.1";
    public int RemotePort { get; set; } = REMOTE_PORT;

    public string PlayerName { get; set; } = default!;

    /// <summary>
    /// These should only be set in scenarios where you want to simulate certain network conditions.
    /// </summary>
    public TestOptions TestOptions { get; set; } = new TestOptions();
  }


}
