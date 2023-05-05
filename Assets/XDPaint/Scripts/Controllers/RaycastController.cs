using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XDPaint.Core;
using XDPaint.Tools.Raycast;
using XDPaint.Tools.Raycast.Base;
using XDPaint.Utils;

namespace XDPaint.Controllers
{
	public class RaycastController : Singleton<RaycastController>
	{
		private readonly List<IRaycastMeshData> meshesData = new List<IRaycastMeshData>();

		public void InitObject(IPaintManager paintManager, Component paintComponent, Component renderComponent)
		{
			DestroyMeshData(paintManager);
			
			var raycastMeshData = meshesData.FirstOrDefault(x => x.Transform == paintComponent.transform);
			if (raycastMeshData == null)
			{
				if (renderComponent is SkinnedMeshRenderer)
				{
					raycastMeshData = new RaycastSkinnedMeshRendererData();
				}
				else if (renderComponent is MeshRenderer)
				{
					raycastMeshData = new RaycastMeshRendererData();
				}

				if (raycastMeshData != null)
				{
					raycastMeshData.Init(paintComponent, renderComponent);
					meshesData.Add(raycastMeshData);
				}
				else
				{
					Debug.LogError("RaycastMeshData is null!");
					return;
				}
			}
			
			if (paintManager.Triangles != null)
			{
				foreach (var triangle in paintManager.Triangles)
				{
					triangle.SetRaycastMeshData(raycastMeshData, paintManager.UVChannel);
				}
			}
			raycastMeshData.AddPaintManager(paintManager);
		}

		public Mesh GetMesh(IPaintManager paintManager)
		{
			return meshesData.Find(x => x.PaintManagers.Contains(paintManager)).Mesh;
		}

		public void DestroyMeshData(IPaintManager paintManager)
		{
			for (var i = meshesData.Count - 1; i >= 0; i--)
			{
				if (meshesData[i].PaintManagers.Count == 1 && meshesData[i].PaintManagers.ElementAt(0) == paintManager)
				{
					meshesData[i].DoDispose();
					meshesData.RemoveAt(i);
					break;
				}

				if (meshesData[i].PaintManagers.Count > 1 && meshesData[i].PaintManagers.Contains(paintManager))
				{
					meshesData[i].RemovePaintManager(paintManager);
					break;
				}
			}
		}
		
		public Triangle Raycast(IPaintManager sender, Ray ray)
		{
			var raycastResults = new List<Triangle>();
			foreach (var meshData in meshesData)
			{
				if (meshData == null)
					continue;
				
				foreach (var paintManager in meshData.PaintManagers)
				{
					if (paintManager == null)
						continue;
					
					var paintManagerComponent = (PaintManager)paintManager;
					if (paintManager == sender && paintManagerComponent.gameObject.activeInHierarchy &&
					    paintManagerComponent.enabled)
					{
						var raycast = meshData.GetRaycast(sender, ray);
						if (raycast != null)
						{
							raycastResults.Add(raycast);
						}
					}
				}
			}
			return SortIntersects(raycastResults);
		}

		public Triangle RaycastLocal(IPaintManager paintManager, Ray ray)
		{
			var raycastResults = new List<Triangle>();
			foreach (var meshData in meshesData)
			{
				var raycast = meshData?.GetRaycast(paintManager, ray, true, false);
				if (raycast != null)
				{
					raycastResults.Add(raycast);
				}
			}
			return SortIntersects(raycastResults);
		}

		public Triangle NeighborsRaycast(IPaintManager sender, Triangle triangle, Ray ray)
		{
			var raycastResults = new List<Triangle>();
			foreach (var meshData in meshesData)
			{
				var raycasts = meshData.GetNeighborsRaycasts(sender, triangle, ray);
				if (raycasts != null)
				{
					raycastResults.AddRange(raycasts);
				}
			}
			return SortIntersects(raycastResults);
		}

		private Triangle SortIntersects(IList<Triangle> triangles)
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
	}
}