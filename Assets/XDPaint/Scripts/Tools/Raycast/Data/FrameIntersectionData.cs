using UnityEngine;
using XDPaint.Core;

namespace XDPaint.Tools.Raycast.Data
{
    public struct FrameIntersectionData
    {
        public IPaintManager PaintManager;
        public Triangle Triangle;
        public Ray Ray;
        public int FrameId;
    }
}