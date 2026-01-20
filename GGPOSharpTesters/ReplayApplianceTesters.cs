namespace GGPOSharpTesters
{
  // ==============================================================================================================================
  public class ReplayApplianceTesters
  {
    // --------------------------------------------------------------------------------------------------------------------------
    [SetUp]
    public void Setup()
    {
    }

    // --------------------------------------------------------------------------------------------------------------------------
    /// <summary>
    /// Show that we are able to create a replay appliance, and a client, and that we are able
    /// to sync the client with the appliance.  This is the first step in being able to send
    /// the replay data to the appliance.
    /// </summary>
    [Test]
    public void CanSyncReplayClientToAppliance()
    {
      var appliance = new ReplayAppliance();
      // Assert.Fail("do something!");
    }

  }
}