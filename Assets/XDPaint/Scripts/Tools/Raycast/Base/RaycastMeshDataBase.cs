using System.Collections.Generic;
using UnityEngine;
using XDPaint.Controllers;
using XDPaint.Core;
using XDPaint.Tools.Raycast.Data;
using XDPaint.Utils;

namespace XDPaint.Tools.Raycast.Base
{
    public abstract class RaycastMeshDataBase : IRaycastMeshData
    {
        private readonly List<IPaintManager> paintManagers = new List<IPaintManager>();
        public IReadOnlyCollection<IPaintManager> PaintManagers => paintManagers;

        private Transform transform;
        public Transform Transform => transform;
        
        private List<Vector3> vertices = new List<Vector3>();
        public List<Vector3> Vertices => vertices;

        private Mesh mesh;
        public Mesh Mesh => mesh;

        protected List<Vector2> UV = new List<Vector2>();
        protected FrameIntersectionData FrameIntersectionData;
        protected int BakedFrame;
        private Dictionary<int, UVChannelData> uvChannelsData = new Dictionary<int, UVChannelData>();
        private Dictionary<int, SubMeshTrianglesData> trianglesSubMeshData = new Dictionary<int, SubMeshTrianglesData>();
        
        public virtual void Init(Component paintComponent, Component rendererComponent)
        {
            mesh = new Mesh();
            transform = paintComponent.transform;
        }
        
        public virtual void AddPaintManager(IPaintManager paintManager)
        {
            paintManagers.Add(paintManager);
        }

        public virtual void RemovePaintManager(IPaintManager paintManager)
        {
            paintManagers.Remove(paintManager);

            if (trianglesSubMeshData.ContainsKey(paintManager.SubMesh))
            {
                trianglesSubMeshData[paintManager.SubMesh].PaintManagers.Remove(paintManager);
                if (trianglesSubMeshData[paintManager.SubMesh].PaintManagers.Count == 0)
                {
                    trianglesSubMeshData.Remove(paintManager.SubMesh);
                }
            }
            
            if (uvChannelsData.ContainsKey(paintManager.UVChannel))
            {
                uvChannelsData[paintManager.UVChannel].PaintManagers.Remove(paintManager);
                if (uvChannelsData[paintManager.UVChannel].PaintManagers.Count == 0)
                {
                    uvChannelsData.Remove(paintManager.UVChannel);
                }
            }
        }

        public void DoDispose()
        {
            if (mesh != null)
            {
                Object.Destroy(mesh);
                mesh = null;
            }
        }
        
        public abstract IEnumerable<Triangle> GetNeighborsRaycasts(IPaintManager sender, Triangle currentTriangle, Ray ray);

        public abstract Triangle GetRaycast(IPaintManager sender, Ray ray, bool useWorld = true, bool useCache = true);

        protected SubMeshTrianglesData GetTrianglesData(int subMesh)
        {
            return trianglesSubMeshData[subMesh];
        }
        
        public Vector2 GetUV(int channel, int index)
        {
            return uvChannelsData[channel].UV[index];
        }

        protected void InitUVs(IPaintManager paintManager, Mesh meshData)
        {
            //Cache UVs
            if (uvChannelsData.ContainsKey(paintManager.UVChannel))
            {
                uvChannelsData[paintManager.UVChannel].PaintManagers.Add(paintManager);
            }
            else
            {
                var uvs = new List<Vector2>();
                meshData.GetUVs(paintManager.UVChannel, uvs);
                
                if (paintManager.UVChannel == 0 && (UV == null || UV.Count == 0))
                {
                    UV = uvs;
                }
                uvChannelsData.Add(paintManager.UVChannel, new UVChannelData
                {
                    PaintManagers = new List<IPaintManager> { paintManager }, UV = uvs
                });
            }
        }
        
        protected void InitTriangles(IPaintManager paintManager, Mesh meshData)
        {
            if (trianglesSubMeshData.ContainsKey(paintManager.SubMesh))
            {
                trianglesSubMeshData[paintManager.SubMesh].PaintManagers.Add(paintManager);
            }
            else
            {
                trianglesSubMeshData.Add(paintManager.SubMesh, new SubMeshTrianglesData
                {
                    PaintManagers = new List<IPaintManager> { paintManager },
                    TrianglesData = new TriangleData[paintManager.Triangles.Length]
                });
            }
            
            if (Vertices == null || Vertices.Count == 0)
            {
                var verticesList = new List<Vector3>();
                meshData.GetVertices(verticesList);
                vertices = verticesList;
            }
			
            var verticesData = GetTrianglesData(paintManager.SubMesh);
            for (var i = 0; i < paintManager.Triangles.Length; i++)
            {
                var triangle = paintManager.Triangles[i];
                var triangleData = new TriangleData
                {
                    Id = triangle.Id,
                    Position0 = Vertices[triangle.I0],
                    Position1 = Vertices[triangle.I1],
                    Position2 = Vertices[triangle.I2],
                    UV0 = UV[triangle.I0],
                    UV1 = UV[triangle.I1],
                    UV2 = UV[triangle.I2]
                };
                verticesData.TrianglesData[i] = triangleData;
            }
        }

        protected Triangle SortIntersects(List<Triangle> triangles)
        {
            if (triangles.Count == 0)
                return null;
			
            if (triangles.Count == 1)
                return triangles[0];
			
            var result = triangles[0];
            var cameraPosition = PaintController.Instance.Camera.transform.position;
            var currentDistance = Vector3.Distance(cameraPosition, result.WorldHit);
            for (var i = 1; i < triangles.Count; i++)
            {
                var distance = Vector3.Distance(cameraPosition, triangles[i].WorldHit);
                if (distance < currentDistance)
                {
                    currentDistance = distance;
                    result = triangles[i];
                }
            }
            return result;
        }
        
        protected bool IsIntersected(Triangle triangle, Ray ray, bool writeHit = true)
        {
            var p1 = triangle.Position0;
            var p2 = triangle.Position1;
            var p3 = triangle.Position2;
            var e1 = p2 - p1;
            var e2 = p3 - p1;
            var eps = float.Epsilon;
            var p = Vector3.Cross(ray.direction, e2);
            var det = Vector3.Dot(e1, p);
            if (det.IsNaNOrInfinity() || det > eps && det < -eps)
            {
                return false;
            }
            var invDet = 1.0f / det;
            var t = ray.origin - p1;
            var u = Vector3.Dot(t, p) * invDet;
            if (u.IsNaNOrInfinity() || u < 0f || u > 1f)
            {
                return false;
            }
            var q = Vector3.Cross(t, e1);
            var v = Vector3.Dot(ray.direction, q) * invDet;
            if (v.IsNaNOrInfinity() || v < 0f || u + v > 1f)
            {
                return false;
            }
            if (Vector3.Dot(e2, q) * invDet > eps)
            {
                var hit = p1 + u * e1 + v * e2;
                if (writeHit)
                {
                    triangle.Hit = hit;
                }
                triangle.UVHit = triangle.UV0 + (triangle.UV1 - triangle.UV0) * u + (triangle.UV2 - triangle.UV0) * v;
                return true;
            }
            return false;
        }
    }
}