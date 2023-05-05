using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using XDPaint.Core;
using XDPaint.Core.PaintObject.Base;
using XDPaint.Tools.Image.Base;
using XDPaint.Utils;
using Object = UnityEngine.Object;

namespace XDPaint.Tools.Image
{
    [Serializable]
    public class BucketTool : BasePaintTool
    {
        private class BucketData
        {
            public RenderTexture Layer;
            public Vector2Int ClickPosition;
        }
        
        [Preserve] public BucketTool(IPaintData paintData) : base(paintData)
        {
        }

        public override PaintTool Type => PaintTool.Bucket;
        public override bool ShowPreview => false;
        public override bool AllowRender => false;
        public override bool ProcessingFinished => false;
        
        #region Bucket Settings

        [PaintToolProperty] public float Tolerance { get; set; } = 0.01f;
	    
        #endregion

        private Texture2D texture;
        private Color32[] pixels;
        private bool[] visitedPixels;
        private Queue<BucketData> bucketData;
        private BucketData currentData;
        private Thread thread;
        private bool isRunning;
#if UNITY_WEBGL
        private bool useThreads = false;
#else
        private bool useThreads = true;
#endif

        public override void Enter()
        {
            base.Enter();
            var layerTexture = GetTexture(RenderTarget.ActiveLayer);
            texture = new Texture2D(layerTexture.width, layerTexture.height, TextureFormat.ARGB32, false);
            bucketData = new Queue<BucketData>();
        }
        
        public override void Exit()
        {
            base.Exit();
            Object.Destroy(texture);
            texture = null;
            pixels = null;
            visitedPixels = null;
            bucketData.Clear();
            currentData = null;
            if (useThreads)
            {
                thread?.Abort();
            }
            isRunning = false;
        }

        public override void UpdateDown(Vector3 localPosition, Vector2 screenPosition, Vector2 uv, Vector2 paintPosition, float pressure)
        {
            base.UpdateDown(localPosition, screenPosition, uv, paintPosition, pressure);
            var layerTexture = GetTexture(RenderTarget.ActiveLayer);
            var textureSize = new Vector2Int(layerTexture.width, layerTexture.height);
            var fillPosition = new Vector2Int((int)(uv.x * textureSize.x), (int)(uv.y * textureSize.y));
            if (fillPosition.x >= 0 && fillPosition.x < texture.width && fillPosition.y >= 0 && fillPosition.y < texture.height)
            {
                var data = new BucketData
                {
                    Layer = layerTexture,
                    ClickPosition = fillPosition
                };
                bucketData.Enqueue(data);
            }
        }

        public override void OnDrawProcess(RenderTargetIdentifier combined)
        {
            var drawProcessed = false;
            if (!isRunning && bucketData.Count > 0)
            {
                base.OnDrawProcess(combined);
                drawProcessed = true;
                currentData = bucketData.Dequeue();
                isRunning = true;
                var previousRenderTexture = RenderTexture.active;
                RenderTexture.active = currentData.Layer;
                texture.ReadPixels(new Rect(0, 0, currentData.Layer.width, currentData.Layer.height), 0, 0, false);
                texture.Apply();
                RenderTexture.active = previousRenderTexture;
                pixels = texture.GetPixels32();
                visitedPixels = new bool[pixels.Length];
                var width = texture.width;
                var height = texture.height;
                var color = Data.Brush.Color;
                if (useThreads)
                {
                    thread = new Thread(() =>
                    {
                        FillTexture(color, width, height, currentData.ClickPosition.x, currentData.ClickPosition.y);
                    });
                    thread.IsBackground = true;
                    thread.SetApartmentState(ApartmentState.STA);
                    thread.Start();
                }
                else
                {
                    FillTexture(color, width, height, currentData.ClickPosition.x, currentData.ClickPosition.y);
                }
                Data.StartCoroutine(WaitForFillEnd());
            }
            else if (isRunning && (!useThreads || (useThreads && !thread.IsAlive)))
            {
                texture.SetPixels32(pixels);
                texture.Apply();
                Graphics.Blit(texture, currentData.Layer);
                currentData = null;
                visitedPixels = null;
                isRunning = false;
                Data.SaveState();
                base.OnDrawProcess(combined);
                drawProcessed = true;
            }

            if (!drawProcessed)
            {
                base.OnDrawProcess(combined);
            }
        }

        private IEnumerator WaitForFillEnd()
        {
            while (useThreads && thread.IsAlive)
            {
                yield return null;
            }

            if (!Data.IsPainting && !Data.Brush.Preview)
            {
                Data.Render();
            }
        }

        private void FillTexture(Color32 fillColor, int width, int height, int x, int y)
        {
            var targetColor = pixels[x + y * width];
            var eps = Mathf.RoundToInt(Mathf.Clamp(Tolerance, 0f, 1f) * 127.5f);
            var position = new Vector2Int(x, y);
            var positions = new Stack<Vector2Int>();
            positions.Push(position);
            while (positions.Count > 0)
            {
                position = positions.Pop();

                if (visitedPixels[position.x + position.y * width] || !pixels[position.x + position.y * width].AreColorsSimilar(targetColor, eps))
                    continue;

                var leftX = position.x;
                var rightX = position.x;
                y = position.y;

                while (leftX > 0 && pixels[leftX - 1 + y * width].AreColorsSimilar(targetColor, eps))
                {
                    leftX--;
                }

                while (rightX < width - 1 && pixels[rightX + 1 + y * width].AreColorsSimilar(targetColor, eps))
                {
                    rightX++;
                }

                for (var i = leftX; i <= rightX; i++)
                {
                    if (!visitedPixels[i + y * width])
                    {
                        pixels[i + y * width] = fillColor;
                        visitedPixels[i + y * width] = true;

                        if (y > 0 && !visitedPixels[i + (y - 1) * width] && pixels[i + (y - 1) * width].AreColorsSimilar(targetColor, eps))
                        {
                            position.x = i;
                            position.y = y - 1;
                            positions.Push(position);
                        }

                        if (y < height - 1 && !visitedPixels[i + (y + 1) * width] && pixels[i + (y + 1) * width].AreColorsSimilar(targetColor, eps))
                        {
                            position.x = i;
                            position.y = y + 1;
                            positions.Push(position);
                        }
                    }
                }
            }
        }
    }
}