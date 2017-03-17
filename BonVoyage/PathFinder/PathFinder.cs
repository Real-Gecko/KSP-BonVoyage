using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
//using KSP

namespace BonVoyage
{
	// Hexagonal grid for pathfinding will be used
	// https://tbswithunity3d.wordpress.com/2012/02/23/hexagonal-grid-path-finding-using-a-algorithm/
	public class PathFinder
	{
		internal const double StepSize = 1000;
		public struct Point
		{
			public int X, Y;

			public Point(int x, int y)
			{
				X = x;
				Y = y;
			}
		}

		private class Tile
		{
			public int x;
			public int y;
			public Hex hex;

			public Tile(int x, int y, Hex hex) {
				this.x = x;
				this.y = y;
				this.hex = hex;
			}
		}

		private Dictionary<int, Point> directions;

		private double startLatitude;
		private double startLongitude;
		private double targetLatitude;
		private double targetLongitude;
		private CelestialBody mainBody;
		private List<Hex> tiles;
		internal Path<Hex> path;

		public PathFinder (double startLat, double startLon, double targetLat, double targetLon, CelestialBody body)
		{
			startLatitude = startLat;
			startLongitude = startLon;
			targetLatitude = targetLat;
			targetLongitude = targetLon;
			mainBody = body;
			estimate = Estimate;

			tiles = new List<Hex>();

			directions = new Dictionary<int, Point> ();
			directions.Add (0, new Point (0, -1)); // 0 degree
			directions.Add (60, new Point(1, -1)); // 60
			directions.Add (120, new Point(1, 0)); // 120
			directions.Add (180, new Point(0, 1)); // 180
			directions.Add (240, new Point(-1, 1)); // 240
			directions.Add (300, new Point(-1, 0)); // 300
		}

		public void FindPath() {
			double distanceToTarget = GeoUtils.GetDistance (startLatitude, startLongitude, targetLatitude, targetLongitude, mainBody.Radius);
			if (distanceToTarget < StepSize) return;
			double bearing = GeoUtils.InitialBearing (startLatitude, startLongitude, targetLatitude, targetLongitude);
			double altitude = GeoUtils.TerrainHeightAt (startLatitude, startLongitude, mainBody);
			int x = 0;
			int y = 0;
			Hex start = new Hex(startLatitude, startLongitude, altitude, bearing, x, y, this);
			tiles.Add (start);

			double straightPath = 0;

			//			ScreenMessages.PostScreenMessage ("building straight " + DateTime.Now.ToString());
			while (straightPath < distanceToTarget)
			{
				GetNeighbours(x, y, false);
				x += directions [0].X;
				y += directions [0].Y;
				straightPath += StepSize;
			}
			Hex destination = tiles.Find (t => (t.X == x + directions [180].X) && (t.Y == y + directions [180].Y));

			/*			KSP.IO.File.AppendAllText<BonVoyage> (
				//				String.Format("lat: {0}\nlon: {1}\nbea: {2}\n----\n", this.latitude, this.longitude, this.bearing),
				String.Format("start: {0}, destination: {1}\n----\n", start.Id, destination.Id),
				"path"
			);*/

			//			ScreenMessages.PostScreenMessage ("started caclulation " + DateTime.Now.ToString());
			path = Path<Hex>.FindPath<Hex> (start, destination, distance, estimate);
			ScreenMessages.PostScreenMessage ("Path build");
		}

		private double Estimate(Hex hex) {
			return GeoUtils.GetDistance (hex.Latitude, hex.Longitude, targetLatitude, targetLongitude, mainBody.Radius);
		}

		Func<Hex, Hex, double> distance = (node1, node2) => StepSize;
		Func<Hex, double> estimate;

		internal IEnumerable<Hex> GetNeighbours(int x, int y, bool passable = true) {
			//			Debug.Log (String.Format("bonvoyage - finding neighbours for {0}, {1}", x, y));
			var tile = tiles.Find(t=> (t.X == x) && (t.Y == y));
			if (tile == null) {
				//				Debug.Log ("bonvoyage - tile not found");
				return null;
			}
			List<Hex> neighbours = new List<Hex> ();
			foreach (var direction in directions) {
				int dirX = direction.Value.X;
				int dirY = direction.Value.Y;
				var neighbour = tiles.Find(n => (n.X == tile.X + dirX) && (n.Y == tile.Y + dirY));
				if (neighbour == null) {
					//					Debug.Log ("bonvoyage - neighbour not found");
					double[] coords = GeoUtils.GetLatitudeLongitude (tile.Latitude, tile.Longitude, tile.Bearing + direction.Key, StepSize, mainBody.Radius);
					double newBearing = GeoUtils.FinalBearing (tile.Latitude, tile.Longitude, coords [0], coords [1]);
					newBearing = (newBearing - direction.Key + 360) % 360;
					double altitude = GeoUtils.TerrainHeightAt (coords [0], coords [1], mainBody);
					neighbour = new Hex (coords [0], coords [1], altitude, newBearing, tile.X + dirX, tile.Y + dirY, this);
				}
				neighbours.Add (neighbour);
				tiles.Add (neighbour);
			}
			if (passable) {
				return neighbours.Where (
					n => (n.Altitude >= 0 || !mainBody.ocean) &&
					((n.Altitude - tile.Altitude) < StepSize / 2) &&
					((n.Altitude - tile.Altitude) > 0 - StepSize / 2)
				);
			}
			else
				return neighbours;
		}

		public double GetDistance() {
			if (path != null)
			{
				Hex destination = path.LastStep;
				double appendix = GeoUtils.GetDistance(destination.Latitude, destination.Longitude, targetLatitude, targetLongitude, mainBody.Radius);
				return path.TotalCost + appendix;
			}
			else
				return 0;
		}
	}
}
