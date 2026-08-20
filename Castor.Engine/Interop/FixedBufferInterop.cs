using System.Runtime.CompilerServices;
using System.Text;

namespace Castor.Engine.Interop
{
    [InlineArray(64)]
    internal struct FixedBuffer64
    {
        private byte _element0;
    }

    [InlineArray(32)]
    internal struct FixedBuffer32
    {
        private byte _element0;
    }

    [InlineArray(128)]
    internal struct FixedBuffer128
    {
        private byte _element0;
    }

    /// <summary>
    /// Encodes and decodes the fixed-size, null-terminated UTF-8 buffers
    /// used by the native video encoder structs, since LibraryImport's
    /// source-generated marshaller has no equivalent of the classic
    /// ByValTStr marshaling attribute.
    /// </summary>
    internal static class FixedBufferInterop
    {
        internal static string Decode(ReadOnlySpan<byte> buffer)
        {
            var terminator = buffer.IndexOf((byte)0);
            var length = terminator < 0 ? buffer.Length : terminator;
            return Encoding.UTF8.GetString(buffer[..length]);
        }

        internal static void Encode(string value, Span<byte> destination)
        {
            destination.Clear();
            var byteCount = Encoding.UTF8.GetByteCount(value);

            if (byteCount >= destination.Length)
            {
                throw new ArgumentException(
                    $"'{value}' does not fit in a {destination.Length}-byte null-terminated buffer.",
                    nameof(value));
            }

            Encoding.UTF8.GetBytes(value, destination);
        }
    }
}
