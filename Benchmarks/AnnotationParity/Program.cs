using BenchmarkDotNet.Running;

namespace AnnotationParity
{
    /// <summary>
    /// gRPC client parity benchmarks (see Clients/WebAnnotationModel.gRPC/PARITY.md).
    /// Run in Release: dotnet run -c Release -f net10.0-windows --project Benchmarks/AnnotationParity -- --filter *
    /// </summary>
    public static class Program
    {
        public static void Main(string[] args) =>
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
