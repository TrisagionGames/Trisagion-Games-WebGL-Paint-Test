using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using XDPaint.Core.Layers;
using XDPaint.Core.Materials;
using XDPaint.Core.PaintModes;
using XDPaint.Tools.Image.Base;
using XDPaint.Tools.Raycast;
using XDPaint.Utils;

namespace XDPaint.Core.PaintObject.Base
{
	public class BasePaintObjectRenderer : IDisposable
	{
		public bool UseNeighborsVertices { set => lineDrawer.UseNeighborsVertices = value; }
		public IBrush Brush { get; set; }
		public IPaintTool Tool { get; set; }
		public bool InBounds { get; protected set; }
		protected Camera Camera { set => lineDrawer.Camera = value; }
		
		protected Paint PaintMaterial;
		protected bool IsPaintingDone;
		protected IPaintMode PaintMode;
		protected IRenderTextureHelper RenderTextureHelper;
		protected Func<ILayer> ActiveLayer;
		private BaseLineDrawer lineDrawer;
		private Mesh mesh;
		private Mesh quadMesh;
		private CommandBufferBuilder commandBufferBuilder;
		
		public void SetPaintMode(IPaintMode paintMode)
		{
			PaintMode = paintMode;
		}

		public void SetActiveLayer(Func<ILayer> getActiveLayer)
		{
			ActiveLayer = getActiveLayer;
		}

		protected void InitRenderer(IPaintManager paintManager, Camera camera, Paint paint)
		{
			mesh = new Mesh();
			PaintMaterial = paint;
			var sourceTextureSize = new Vector2(paint.SourceTexture.width, paint.SourceTexture.height);
			lineDrawer = new BaseLineDrawer(paintManager);
			lineDrawer.Init(camera, sourceTextureSize, RenderLine);
			commandBufferBuilder = new CommandBufferBuilder("XDPaintObject");
			InitQuadMesh();
		}

		private void InitQuadMesh()
		{
			if (quadMesh == null)
			{
				quadMesh = MeshGenerator.GenerateQuad(Vector3.one, Vector3.zero);
			}
		}

		public virtual void DoDispose()
		{
			commandBufferBuilder?.Release();
			if (mesh != null)
			{
				UnityEngine.Object.Destroy(mesh);
			}
			if (quadMesh != null)
			{
				UnityEngine.Object.Destroy(quadMesh);
			}
		}

		protected void ClearTexture(RenderTarget target)
		{
			commandBufferBuilder.Clear().SetRenderTarget(RenderTextureHelper.GetTarget(target)).ClearRenderTarget().Execute();
		}
		
		protected void ClearTexture(RenderTexture renderTexture, Color color)
		{
			commandBufferBuilder.Clear().SetRenderTarget(renderTexture).ClearRenderTarget(color).Execute();
		}

		private RenderTargetIdentifier GetRenderTarget(RenderTarget target)
		{
			return target == RenderTarget.ActiveLayer ? ActiveLayer().RenderTexture : RenderTextureHelper.GetTarget(target);
		}
		
		private void ClearTextureAndRender(RenderTarget target, Mesh drawMesh)
		{
			commandBufferBuilder.Clear().SetRenderTarget(RenderTextureHelper.GetTarget(target)).ClearRenderTarget().DrawMesh(drawMesh, Brush.Material).Execute();
		}
		
		private void ClearTextureAndRenderInstanced(RenderTarget target, Mesh drawMesh, Matrix4x4[] matrix)
		{
			commandBufferBuilder.Clear().SetRenderTarget(RenderTextureHelper.GetTarget(target)).ClearRenderTarget().DrawMeshInstanced(drawMesh, Brush.Material, matrix).Execute();
		}

		private void RenderToTexture(RenderTarget target, Mesh drawMesh)
		{
			if (!Tool.RenderToLayer && target == RenderTarget.ActiveLayer)
				return;
			
			if (!Tool.RenderToInput && target == RenderTarget.Input)
				return;

			commandBufferBuilder.Clear().SetRenderTarget(GetRenderTarget(target)).DrawMesh(drawMesh, Brush.Material).Execute();

			//Colorize PaintInput texture
			if (target == RenderTarget.Input)
			{
				commandBufferBuilder.Clear().SetRenderTarget(RenderTextureHelper.GetTarget(RenderTarget.Input)).DrawMesh(drawMesh, Brush.Material, Brush.Material.passCount - 1).Execute();
			}
		}
		
		private void RenderToTextureInstanced(RenderTarget target, Mesh drawMesh, Matrix4x4[] matrix)
		{
			if (!Tool.RenderToLayer && target == RenderTarget.ActiveLayer)
				return;
			
			if (!Tool.RenderToInput && target == RenderTarget.Input)
				return;

			commandBufferBuilder.Clear().SetRenderTarget(GetRenderTarget(target)).DrawMeshInstanced(drawMesh, Brush.Material, matrix, 0).Execute();

			//Colorize PaintInput texture
			if (target == RenderTarget.Input)
			{
				commandBufferBuilder.Clear().SetRenderTarget(RenderTextureHelper.GetTarget(RenderTarget.Input)).DrawMeshInstanced(drawMesh, Brush.Material, matrix, Brush.Material.passCount - 1).Execute();
			}
		}

		protected void DrawPreProcess()
		{
			if (Tool.DrawPreProcess)
			{
				Tool.OnDrawPreProcess(RenderTextureHelper.GetTarget(RenderTarget.Combined));
			}
		}

		protected void DrawProcess()
		{
			if (Tool.DrawProcess)
			{
				Tool.OnDrawProcess(RenderTextureHelper.GetTarget(RenderTarget.Combined));
			}
		}

		protected void BakeInputToPaint()
		{
			if (Tool.BakeInputToPaint)
			{
				Tool.OnBakeInputToLayer(ActiveLayer().RenderTarget);
			}
		}

		protected void RenderQuad(Rect positionRect)
		{
			quadMesh.SetVertices(new[]
			{
				new Vector3(positionRect.xMin, positionRect.yMax, 0),
				new Vector3(positionRect.xMax, positionRect.yMax, 0),
				new Vector3(positionRect.xMax, positionRect.yMin, 0),
				new Vector3(positionRect.xMin, positionRect.yMin, 0)
			});
			quadMesh.SetUVs(0, new[] {Vector2.up, Vector2.one, Vector2.right, Vector2.zero});
			GL.LoadOrtho();
			if (Tool.RenderToLayer)
			{
				RenderToTexture(PaintMode.RenderTarget, quadMesh);
			}
			if (Tool.RenderToInput)
			{
				RenderToLineTexture(quadMesh);
			}
		}

		protected Vector2[] GetLinePositions(Vector2 fistPaintPos, Vector2 lastPaintPos, Triangle firstTriangle, Triangle lastTriangle)
		{
			return lineDrawer.GetLinePositions(fistPaintPos, lastPaintPos, firstTriangle, lastTriangle);
		}

		protected void RenderLine(Vector2[] drawLine, Vector2 renderOffset, Texture brushTexture, float brushSizeActual, float[] brushSizes)
		{
			lineDrawer.RenderLine(drawLine, renderOffset, brushTexture, brushSizeActual, brushSizes);
		}

		private void RenderToLineTexture(Mesh renderMesh)
		{
			if (Tool.RenderToInput)
			{
				if (PaintMode.UsePaintInput)
				{
					RenderToTexture(RenderTarget.Input, renderMesh);
				}
				else
				{
					ClearTextureAndRender(RenderTarget.Input, renderMesh);
				}
			}
		}
		
		private void RenderToLineTextureInstanced(Mesh renderMesh, Matrix4x4[] matrix)
		{
			if (Tool.RenderToInput)
			{
				if (PaintMode.UsePaintInput)
				{
					RenderToTextureInstanced(RenderTarget.Input, renderMesh, matrix);
				}
				else
				{
					ClearTextureAndRenderInstanced(RenderTarget.Input, renderMesh, matrix);
				}
			}
		}

		private void RenderLine(List<Vector3> positions, List<Vector2> uv, List<int> indices, List<Color> colors)
		{
			if (mesh != null)
			{
				mesh.Clear(false);
			}
			mesh.SetVertices(positions);
			mesh.SetUVs(0, uv);
			mesh.SetTriangles(indices, 0);
			mesh.SetColors(colors);
			if (PaintMode.UsePaintInput)
			{
				Brush.Material.SetInt(Constants.BrushShader.DstAlphaBlend, (int)BlendMode.One);
			}
			GL.LoadOrtho();
			RenderToTexture(PaintMode.RenderTarget, mesh);
			RenderToLineTexture(mesh);
		}
	}
}