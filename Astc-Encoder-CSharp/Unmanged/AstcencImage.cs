using System;
using System.Collections.Generic;
using System.Text;

namespace AstcEncoder
{
    /// <summary>
    /// An uncompressed 2D or 3D image.
    /// 3D image are passed in as an array of 2D slices. Each slice has identical size and color format.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public unsafe struct AstcencImageUnmanaged
    {
        /// <summary>
        /// The X dimension of the image, in texels.
        /// </summary>
        public uint dimX;
        /// <summary>
        /// The Y dimension of the image, in texels.
        /// </summary>
        public uint dimY;
        /// <summary>
        /// The Z dimension of the image, in texels.
        /// </summary>
        public uint dimZ;
        /// <summary>
        /// The data type per component.
        /// </summary>
        public AstcencType dataType;
        /// <summary>
        /// The array of 2D slices, of length <c>dim_z</c>.
        /// </summary>
        public byte** data;
    }
}
