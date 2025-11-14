# GGPOSharp
A C# implementation of the GPPO code that I found, and how it was integrated into fs-fbneo.



## Next Steps
- Update C# topology so that the current 'GGPOClient' class becomes 'GGPOEndpoint'.  Make a proper 'client' class
that can manage multiple endpoints (one for now (two players)) that will also be able to sync, etc. or whatever the
current version of the client is able to do.
--> There are notes in code about why we want to do this.  Will probably make future development easier....

- Make a 'replay client'  This thing will just take the inputs from the other player, and replay them a fixed time
later.  Options to reverse L/R directions + replay time would be nice.  Consider how such a thing might work in
terms of a game with more than two players... not to actually implement it, but so that we don't paint ourselves into
a corner when it comes to creating other types of clients that might interact with more than two players.
 