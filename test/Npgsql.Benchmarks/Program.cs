using BenchmarkDotNet.Running;
using System.Reflection;

namespace Npgsql.Benchmarks;

class Program
{
    static void Main(string[] args)
    {
#if NET11_0_OR_GREATER
        // runtime-async=on is only supported on net11.0, and BenchmarkDotNet does not yet
        // recognize net11.0 as a valid runtime moniker. Run in-process to avoid runtime validation.
        if (!System.Array.Exists(args, a => a is "-i" or "--inProcess"))
            args = [.. args, "-i"];
#endif
        new BenchmarkSwitcher(typeof(Program).GetTypeInfo().Assembly).Run(args);
    }
}