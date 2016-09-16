using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BonVoyage
{
	public class Hex : IHasNeighbours<Hex> {
		private double latitude;
		public double Latitude { get { return latitude; } }

		private double longitude;
		public double Longitude { get { return longitude; } }

		public double altitude;
		public double Altitude { get { return altitude; } }

		private double bearing;
		public double Bearing { get { return bearing; } }

		private int x;
		public int X { get { return x; } }

		private int y;
		public int Y { get { return y; } }

		private PathFinder parent;

		public Hex(double latitude, double longitude, double altitude, double bearing, int x, int y, PathFinder parent) {
			this.latitude = latitude;
			this.longitude = longitude;
			this.altitude = altitude;
			this.bearing = bearing;
			this.x = x;
			this.y = y;
			this.parent = parent;
		}

		public IEnumerable<Hex> Neighbours {
			get {
				return parent.GetNeighbours (this.x, this.y);
			}
		}
	}
}
