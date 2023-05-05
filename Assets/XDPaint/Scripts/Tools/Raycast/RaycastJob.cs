using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using XDPaint.Tools.Raycast.Data;
using XDPaint.Utils;
#if BURST
using Unity.Burst;
#endif

namespace XDPaint.Tools.Raycast
{
#if BURST
	[BurstCompile]
#endif
	public struct RaycastJob : IJob
	{
		[ReadOnly] public NativeArray<TriangleData> Triangles;
		[WriteOnly] public NativeArray<RaycastTriangleHit> Hits;
		public NativeArray<int> HitsCount;
		public Vector3 RayOrigin, RayDirection;

		public void Execute()
		{
			var eps = float.Epsilon;
			for (var i = 0; i < Triangles.Length; i++)
			{
				var triangle = Triangles[i];
				var p1 = triangle.Position0;
				var p2 = triangle.Position1;
				var p3 = triangle.Position2;
				var e1 = p2 - p1;
				var e2 = p3 - p1;

				var p = Vector3.Cross(RayDirection, e2);
				var det = Vector3.Dot(e1, p);
				if (det.IsNaNOrInfinity() || det > eps && det < -eps)
					continue;

				var invDet = 1.0f / det;
				var t = RayOrigin - p1;
				var u = Vector3.Dot(t, p) * invDet;
				if (u.IsNaNOrInfinity() || u < 0f || u > 1f)
					continue;

				var q = Vector3.Cross(t, e1);
				var v = Vector3.Dot(RayDirection, q) * invDet;
				if (v.IsNaNOrInfinity() || v < 0f || u + v > 1f)
					continue;

				if (Vector3.Dot(e2, q) * invDet > eps)
				{
					Hits[HitsCount[0]] = new RaycastTriangleHit
					{
						Id = triangle.Id,
						Position = p1 + u * e1 + v * e2,
						UV = triangle.UV0 + (triangle.UV1 - triangle.UV0) * u + (triangle.UV2 - triangle.UV0) * v
					};
					HitsCount[0]++;
				}
			}
		}
	}
}