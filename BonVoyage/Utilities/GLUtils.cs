using System;
using System.Collections.Generic;
using UnityEngine;

namespace BonVoyage
{
	public class GLUtils
	{
		/// <summary>
		/// Creates GL vertex for map or standard view.
		/// Stolen from MechJeb2 source code :D
		/// </summary>
		/// <param name="worldPosition">World position.</param>
		/// <param name="map">If set to <c>true</c> map.</param>
		internal static void GLVertex(Vector3d worldPosition, bool mapView = true)
		{
			Vector3 screenPoint = mapView ? PlanetariumCamera.Camera.WorldToViewportPoint(ScaledSpace.LocalToScaledSpace(worldPosition)) : FlightCamera.fetch.mainCamera.WorldToViewportPoint(worldPosition);
			GL.Vertex3(screenPoint.x, screenPoint.y, 0);
		}

		/// <summary>
		/// Draws the curve.
		/// </summary>
		/// <param name="points">Points.</param>
		/// <param name="mapView">If set to <c>true</c> map view.</param>
		internal static void DrawCurve(List<Vector3d> points, bool mapView = true) {
			for(int i = 0; i < points.Count - 1; i++)
			{
				GL.PushMatrix ();
				GL.LoadOrtho ();
				GL.Begin (GL.LINES);
				GL.Color (Color.red);
				GLVertex (points[i], true);
				GLVertex (points[i + 1], true);
				GL.End ();
				GL.PopMatrix ();
			}
		}
	}
}

