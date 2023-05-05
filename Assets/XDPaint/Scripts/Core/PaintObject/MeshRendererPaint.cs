using UnityEngine;
using XDPaint.Core.PaintObject.Base;
using XDPaint.Tools.Raycast;

namespace XDPaint.Core.PaintObject
{
	public sealed class MeshRendererPaint : BasePaintObject
	{
		private Renderer renderer;
		private Mesh mesh;
		private Bounds bounds;

		protected override void Init()
		{
			ObjectTransform.TryGetComponent(out renderer);

			if (ObjectTransform.TryGetComponent<MeshFilter>(out var meshFilter))
			{
				mesh = meshFilter.sharedMesh;
			}
			
			if (mesh == null)
			{
				Debug.LogError("Can't find MeshFilter component!");
			}
		}

		protected override bool IsInBounds(Vector3 position)
		{
			if (renderer != null)
			{
				bounds = renderer.bounds;
			}
			var ray = Camera.ScreenPointToRay(position);
			var inBounds = bounds.IntersectRay(ray);
			return inBounds;
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