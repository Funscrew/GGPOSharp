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


