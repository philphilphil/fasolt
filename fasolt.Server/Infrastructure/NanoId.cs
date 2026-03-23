using NanoidDotNet;

namespace Fasolt.Server.Infrastructure;

public static class NanoIdGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int Size = 12;

    public static string New() => Nanoid.Generate(Alphabet, Size);
}
