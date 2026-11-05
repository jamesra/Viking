using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Viking.SegmentationPrompts.Native
{
    /// <summary>
    /// C entry points over <see cref="PromptBuilder"/>. Every call takes serialized protobuf messages and writes one
    /// buffer the caller frees with <c>sp_free</c>: the result on success, a UTF-8 message otherwise.
    /// </summary>
    /// <remarks>
    /// Status codes: 0 success; 1 invalid argument (<see cref="ArgumentException"/>, <see cref="FormatException"/>, or a
    /// message that does not parse); 2 too large (<see cref="PromptTooLargeException"/>); 3 any other failure.
    /// Exceptions never cross the boundary: an unmanaged caller cannot catch them.
    /// </remarks>
    public static unsafe class Exports
    {
        private const int Ok = 0;
        private const int InvalidArgument = 1;
        private const int TooLarge = 2;
        private const int Failed = 3;

        /// <summary><see cref="PromptBuilder.BuildStart"/>: a PromptInput in, a SegmentTilesRequest out.</summary>
        [UnmanagedCallersOnly(EntryPoint = "sp_build_start")]
        public static int BuildStart(byte* input, nint inputLength, byte** output, nint* outputLength) =>
            Run(() => PromptBuilder.BuildStart(Bytes(input, inputLength)), output, outputLength);

        /// <summary><see cref="PromptBuilder.Reach"/>: a PromptInput in, a PromptReach out.</summary>
        [UnmanagedCallersOnly(EntryPoint = "sp_reach")]
        public static int Reach(byte* input, nint inputLength, byte** output, nint* outputLength) =>
            Run(() => PromptBuilder.Reach(Bytes(input, inputLength)), output, outputLength);

        /// <summary>
        /// <see cref="PromptBuilder.BackgroundFor"/>: a PromptInput, a TilesNeeded and the location ids sent so far in,
        /// a PromptBackground out.
        /// </summary>
        [UnmanagedCallersOnly(EntryPoint = "sp_background_for")]
        public static int BackgroundFor(
            byte* input, nint inputLength, byte* tilesNeeded, nint tilesNeededLength, long* sentIds, nint sentCount,
            byte** output, nint* outputLength)
        {
            return Run(() =>
            {
                long[] sent = sentCount > 0 ? new ReadOnlySpan<long>(sentIds, checked((int)sentCount)).ToArray() : Array.Empty<long>();
                return PromptBuilder.BackgroundFor(Bytes(input, inputLength), Bytes(tilesNeeded, tilesNeededLength), sent);
            }, output, outputLength);
        }

        /// <summary>Frees a buffer another entry point wrote.</summary>
        [UnmanagedCallersOnly(EntryPoint = "sp_free")]
        public static void Free(byte* buffer) => NativeMemory.Free(buffer);

        private static byte[] Bytes(byte* data, nint length) =>
            length > 0 ? new ReadOnlySpan<byte>(data, checked((int)length)).ToArray() : Array.Empty<byte>();

        private static int Run(Func<byte[]> call, byte** output, nint* outputLength)
        {
            int status;
            byte[] result;
            try
            {
                result = call();
                status = Ok;
            }
            catch (PromptTooLargeException e)
            {
                (status, result) = (TooLarge, Encoding.UTF8.GetBytes(e.Message));
            }
            catch (Exception e) when (e is ArgumentException || e is FormatException || e is Google.Protobuf.InvalidProtocolBufferException)
            {
                (status, result) = (InvalidArgument, Encoding.UTF8.GetBytes(e.Message));
            }
            catch (Exception e)
            {
                (status, result) = (Failed, Encoding.UTF8.GetBytes(e.ToString()));
            }
            byte* buffer = (byte*)NativeMemory.Alloc((nuint)Math.Max(1, result.Length));
            result.AsSpan().CopyTo(new Span<byte>(buffer, result.Length));
            *output = buffer;
            *outputLength = result.Length;
            return status;
        }
    }
}
