using GGPOSharp;
using GGPOSharp.Clients;

namespace GGPOSharpTesters
{
  // ==================================================================================================================
  public class SimReplayAppliance : ReplayAppliance
  {
    // ----------------------------------------------------------------------------------------------------------------
    public SimReplayAppliance(GGPOClientOptions ggpoOps_, ReplayListenOptions ops_, IUdpBlaster udp_, GGPOSharp.SimTimer clock_)
      : base(ggpoOps_, ops_, udp_, clock_)
    { }
  }
}
