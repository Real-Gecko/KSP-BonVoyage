using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;

namespace BonVoyage
{
	public class ActiveRover {
		public string status;
		public double toTravel;

		public Vessel vessel;
		private ConfigNode vesselConfigNode;
		private double lastTime;
		public double LastTime { get { return lastTime; } }
		private double targetLatitude;
		private double targetLongitude;

		private double averageSpeed;
        private double averageSpeedAtNight;
        private double speedMultiplier;
        private double angle;
        public double AverageSpeed { get { return ((angle <= 90) ? (averageSpeed * speedMultiplier) : (averageSpeedAtNight * speedMultiplier)); } }

        private double distanceTravelled;
		private double distanceToTarget;
		public double yetToTravel { get { return distanceToTarget - distanceTravelled; } }

		private bool solarPowered;
		public bool bvActive;
		private bool isManned;
		private ConfigNode BVModule;
		private List<PathUtils.WayPoint> path;

		/// <summary>
		/// Initializes a new instance of the <see cref="BonVoyage.ActiveRover"/> class.
		/// </summary>
		/// <param name="v">Vessel.</param>
		/// <param name="module">Bon Voyage Module.</param>
		/// <param name="vcf">Vessel Config Node.</param>
		public ActiveRover(Vessel v, ConfigNode module, ConfigNode vcf) {
			vessel = v;
			vesselConfigNode = vcf;

			BVModule = module;

			bvActive = bool.Parse (BVModule.GetValue ("isActive"));

			// Workaround for update from versions prior to 1.0
			try {
				isManned = bool.Parse (BVModule.GetValue ("isManned"));
			} catch {
				isManned = true;
			}

			solarPowered = bool.Parse (BVModule.GetValue ("solarPowered"));

			lastTime = double.Parse (BVModule.GetValue ("lastTime"));
			distanceTravelled = double.Parse (BVModule.GetValue ("distanceTravelled"));
			distanceToTarget = double.Parse (BVModule.GetValue ("distanceToTarget"));
			targetLatitude = double.Parse (BVModule.GetValue ("targetLatitude"));
			targetLongitude = double.Parse (BVModule.GetValue ("targetLongitude"));
			averageSpeed = double.Parse(BVModule.GetValue ("averageSpeed"));
            if (BVModule.HasValue("averageSpeedAtNight")) // Backward compatibility
                averageSpeedAtNight = double.Parse(BVModule.GetValue("averageSpeedAtNight"));
            else
            {
                if (!solarPowered)
                    averageSpeedAtNight = averageSpeed;
                else
                    averageSpeedAtNight = 0;
            }

            path = PathUtils.DecodePath(BVModule.GetValue("pathEncoded"));
			speedMultiplier = 1.0;
		}

		/// <summary>
		/// Update rover.
		/// </summary>
		/// <param name="currentTime">Current time.</param>
		public void Update(double currentTime) {
			if (vessel.isActiveVessel)
			{
				status = "current";
				return;
			}

			if (!bvActive || vessel.loaded)
			{
				status = "idle";
				return;
			}

			Vector3d vesselPos = vessel.mainBody.position - vessel.GetWorldPos3D();
			Vector3d toKerbol = vessel.mainBody.position - FlightGlobals.Bodies[0].position;
            //double angle = Vector3d.Angle(vesselPos, toKerbol);
            angle = Vector3d.Angle(vesselPos, toKerbol);

            // Speed penalties at twighlight and at night
            if (angle > 90 && isManned)
				speedMultiplier = 0.25;
			else if (angle > 85 && isManned)
				speedMultiplier = 0.5;
			else if (angle > 80 && isManned)
				speedMultiplier = 0.75;
			else
				speedMultiplier = 1.0;

            // No moving at night, or when there's not enougth solar light for solar powered rovers
            //if (angle > 90 && solarPowered)
            // No moving at night, if there isn't power source
            if ((angle > 90) && (averageSpeedAtNight == 0.0))
            {
                status = "awaiting sunlight";
				lastTime = currentTime;
				BVModule.SetValue("lastTime", currentTime.ToString());
				vessel.protoVessel = new ProtoVessel(vesselConfigNode, HighLogic.CurrentGame);
				return;
			}

			double deltaT = currentTime - lastTime;

			double deltaS = AverageSpeed * deltaT;
			double bearing = GeoUtils.InitialBearing(
				vessel.latitude,
				vessel.longitude,
				targetLatitude,
				targetLongitude
			);
			distanceTravelled += deltaS;
			if (distanceTravelled >= distanceToTarget)
			{
				if (!MoveSafe (targetLatitude, targetLongitude))
					distanceTravelled -= deltaS;
				else {
					distanceTravelled = distanceToTarget;

					bvActive = false;
					BVModule.SetValue ("isActive", "False");
					BVModule.SetValue ("distanceTravelled", distanceToTarget.ToString ());
					BVModule.SetValue ("pathEncoded", "");

					if (BonVoyage.Instance.AutoDewarp) {
						if (TimeWarp.CurrentRate > 3)
							TimeWarp.SetRate (3, true);
						if (TimeWarp.CurrentRate > 0)
							TimeWarp.SetRate (0, false);
						ScreenMessages.PostScreenMessage (vessel.vesselName + " has arrived to destination at " + vessel.mainBody.name);
					}
					HoneyImHome ();
				}
				status = "idle";
			}
			else {
				int step = Convert.ToInt32(Math.Floor(distanceTravelled / PathFinder.StepSize));
				double remainder = distanceTravelled % PathFinder.StepSize;

				if (step < path.Count - 1)
					bearing = GeoUtils.InitialBearing(
						path[step].latitude,
						path[step].longitude,
						path[step + 1].latitude,
						path[step + 1].longitude
					);
				else
					bearing = GeoUtils.InitialBearing(
						path[step].latitude,
						path[step].longitude,
						targetLatitude,
						targetLongitude
					);

				double[] newCoordinates = GeoUtils.GetLatitudeLongitude(
					path[step].latitude,
					path[step].longitude,
					bearing,
					remainder,
					vessel.mainBody.Radius
				);
                
				if (!MoveSafe (newCoordinates [0], newCoordinates [1])) {
					distanceTravelled -= deltaS;
					status = "idle";
				} else
					status = "roving";
			}
			Save (currentTime);
		}

		/// <summary>
		/// Save data to ProtoVessel.
		/// </summary>
		public void Save(double currentTime) {
			lastTime = currentTime;
			vesselConfigNode.SetValue("lat", vessel.latitude.ToString());
			vesselConfigNode.SetValue("lon", vessel.longitude.ToString());
			vesselConfigNode.SetValue("alt", vessel.altitude.ToString());
			vesselConfigNode.SetValue("landedAt", vessel.mainBody.bodyDisplayName);
			BVModule.SetValue("distanceTravelled", (distanceTravelled).ToString());
			BVModule.SetValue("lastTime", currentTime.ToString());
			vessel.protoVessel = new ProtoVessel(vesselConfigNode, HighLogic.CurrentGame);
		}

		/// <summary>
		/// Prevent crazy torpedoing active vessel :D
		/// </summary>
		/// <returns><c>true</c>, if rover was moved, <c>false</c> otherwise.</returns>
		/// <param name="latitude">Latitude.</param>
		/// <param name="longitude">Longitude.</param>
		private bool MoveSafe(double latitude, double longitude) {
			double altitude = GeoUtils.TerrainHeightAt(latitude, longitude, vessel.mainBody);
			if (FlightGlobals.ActiveVessel != null) {
				Vector3d newPos = vessel.mainBody.GetWorldSurfacePosition (latitude, longitude, altitude);
				Vector3d actPos = FlightGlobals.ActiveVessel.GetWorldPos3D ();
				double distance = Vector3d.Distance (newPos, actPos);
				if (distance <= 2400) {
					return false;
				}
			}

			vessel.latitude = latitude;
			vessel.longitude = longitude;
			vessel.altitude = altitude + vessel.heightFromTerrain;
			return true;
		}

		/// <summary>
		/// Notify that rover has arrived
		/// </summary>
		private void HoneyImHome() {
			MessageSystem.Message message = new MessageSystem.Message (
                "Rover arrived",
				//------------------------------------------
                "<color=#74B4E2>" + vessel.vesselName + "</color>" +
                " has arrived to destination\n<color=#AED6EE>LAT: " +
                targetLatitude.ToString ("F2") + "</color>\n<color=#AED6EE>LON: " +
                targetLongitude.ToString ("F2") +
                "</color>\n<color=#82BCE5>At " + vessel.mainBody.name + ".</color>\n" +
                "Distance travelled: " +
                "<color=#74B4E2>" + distanceTravelled.ToString ("N") + "</color> meters",
				//------------------------------------------
                MessageSystemButton.MessageButtonColor.GREEN,
                MessageSystemButton.ButtonIcons.COMPLETE
            );
			MessageSystem.Instance.AddMessage (message);
		}
	}
}
