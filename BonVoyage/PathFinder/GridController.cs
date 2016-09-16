using System;
using System.Collections.Generic;

namespace BonVoyage
{/*
	public class GridController
	{
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

		private static List<Point> NeighbourShift
		{
			get
			{
				return new List<Point>
				{
					new Point(0, -1), // 0 degree
					new Point(1, -1), // 60
					new Point(1, 0), // 120
					new Point(0, 1), // 180
					new Point(-1, 1), // 240
					new Point(-1, 0), // 300
				};
			}
		}

		private double startLatitude;
		private double startLongitude;
		private double targetLatitude;
		private double targetLongitude;
		private double bodyRadius;

		private double bearing;
		double Bearing {
			get { return bearing; }
		}

		private double radius;
		private List<Tile> tiles;
//		Hex start;

		public GridController() {//double startLatitude, double startLongitude, double targetLatitude, double targetLongitude, double radius) {
			this.startLatitude = startLatitude;
			this.startLongitude = startLongitude;
			this.targetLatitude = targetLatitude;
			this.targetLongitude = targetLongitude;
			this.radius = radius;
			tiles = new List<Tile> ();
			directions.Add (0, new Point (0, -1)); // 0 degree
			directions.Add (60, new Point(1, -1)); // 60
			directions.Add (120, new Point(1, 0)); // 120
			directions.Add (180, new Point(0, 1)); // 180
			directions.Add (240, new Point(-1, 1)); // 240
			directions.Add (300, new Point(-1, 0)); // 300
//			start = new Hex (startLatitude, startLongitude, 0, true, 0, 0, 0);
//			tiles.Add (start);
		}

		public Tile AddTile(int x, int y, Hex hex) {
			Tile tile = new Tile(x, y, hex);
			tiles.Add (tile);
			return tile;
		}

		public IEnumerable<Hex> GetNeighbours(int x, int y) {
			var tile = tiles.Find(t => (t.x == x && t.y == y));
			if (tile == null) {
				return null;
			}
			List<Hex> neighbours = new List<Hex> ();
			foreach (var direction in directions) {
				int nx = direction.Value.X;
				int ny = direction.Value.Y;
				var neighbour = tiles.Find(n => (n.x == nx && n.y == ny));
				if (neighbour == null) {
					double[] coords = GeoUtils.GetLatitudeLongitude (tile.hex.Latitude, tile.hex.Longitude, bearing + direction.Key, 1000, bodyRadius);
					double newBearing = GeoUtils.FinalBearing (tile.hex.Latitude, tile.hex.Longitude, coords [0], coords [1]);
					newBearing = (newBearing - direction.Key + 360) % 360;
					neighbour = AddTile (x + nx, y + ny, new Hex (coords[0], coords[1], newBearing, null, targetLatitude, targetLongitude));
				}
				neighbours.Add (neighbour.hex);
			}
			return neighbours;
		}
	}*/
}
