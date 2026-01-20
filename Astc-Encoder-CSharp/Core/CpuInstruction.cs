using System;
using System.Collections.Generic;
using System.Text;

namespace Astc_Encoder_CSharp
{
    public enum CpuInstruction
    {
        SSE2,
        SSE4_1,
        SSE4_2,
        POPCNT,
        AVX2,
        F16C,
        SVEC_128,
        SVEC_256,
        NEON,
    }
}
