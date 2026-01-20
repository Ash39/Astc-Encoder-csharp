using System;
using System.Collections.Generic;
using System.Text;

namespace Astc_Encoder_CSharp
{

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public ref struct AstcencImage
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
        public Span<byte> data;
    }
}
