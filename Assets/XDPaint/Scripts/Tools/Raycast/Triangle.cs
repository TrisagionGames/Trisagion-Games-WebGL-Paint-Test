using System;
using System.Collections.Generic;
using UnityEngine;
using XDPaint.Tools.Raycast.Base;

namespace XDPaint.Tools.Raycast
{
	[Serializable]
	public class Triangle
	{
		//Triangle id
		public int Id;
		//Index 0
		public int I0;
		//Index 1
		public int I1;
		//Index 2
		public int I2;
		//Neighbors
		public List<int> N = new List<int>();

		private IRaycastMeshData meshData;
		private int uvChannel;
		private Barycentric barycentricLocal;

		public Transform Transform => meshData.Transform;
		public Vector3 Position0 => meshData.Vertices[I0];
		public Vector3 Position1 => meshData.Vertices[I1];
		public Vector3 Position2 => meshData.Vertices[I2];

		public Vector3 Hit
		{
			get
			{
				if (barycentricLocal == null)
				{
					barycentricLocal = new Barycentric();
				}
				return barycentricLocal.Interpolate(Position0, Position1, Position2);
			}
			set => barycentricLocal = new Barycentric(Position0, Position1, Position2, value);
		}

		public Vector3 WorldHit
		{
			get
			{
				var localHit = Hit;
				return Transform.localToWorldMatrix.MultiplyPoint(localHit);
			}
		}
		
		public Vector2 UV0 => meshData.GetUV(uvChannel, I0);
		public Vector2 UV1 => meshData.GetUV(uvChannel, I1);
		public Vector2 UV2 => meshData.GetUV(uvChannel, I2);
		
		private Vector2 uvHit;
		public Vector2 UVHit
		{
			get => uvHit;
			set => uvHit = value;
		}

		public Triangle(int id, int index0, int index1, int index2)
		{
			Id = id;
			I0 = index0;
			I1 = index1;
			I2 = index2;
		}

		public void SetRaycastMeshData(IRaycastMeshData raycastMeshData, int uvChannelIndex = 0)
		{
			meshData = raycastMeshData;
			uvChannel = uvChannelIndex;
		}
	}
}