using drewCo.Tools;
using GGPOSharp;
using GGPOSharp.Clients;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GGPOSharpTesters
{

  // ==============================================================================================================================
  public class ReplayTesters : TestBase
  {
    private const string TEST_DATA_DIR = "ReplayData";

    // -----------------------------------------------------------------------------------------------------------------------
    public ReplayTesters()
    {
      FileTools.CreateDirectory(TEST_DATA_DIR);
    }

    // -----------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// This test case shows that we can merge game inputs (and other) messages that come along and record them to disk or wherever.
    /// </summary>
    [Test]
    public unsafe void CanMergeGameInputs()
    {
      const int TEST_SESSION_ID = 1234;
      var recorder = new GameRecorder(new GameData()
      {
        GameName = "MyGame",
        PlayerCount = 2,
        TotalInputSize = 2 * 5
      }, TEST_DATA_DIR, TEST_SESSION_ID, true);


      // Add some inputs for the players...
      GameInput p1Input = new GameInput();
      GameInput p2Input = new GameInput();

      const int TO_ADD = 10;
      for (int i = 0; i < TO_ADD; i++)
      {
        p1Input.frame = i;
        p1Input.data[0] = (byte)(i % 256);

        p2Input.frame = i;
        p2Input.data[0] = (byte)((i + 1) % 256);

        recorder.AddInput(0, ref p1Input);
        recorder.AddInput(1, ref p2Input);
      }

      recorder.CompleteReplay(p1Input.frame + 1, ECompletionReason.NormalDisconnect);

      recorder.Dispose();

      // TODO: Best way to show that this is OK is to read the file back
      // and enure that the data is what we expect it to be.
      
      // We will record some inputs + text + proper disconnect signal.
      Assert.Fail("Please complete this test!");

    }

  }


}
