using GGPOSharp;

namespace GGPOSharpTesters
{
  // ==============================================================================================================================
  public class TestContext
  {
    const int TIME_INTERVAL = 1;
    const int FRAME_INTERVAL = 16;

    private SimTimer TimeSource = default!;
    private List<GGPOClient> AllClients = new List<GGPOClient>();
    private List<byte[]> InputBuffers = new List<byte[]>();

    // --------------------------------------------------------------------------------------------------------------------------
    public TestContext(SimTimer timeSource_, IList<GGPOClient> allClients_, IList<byte[]> inputBuffers_)
    {
      TimeSource = timeSource_;
      AllClients.AddRange(allClients_);
      InputBuffers.AddRange(inputBuffers_);
    }


    // --------------------------------------------------------------------------------------------------------------------------
    public void RunGame(int totalFrameCount)
    {

      // The total number of 'frames' that we want to simulate in this case.
      //  const int MAX_FRAMES = 50;
      for (int i = 0; i < totalFrameCount; i++)
      {
        TimeSource.AddTime(TIME_INTERVAL);

        if (i % FRAME_INTERVAL == 0)
        {
          // TODO: I want to change the inputs per frame.  Data doesn't matter, just that it can be exchanged.
          // Probably just increment the bits....
          // p1Input[0] = (byte)(i & 0xFF);

          int len = AllClients.Count;
          for (int j = 0; j < len; j++)
          {
            var c = AllClients[j];
            Program.RunFrame(c, InputBuffers[j]);
          }

        }
        else
        {
          // TODO: A proper idle() function.....    (see Program.cs for example)
          // This is where we would send out the player inputs and so on....
          int len = AllClients.Count;
          for (int j = 0; j < len; j++)
          {
            var c = AllClients[j];
            c.Idle();
          }
        }
      }

    }
  }




}
