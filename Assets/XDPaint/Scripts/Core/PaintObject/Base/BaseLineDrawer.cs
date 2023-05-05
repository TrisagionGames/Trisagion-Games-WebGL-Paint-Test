using System;
using System.Collections.Generic;
using UnityEngine;
using XDPaint.Controllers;
using XDPaint.Tools;
using XDPaint.Tools.Raycast;
using XDPaint.Utils;

namespace XDPaint.Core.PaintObject.Base
{
    /// <summary>
    /// Performs lines drawing
    /// </summary>
	public class BaseLineDrawer
    {
        private bool useNeighborsVertices;
        public bool UseNeighborsVertices { set => useNeighborsVertices = value; }
        public Camera Camera { set => camera = value; }

        private IPaintManager paintManager;
        private BasePaintObjectRenderer objectRenderer;
        private Camera camera;
        private Transform Transform => firstTriangle.Transform;
        private Vector3 IntersectionOffset => (firstTriangle.Hit - lastTriangle.Hit) * OffsetValue;
        private Action<List<Vector3>, List<Vector2>, List<int>, List<Color>> drawLine;
        private List<Vector2> drawPositions = new List<Vector2>();
        private List<Vector3> positions = new List<Vector3>();
        private List<Color> colors = new List<Color>();
        private List<int> indices = new List<int>();
        private List<Vector2> uv = new List<Vector2>();
        private Triangle firstTriangle, lastTriangle;
        private Vector3 normal, cameraLocalPosition;
        private Vector2 textureSize;
        
        private const int IterationsMaxCount = 256;
        private const float OffsetValue = 0.0001f;
        private const float MinNormalLength = 0.000001f;

        public BaseLineDrawer(IPaintManager paintManagerInstance)
        {
            paintManager = paintManagerInstance;
        }

        public void Init(Camera currentCamera, Vector2 sourceTextureSize, Action<List<Vector3>, List<Vector2>, List<int>, List<Color>> onDrawLine)
        {
            camera = currentCamera;
            textureSize = sourceTextureSize;
            drawLine = onDrawLine;
        }

        public Vector2[] GetLinePositions(Vector2 paintUV1, Vector2 paintUV2, Triangle triangleA, Triangle triangleB, bool canRetry = true)
        {
            firstTriangle = triangleA;
            lastTriangle = triangleB;
            
            if (canRetry)
            {
                var firstWorld = Transform.TransformPoint(firstTriangle.Hit);
                var lastWorld = Transform.TransformPoint(lastTriangle.Hit);
                camera.WorldToScreenPoint(firstWorld);
                camera.WorldToScreenPoint(lastWorld);
                
                cameraLocalPosition = Transform.InverseTransformPoint(camera.transform.position);
                normal = Vector3.Cross(firstTriangle.Hit - cameraLocalPosition, lastTriangle.Hit - cameraLocalPosition);
                if (normal.magnitude < MinNormalLength)
                {
                    drawPositions.Add(paintUV1);
                    drawPositions.Add(paintUV1);
                    drawPositions.Add(paintUV2);
                    drawPositions.Add(paintUV2);
                    return drawPositions.ToArray();
                }
            }

            var iterationsCount = 0;
            var currentTriangle = firstTriangle;
            var triangles = new List<int>();
            
            if (Mathf.Abs(IntersectionOffset.magnitude) < float.Epsilon)
                return drawPositions.ToArray();

            var uvFirst = GetIntersectionUV(firstTriangle, lastTriangle.Hit, out var intersection);
            drawPositions.Add(paintUV1);
            drawPositions.Add(uvFirst);
            var beginExit = intersection;
            
            while (iterationsCount < IterationsMaxCount && currentTriangle.Id != lastTriangle.Id)
            {
                iterationsCount++;
                intersection -= IntersectionOffset;
                Triangle triangle;
                var ray = GetRay(intersection);
                if (useNeighborsVertices)
                {
                    triangle = RaycastController.Instance.NeighborsRaycast(paintManager, currentTriangle, ray);
                }
                else
                {
                    triangle = RaycastController.Instance.RaycastLocal(paintManager, ray);
                }

                if (triangle == null)
                {
                    if (canRetry)
                    {
                        return GetLinePositions(paintUV2, paintUV1, lastTriangle, firstTriangle, false);
                    }
                    break;
                }

                if (triangle.Id != lastTriangle.Id && currentTriangle.Id != triangle.Id)
                {
                    currentTriangle = triangle;
                    if (useNeighborsVertices)
                    {
                        if (triangles.Contains(currentTriangle.Id))
                        {
                            break;
                        }
                        triangles.Add(currentTriangle.Id);
                    }
                    
                    intersection = MathHelper.GetExitPointFromTriangle(camera, currentTriangle, beginExit, lastTriangle.Hit, normal);
                    beginExit = intersection;
                    ray = GetRay(intersection);
                    var uv = MathHelper.GetIntersectionUV(currentTriangle, ray);
                    var uvRaycast = new Vector2(triangle.UVHit.x * textureSize.x, triangle.UVHit.y * textureSize.y);
                    uv = new Vector2(uv.x * textureSize.x, uv.y * textureSize.y);
                    drawPositions.Add(uvRaycast);
                    drawPositions.Add(uv);
                }
                else
                {
                    break;
                }
            }

            var uvLast = GetIntersectionUV(lastTriangle, beginExit, out intersection);
            drawPositions.Add(uvLast);
            drawPositions.Add(paintUV2);
            return drawPositions.ToArray();
        }

        private Vector2 GetIntersectionUV(Triangle triangle, Vector3 exit, out Vector3 exitPosition)
        {
            exitPosition = MathHelper.GetExitPointFromTriangle(camera, triangle, triangle.Hit, exit, normal);
            var ray = GetRay(exitPosition);
            var intersectionUV = MathHelper.GetIntersectionUV(triangle, ray);
            return new Vector2(intersectionUV.x * textureSize.x, intersectionUV.y * textureSize.y);
        }

        private Ray GetRay(Vector3 point)
        {
            var direction = point - cameraLocalPosition;
            return new Ray(point + direction, -direction);
        }

        private float GetRatio(float totalDistanceInPixels, float brushSize, float[] brushSizes)
        {
            var brushPressureStart = brushSizes[0];
            var brushPressureEnd = brushSizes[1];
            var pressureDifference = Mathf.Abs(brushPressureStart - brushPressureEnd);
            var brushCenterPartWidth = Mathf.Clamp(Settings.Instance.BrushDuplicatePartWidth * brushSize, 1f, 100f);
            var ratioBrush = totalDistanceInPixels * pressureDifference / brushCenterPartWidth;
            var ratioSource = totalDistanceInPixels / brushCenterPartWidth;
            var ratio = (ratioSource + ratioBrush) / totalDistanceInPixels;
            return ratio;
        }

        /// <summary>
        /// Creates line mesh
        /// </summary>
        /// <param name="renderPositions"></param>
        /// <param name="renderOffset"></param>
        /// <param name="brushTexture"></param>
        /// <param name="brushSizeActual"></param>
        /// <param name="brushSizes"></param>
        public void RenderLine(Vector2[] renderPositions, Vector2 renderOffset, Texture brushTexture, float brushSizeActual, float[] brushSizes)
        {
            var brushPressureStart = brushSizes[0];
            var brushPressureEnd = brushSizes[1];
            var brushWidth = brushTexture.width;
            var brushHeight = brushTexture.height;
            var maxBrushPressure = Mathf.Max(brushPressureStart, brushPressureEnd);
            var brushOffset = new Vector2(brushWidth, brushHeight) * maxBrushPressure;
            var distances = new float[renderPositions.Length / 2];
            var totalDistance = 0f;
            for (var i = 0; i < renderPositions.Length - 1; i += 2)
            {
                var from = renderPositions[i + 0];
                from = from.Clamp(Vector2.zero - brushOffset, textureSize + brushOffset);
                var to = renderPositions[i + 1];
                to = to.Clamp(Vector2.zero - brushOffset, textureSize + brushOffset);
                renderPositions[i + 0] = from;
                renderPositions[i + 1] = to;
                distances[i / 2] = Vector2.Distance(from, to);
                totalDistance += distances[i / 2];
            }

            var ratio = GetRatio(totalDistance, brushSizeActual, brushSizes) * 2f;
            var quadsCount = 0;
            for (var i = 0; i < renderPositions.Length - 1; i += 2)
            {
                quadsCount += (int)(distances[i / 2] * ratio + 1);
            }

            quadsCount = Mathf.Clamp(quadsCount, renderPositions.Length / 2, 16384);
            ClearMeshData();
            var count = 0;

            for (var i = 0; i < renderPositions.Length - 1; i += 2)
            {
                var from = renderPositions[i + 0];
                var to = renderPositions[i + 1];
                var currentDistance = Mathf.Max(1, (int)(distances[i / 2] * ratio));
                for (var j = 0; j < currentDistance; j++)
                {
                    var minDistance = Mathf.Max(1, (float) (quadsCount - 1));
                    var t = Mathf.Clamp(count / minDistance, 0, 1);
                    var thickness = Mathf.Lerp(brushPressureStart, brushPressureEnd, t);
                    var holePosition = from + (to - from) / currentDistance * j;
                    
                    var positionRect = new Rect(
                        (holePosition.x - (0.5f - renderOffset.x) * brushWidth * thickness) / textureSize.x,
                        (holePosition.y - (0.5f - renderOffset.y) * brushHeight * thickness) / textureSize.y,
                        brushWidth * thickness / textureSize.x,
                        brushHeight * thickness / textureSize.y
                    );

                    positions.Add (new Vector3(positionRect.xMin, positionRect.yMax, 0));
                    positions.Add (new Vector3(positionRect.xMax, positionRect.yMax, 0));
                    positions.Add(new Vector3(positionRect.xMax, positionRect.yMin, 0));
                    positions.Add(new Vector3(positionRect.xMin, positionRect.yMin, 0));

                    colors.Add(Color.white);
                    colors.Add(Color.white);
                    colors.Add(Color.white);
                    colors.Add(Color.white);

                    uv.Add(Vector2.up);
                    uv.Add(Vector2.one);
                    uv.Add(Vector2.right);
                    uv.Add(Vector2.zero);

                    indices.Add(0 + count * 4);
                    indices.Add(1 + count * 4);
                    indices.Add(2 + count * 4);
                    indices.Add(2 + count * 4);
                    indices.Add(3 + count * 4);
                    indices.Add(0 + count * 4);

                    count++;
                }
            }

            if (positions.Count > 0)
            {
                //BasePaintObjectRenderer.RenderLine
                drawLine(positions, uv, indices, colors);
            }

            ClearMeshData();
            drawPositions.Clear();
        }

        private void ClearMeshData()
        {
            positions.Clear();
            colors.Clear();
            indices.Clear();
            uv.Clear();
        }
    }
}