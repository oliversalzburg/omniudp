using System;
using System.Linq;

namespace OmniUdp {
    /// <summary>
    ///   Helper methods for dealing with byte-based buffers.
    /// </summary>
    internal static class BufferUtils {
        /// <summary>
        ///   Combine multiple byte arrays into a single one.
        /// </summary>
        /// <param name="arrays">The arrays to combine.</param>
        /// <returns></returns>
        /// <see cref="http://stackoverflow.com/questions/415291/best-way-to-combine-two-or-more-byte-arrays-in-c-sharp" />
        public static byte[] Combine( params byte[][] arrays ) {
            byte[] rv = new byte[ arrays.Sum( a => a.Length ) ];
            int offset = 0;
            foreach( byte[] array in arrays ) {
                Buffer.BlockCopy( array, 0, rv, offset, array.Length );
                offset += array.Length;
            }
            return rv;
        }
    }
}