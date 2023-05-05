using System.Collections.Generic;
using UnityEngine;
using XDPaint.Core;

namespace XDPaint.Tools.Raycast.Base
{
    public interface IRaycastMeshData : IDisposable
    {
        Transform Transform { get; }
        List<Vector3> Vertices { get; }
        Mesh Mesh { get; }
        IReadOnlyCollection<IPaintManager> PaintManagers { get; }

        void AddPaintManager(IPaintManager paintManager);
        void RemovePaintManager(IPaintManager paintManager);

        void Init(Component paintComponent, Component rendererComponent);
        Vector2 GetUV(int channel, int index);
        IEnumerable<Triangle> GetNeighborsRaycasts(IPaintManager sender, Triangle currentTriangle, Ray ray);
        Triangle GetRaycast(IPaintManager sender, Ray ray, bool useWorld = true, bool useCache = true);
    }
}