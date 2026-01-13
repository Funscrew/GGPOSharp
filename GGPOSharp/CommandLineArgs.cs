using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GGPOSharp;



// ==============================================================================================================================
public abstract class ClientOptions
{
  [Option("local-port", Required = false, Default = Defaults.LOCAL_PORT, HelpText = "The port that we are listening on.")]
  public int LocalPort { get; set; } = Defaults.LOCAL_PORT;

  [Option("auto-reinit", HelpText = "If set, the client will automatically reinitialize when the client(s) have disconnected.", Required = false)]
  public bool AutoReinitialize { get; set; }

  public uint ProtocolVersion { get; set; } = Defaults.PROTOCOL_VERSION;
}

// ==============================================================================================================================
/// <summary>
/// This client will listen to two or more connected endpoints and record / merge the input packets
/// that are sent along.
/// </summary>
[Verb("replay-listen")]
public class ReplayListenOptions : ClientOptions
{

  /// <summary>
  /// This is how the replay sessions are uniquely identified.
  /// This is what will be sent to the client as this endpoint's "player name".
  /// </summary>
  [Option("session-id")]
  public UInt64 SessionId { get; set; } = 0;

  /// <summary>
  /// Comma delimited list of all addresses that we are going to listen in on.
  /// There should be one entry for each player that will be sending the data.
  /// </summary>
  [Option("listen-on", HelpText = "host:port that we are listening for connections on.")]
  public string ListenOn { get; set; }

  /// <summary>
  /// Time in ms. for how long we will wait for the expected players to connect / sync.
  /// </summary>
  public int StartupTimeout { get; set; } = -1;

}

// ==============================================================================================================================
[Verb("input-echo")]
public class InputEchoOptions : ClientOptions
{

  [Option("player", HelpText = "The player number: 1, 2, etc.")]
  public byte PlayerNumber { get; set; }

  [Option("name", HelpText = "Name of the player", Required = true)]
  public string PlayerName { get; set; }

  
  /// <summary>
  /// Should the left / right buttons be reversed?
  /// </summary>
  [Option("invert-controls", HelpText ="If set, the left/right controls will be inverted when echoing the input.")]
  public bool InvertLeftRightControls { get; set; } = true;

  /// <summary>
  /// How many frams should the echo be delayed?
  /// </summary>
  [Option("delay-frames", HelpText = "How many frames should the echo be delayed?")]
  public int DelayFrameCount { get; set; } = 30;
}