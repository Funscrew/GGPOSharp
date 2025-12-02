# GGPOSharp
A C# implementation of the GPPO code that I found, and how it was integrated into fs-fbneo.



## Flow:
Each frame gets + syncs inputs + messages.

- If the simulation won't advance (for time reasons), run an idle routine.  This will run the polling + associated updates.

- If it will advance, then 'IncrementFrame' can be called.
--> This does:
1. Poll with no delay.
2. Read local inputs
3. --> add local inputs to client.
4. Sync the inputs
--> Callback for rollbacks, etc. will take place here.

From here the 'game' can run its next frame since it has synced inputs for all players.

NOTE: Any received message is rejected unless the sync event has completed.  This sets the internal 'remote magic' number
value to that of the sync.  Since all messages have this value (and it is the same) we can reject any non-sync related messages.


## Bugs
- BUG: We are not sending the local player name to the remote. (it is being set twice...)
- BUG: We are double-setting the local player number during client init.
--> This is actually more of a quality thing....


## Next Steps
- Make a 'replay client'  This thing will just take the inputs from the other player, and replay them a fixed time
later.  Options to reverse L/R directions + replay time would be nice.  Consider how such a thing might work in
terms of a game with more than two players... not to actually implement it, but so that we don't paint ourselves into
a corner when it comes to creating other types of clients that might interact with more than two players.

- Setup two C# clients and let them blast frames at each other.  This will be used for perf testing, in particular we
want to be sure that there isn't a bunch of garbage creation / collection.  Ideally we could pare it down to zero.

- Do some testing with OO (out of order) packets.  I'm still not clear on how GGPO handles this, if at all.







# GGPO PROTO:


## Notes:


## STEPS:
# Synchronize
###  Running:
Clients are now exchanging packets.  Inspection of those packets 
Sync / Handshake:
During sync operations, each client sends a sync request, and expects a response from the endpoint.
For robustness, a certain number (default = 5) of successful sync replies are required before the clients will be considered synchronized.

## Polling and Events:
Each of the clients / backends poll each game frame.  This is where messages are send, received, and handled.  Events are created for processing in the next step.
After the polling step, the client will handle any events that were put the in the queue.




## GGPO Sepcific (C++)
- Each of the backends has a list of 'endpoints' that it communicates with.  This is the equivalent of one of the C# GGPOClient classes.
- Each endpoint is initialized with UDP connection information and a *poll manager*.
- Each of the endpoints share the same poll manager instance.
- Both UdpProtocol and Udp (the main UDP client wrapper) use the IPollSink interface, and so each of them get called in PollManager::Pump()
-- Udp is initialized when the backend is setup, and UdpProto is initialized when each of the players (endpoints) are added.  This means that Udp will always poll first
and this is where the network data (messages) comes from.  It makes sense to do it first, IMO....
-- Each backend has its own Udp instance, so only the poll manager, which fires off the callbacks, is shared between everything.  Personally, I am not really sure why there is a poll manager at all..... maybe because of the periodic, and msg sinks which are never actually used?  Anyway, pollmgr + 'loop sinks' seem to be overkil
at the moment, so I will not use that approach in the C# client, and will opt for something a bit more direct for now.

- When Udp receives a message from the network it hands it off to the callbacks, which are located in the backends (p2p, etc.)  Because the backends can have
more than one connection, each are first checked via HandlesMessage, which does an address check:
```
  return _peer_addr.sin_addr.S_un.S_addr == from.sin_addr.S_un.S_addr &&
    _peer_addr.sin_port == from.sin_port;
```
to make sure that the endpoint (udpprotocol) that sent the message doesn't handle it.  This makes sense to me, but not sure if the C# model will end up this way...
Anyway, UdpProtocol uses an array of function pointers for the message specific handles, indexed by the message type...  I guess that it slightly better than a branch, so I will look into a similar implementation for the C# client....