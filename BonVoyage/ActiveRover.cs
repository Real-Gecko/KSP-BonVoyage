using System;
using System.Collections.Generic;

namespace BonVoyage
{
	public class ActiveRover {
		public struct WayPoint {
			public double latitude;
			public double longitude;
			public WayPoint(double lat, double lon) {
				latitude = lat;
				longitude = lon;
			}
		}

		public string status;
		public double toTravel;

		public Vessel vessel;
		public ConfigNode vesselConfigNode;
		public double lastTime;
		public double targetLatitude;
		public double targetLongitude;
		public double averageSpeed;
		public double distanceTravelled;
		public double distanceToTarget;
		public bool solarPowered;
		public bool bvActive;
		public ConfigNode BVModule;
		public List<WayPoint> path;
		public ActiveRover(Vessel v) {
			vessel = v;

			vesselConfigNode = new ConfigNode ();
			vessel.protoVessel.Save (vesselConfigNode);

			// This is annoying
			var BVPart = vesselConfigNode.GetNode ("PART", "name", "BonVoyageModule");
			if (BVPart == null)
				BVPart = vesselConfigNode.GetNode ("PART", "name", "Malemute.RoverCab");
			if (BVPart == null)
				BVPart = vesselConfigNode.GetNode ("PART", "name", "KER.RoverCab");
			if (BVPart == null)
				BVPart = vesselConfigNode.GetNode ("PART", "name", "WBI.BuffaloCab");
			if (BVPart == null)
				BVPart = vesselConfigNode.GetNode("PART", "name", "ARESrovercockpit");
			if (BVPart == null)
				BVPart = vesselConfigNode.GetNode("PART", "name", "Puma Pod");
			if (BVPart == null)
				return;

			BVModule = BVPart.GetNode ("MODULE", "name", "BonVoyageModule");
			if (BVModule == null)
				return;

			bvActive = bool.Parse (BVModule.GetValue ("isActive"));

			lastTime = double.Parse (BVModule.GetValue ("lastTime"));
			distanceTravelled = double.Parse (BVModule.GetValue ("distanceTravelled"));
			distanceToTarget = double.Parse (BVModule.GetValue ("distanceToTarget"));
			solarPowered = bool.Parse (BVModule.GetValue ("solarPowered"));
			targetLatitude = double.Parse (BVModule.GetValue ("targetLatitude"));
			targetLongitude = double.Parse (BVModule.GetValue ("targetLongitude"));
			averageSpeed = double.Parse(BVModule.GetValue ("averageSpeed"));
			path = new List<WayPoint> ();
			DecodePath ();
		}

		private void DecodePath() {
			string p = BVModule.GetValue("pathEncoded");
			if (p == null)
				return;
			char[] separators = new char[] { ';' };
			string[] wps = p.Split (separators, StringSplitOptions.RemoveEmptyEntries);
			foreach (var wp in wps) {
				string[] latlon = wp.Split (':');
				path.Add (new WayPoint (double.Parse(latlon [0]), double.Parse(latlon [1])));
			}
			path.Reverse (); // Don't ask me...
		}

		public double yetToTravel {
			get { return distanceToTarget - distanceTravelled; }
		}
	}
}
