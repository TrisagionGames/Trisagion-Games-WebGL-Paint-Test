using UnityEngine;
using XDPaint.Core.PaintObject.Base;
using XDPaint.Tools.Raycast;
using XDPaint.Tools.Raycast.Data;

namespace XDPaint.Core.PaintObject
{
    public sealed class SpriteRendererPaint : BasePaintObject
    {
        private SpriteRenderer renderer;
        private Bounds bounds;
        private Sprite sprite;
        private Plane plane;
        private TriangleData triangle;
        private Ray ray;
        private Vector3 objectPosition;
        private Vector3 objectForward;
        private Vector3 objectBoundsSize;

        protected override void Init()
        {
            ObjectTransform.TryGetComponent(out renderer);
            sprite = renderer.sprite;
            UpdateObjectBounds();
        }
        
        private void InitTriangle()
        {
            var previousRotation = renderer.transform.rotation;
            renderer.transform.rotation = Quaternion.identity;
            var boundsSize = renderer.bounds.size;
            objectBoundsSize = boundsSize;
            //bottom left
            var position0 = new Vector3(-boundsSize.x / 2f, -boundsSize.y / 2f, 0);
            var uv0 = Vector2.zero;
            //upper left
            var position1 = new Vector3(-boundsSize.x / 2f, boundsSize.y / 2f, 0);
            var uv1 = Vector2.up;
            //upper right
            var position2 = new Vector3(boundsSize.x / 2f, boundsSize.y / 2f, 0);
            var uv2 = Vector2.one;
            triangle = new TriangleData
            {
                Position0 = position0,
                Position1 = position1,
                Position2 = position2,
                UV0 = uv0,
                UV1 = uv1,
                UV2 = uv2
            };
            renderer.transform.rotation = previousRotation;
        }

        protected override bool IsInBounds(Vector3 position)
        {
            if (renderer != null)
            {
                bounds = renderer.bounds;
            }
            ray = Camera.ScreenPointToRay(position);
            var inBounds = bounds.IntersectRay(ray);
            return inBounds;
        }

        private void UpdateObjectBounds()
        {
            if (renderer != null && objectBoundsSize != renderer.bounds.size)
            {
                InitTriangle();
            }
        }

        protected override void CalculatePaintPosition(Vector3 position, Vector2? uv = null, bool usePostPaint = true, Triangle hitTriangle = null)
        {
            InBounds = IsInBounds(position);
            if (InBounds)
            {
                IsPaintingDone = true;
            }

            if (objectPosition != ObjectTransform.position || objectForward != ObjectTransform.forward)
            {
                plane = new Plane(ObjectTransform.forward, ObjectTransform.position);
                objectPosition = ObjectTransform.position;
                objectForward = ObjectTransform.forward;
            }
            
            if (plane.Raycast(ray, out var enter))
            {
                var point = ray.GetPoint(enter);
                LocalPosition = Vector3.Scale(ObjectTransform.InverseTransformPoint(point), ObjectTransform.lossyScale);
                UpdateObjectBounds();
                var pivotOffset = sprite.pivot / sprite.rect.size - Vector2.one * 0.5f;
                var uvCoords = triangle.GetUV(LocalPosition.Value) + pivotOffset;
                PaintPosition = new Vector2(
                    Mathf.LerpUnclamped(sprite.rect.x, sprite.rect.x + sprite.rect.width, uvCoords.x),
                    Mathf.LerpUnclamped(sprite.rect.y, sprite.rect.y + sprite.rect.height, uvCoords.y));
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