using System;
using System.Runtime.InteropServices;

namespace BetterSuppression
{
    public static class RNNoiseNative
    {
        private const string DllName = "rnnoise";

        public const int FrameSize = 480; // 48kHz * 0.01s (10ms)

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr rnnoise_create(IntPtr model);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern float rnnoise_process_frame(IntPtr state, [Out] float[] outFrame, [In] float[] inFrame);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void rnnoise_destroy(IntPtr state);
    }
}
