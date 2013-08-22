# csharp-bert

csharp-bert is a [BERT] de/serializer for C#. It just does the de/serialization part, not RPC (yet). It depends on [erlectric].

## encoding

csharp-bert coerces dict keys of type BINARY_EXT into C# strings, because
byte[] is a poor lookup key type in a C# Hashtable.


[BERT]: http://bert-rpc.org/
[erlectric]: https://github.com/znull/erlectric
