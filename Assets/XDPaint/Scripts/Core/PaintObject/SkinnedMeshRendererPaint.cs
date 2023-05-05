using UnityEngine;
using XDPaint.Core.PaintObject.Base;
using XDPaint.Tools.Raycast;

namespace XDPaint.Core.PaintObject
{
    public sealed class SkinnedMeshRendererPaint : BasePaintObject
    {
		private SkinnedMeshRenderer skinnedMeshRenderer;
		private Mesh mesh;
		private Bounds bounds;

		protected override void Init()
		{
			if (ObjectTransform.TryGetComponent(out skinnedMeshRenderer))
			{
				mesh = skinnedMeshRenderer.sharedMesh;
			}
			
			if (mesh == null)
			{
				Debug.LogError("Can't find SkinnedMeshRenderer component!");
			}
		}

		protected override bool IsInBounds(Vector3 position)
		{
			if (skinnedMeshRenderer != null)
			{
				// bounds = renderer.bounds;
				bounds = mesh.GetSubMesh(PaintManager.SubMesh).bounds;
				bounds = TransformBounds(skinnedMeshRenderer.rootBone, bounds);
			}

			var ray = Camera.ScreenPointToRay(position);
			var inBounds = bounds.IntersectRay(ray);
			return inBounds;
		}

		private Bounds TransformBounds(Transform transform, Bounds localBounds)
		{
			var center = transform.TransformPoint(localBounds.center);
			var extents = localBounds.extents;
			var axisX = transform.TransformPoint(extents.x, 0, 0);
			var axisY = transform.TransformPoint(0, extents.y, 0);
			var axisZ = transform.TransformPoint(0, 0, extents.z);
			extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
			extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
			extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);
			return new Bounds { center = center, extents = extents };
		}

		protected override void CalculatePaintPosition(Vector3 position, Vector2? uv = null, bool usePostPaint = true, Triangle hitTriangle = null)
		{
			InBounds = hitTriangle != null && IsInBounds(position);
			if (!InBounds)
			{
				PaintPosition = null;
				if (usePostPaint)
				{
					OnPostPaint();
				}
				else
				{
					UpdateBrushPreview();
				}
				return;
			}

			var hasRaycast = uv != null;
			if (hasRaycast)
			{
				PaintPosition = new Vector2(PaintMaterial.SourceTexture.width * uv.Value.x, PaintMaterial.SourceTexture.height * uv.Value.y);
				IsPaintingDone = true;
			}

			if (usePostPaint)
			{
				OnPostPaint();
			}
			else
			{
				UpdateBrushPreview();
			}
		}
    }
}