using System.Collections.Generic;

namespace FrameSyncMoba.Unit
{
    public static class VisualEventOutput
    {
        private static readonly List<VfxEvent> VfxBuffer = new List<VfxEvent>();
        private static readonly List<SfxEvent> SfxBuffer = new List<SfxEvent>();

        public static void SubmitVfx(in VfxEvent evt)
        {
            VfxBuffer.Add(evt);
        }

        public static void SubmitSfx(in SfxEvent evt)
        {
            SfxBuffer.Add(evt);
        }

        public static IReadOnlyList<VfxEvent> ConsumeVfxEvents()
        {
            var result = VfxBuffer.ToArray();
            VfxBuffer.Clear();
            return result;
        }

        public static IReadOnlyList<SfxEvent> ConsumeSfxEvents()
        {
            var result = SfxBuffer.ToArray();
            SfxBuffer.Clear();
            return result;
        }

        public static void Clear()
        {
            VfxBuffer.Clear();
            SfxBuffer.Clear();
        }
    }
}
