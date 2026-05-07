using System;
using System.Collections.Generic;
using System.Text;

namespace AstcEncoder
{
    /// <summary>
    /// An opaque structure; see  astcenc_internal.h for definition.
    /// </summary>
    public struct AstcencContext
    {
        internal IntPtr internal_context;

        public static AstcencContext Null => new AstcencContext { internal_context = IntPtr.Zero };
    }
}
