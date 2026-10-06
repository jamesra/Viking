using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace Viking.Benchmarks.Geometry
{
    /// <summary>
    /// Runs the micro-benchmarks. Pass BenchmarkDotNet arguments, for example <c>--filter *Rbf*</c>, or none to pick interactively.
    /// </summary>
    internal static class Program
    {
        private static void Main(string[] args) =>
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, DefaultConfig.Instance);
    }
}
