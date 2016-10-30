using System;
using System.Collections.Generic;
using UnityEngine;

namespace BonVoyage
{
	public class BonVoyageModule : PartModule
	{
		private bool mapLocationMode;
		private bool showUtils = false;

		private List<Vector3d> wayPoints;

//		private Rect guiRect;
		private GUIStyle labelStyle;

		[KSPField(isPersistant = true, guiName = "Active", guiActive = true)]
		public bool isActive = false;

		[KSPField(isPersistant = true, guiName = "Target latitude", guiActive = true, guiFormat = "F2")]
		public double targetLatitude = 0;

		[KSPField(isPersistant = true, guiName = "Target longitude", guiActive = true, guiFormat = "F2")]
		public double targetLongitude = 0;

		[KSPField(isPersistant = true, guiName = "Distance to target", guiActive = true, guiFormat = "N0", guiUnits = " meters")]
		public double distanceToTarget = 0;

		[KSPField(isPersistant = true, guiName = "Distance travelled", guiActive = true, guiFormat = "N0", guiUnits = " meters")]
		public double distanceTravelled = 0;

		[KSPField(isPersistant = true, guiName = "Average speed", guiActive = true)]
		public double averageSpeed = 0;

		[KSPField(isPersistant = true, guiName = "Last Updated", guiActive = false)]
		public double lastTime = 0;

		[KSPField(isPersistant = true, guiName = "Solar powered", guiActive = false)]
		public bool solarPowered = true;

		[KSPField(isPersistant = true, guiName = "Is manned", guiActive = false)]
		public bool isManned = true;

		[KSPField(isPersistant = true, guiName = "pathEncoded", guiActive = false)]
		public string pathEncoded = "";

		[KSPEvent(guiActive = true, guiName = "Pick target on map")]
		public void PickTarget()
		{
			if (this.vessel.situation != Vessel.Situations.LANDED)
				return;
			Deactivate();
			MapView.EnterMapView();
			mapLocationMode = true;
		}

		[KSPEvent(guiActive = true, guiName = "Set to active target")]
		public void SetToActive()
		{
			ScreenMessages.PostScreenMessage (this.vessel.targetObject != null ? "target" : "no target");
			if (this.vessel.targetObject == null || this.vessel.situation != Vessel.Situations.LANDED)
				return;

			Vessel targetVessel = this.vessel.targetObject.GetVessel();
			if (targetVessel == null)
			{
				ScreenMessages.PostScreenMessage("Target some suitable vessel first!");
				return;
			}

			if (targetVessel.mainBody == this.vessel.mainBody && targetVessel.situation == Vessel.Situations.LANDED)
			{
				Deactivate();
				this.distanceToTarget = GeoUtils.GetDistance(
					this.vessel.latitude, this.vessel.longitude, targetVessel.latitude, targetVessel.longitude, this.vessel.mainBody.Radius
				);

				double bearing = GeoUtils.InitialBearing(this.vessel.latitude, this.vessel.longitude, targetVessel.latitude, targetVessel.longitude);
				// We don't want to spawn inside vessel
				if (distanceToTarget == 0)
					return;
				this.distanceToTarget -= 200;
				double[] newCoordinates = GeoUtils.GetLatitudeLongitude(this.vessel.latitude, this.vessel.longitude, bearing, distanceToTarget, this.vessel.mainBody.Radius);
				this.targetLatitude = newCoordinates[0];
				this.targetLongitude = newCoordinates[1];
				this.distanceTravelled = 0;
				FindPath();
			}
			else {
				ScreenMessages.PostScreenMessage("Your target is out there somewhere, this won't work!");
			}
		}

		[KSPEvent(guiActive = true, guiName = "Set to active waypoint", isPersistent = true)]
		private void SetToWaypoint() {
			NavWaypoint navPoint = NavWaypoint.fetch;
			if (navPoint == null || !navPoint.IsActive || navPoint.Body != this.vessel.mainBody) {
				ScreenMessages.PostScreenMessage ("No valid nav point");
			} else {
				Deactivate();
				this.targetLatitude = navPoint.Latitude;
				this.targetLongitude = navPoint.Longitude;
				this.distanceTravelled = 0;
				FindPath();
			}
		}

		[KSPEvent(guiActive = true, guiName = "Poehali!!!", isPersistent = true)]
		public void Activate()
		{
			if (distanceToTarget == 0)
			{
				ScreenMessages.PostScreenMessage("No path to target calculated");
				return;
			}
			if (this.vessel.situation != Vessel.Situations.LANDED)
			{
				ScreenMessages.PostScreenMessage("Something is wrong", 5);
				ScreenMessages.PostScreenMessage("Hmmmm, what can it be?", 6);
				ScreenMessages.PostScreenMessage("Ah, yes! You're not landed!", 7);
				return;
			}

			this.averageSpeed = 0;
			double powerRequired = 0;
			int inTheAir = 0;

			List<ModuleWheels.ModuleWheelMotor> operableWheels = new List<ModuleWheels.ModuleWheelMotor>();
			foreach (var part in this.vessel.parts)
			{
				ModuleWheels.ModuleWheelMotor wheelMotor = part.FindModuleImplementing<ModuleWheels.ModuleWheelMotor>();
				if (wheelMotor != null)
				{
					ModuleWheels.ModuleWheelDamage wheelDamage = part.FindModuleImplementing<ModuleWheels.ModuleWheelDamage>();
					if (wheelDamage != null)
					{ // Malemute and Karibou wheels do not implement moduleDamage, thus making this mod cheaty
						if (wheelDamage.isDamaged)
						{
							ScreenMessages.PostScreenMessage("Some wheels are broken, we're stuck!");
							return;
						}
						if (wheelDamage.currentDownForce == 0)
						{
							inTheAir++;
							continue;
						}
					}
					if (wheelMotor.motorEnabled)
					{
						//						powerRequired += wheelMotor.inputResource.rate;
						powerRequired += wheelMotor.avgResRate;
					}
					operableWheels.Add(wheelMotor);
				}
			}

			// Average speed will vary depending on number of wheels online from 50 to 70 percent of wheel max speed
			this.averageSpeed = GetAverageSpeed(operableWheels);

			// Looks like no wheels are on
			if (this.averageSpeed == 0)
			{
				ScreenMessages.PostScreenMessage("At least two wheels must be online!");
				return;
			}

			// No driving until 4 operable wheels are touching the ground
			if (inTheAir > 0 && operableWheels.Count < 4)
			{
				ScreenMessages.PostScreenMessage("Wheels are not touching the ground, are you serious???");
				return;
			}

			// What???
			if (operableWheels.Count < 4)
			{
				ScreenMessages.PostScreenMessage("Monocycles, bicycles and trycicles are not supported, bye!");
				return;
			}

			// Unmanned rovers drive with 80% speed penalty
			this.isManned = (this.vessel.GetCrewCount () > 0);
			if (!this.isManned) {
				averageSpeed = averageSpeed * 0.2;
				ScreenMessages.PostScreenMessage ("Rover is unmanned, 80% speed penalty!");
			}

			// Generally moving at high speed requires less power than wheels' max consumption
			// BV will require max online wheels consumption to be less than 35% of max power production
			powerRequired = powerRequired / 100 * 35;

			double solarPower = CalculateSolarPower();
			double otherPower = CalculateOtherPower();

			if (solarPower + otherPower < powerRequired)
			{
				ScreenMessages.PostScreenMessage("Your power production is low, do something with it!");
				return;
			}

			// If alternative power sources produce more then required
			// Rover will ride forever :D
			if (otherPower >= powerRequired)
				solarPowered = false;

			isActive = true;
			lastTime = Planetarium.GetUniversalTime();
/*			distanceToTarget = GeoUtils.GetDistance(
				this.vessel.latitude, this.vessel.longitude, targetLatitude, targetLongitude, this.vessel.mainBody.Radius
			);*/
			distanceTravelled = 0;
			Events["Activate"].active = false;
			Events["Deactivate"].active = true;
			BonVoyage.Instance.UpdateRoverState(this.vessel, true);
			ScreenMessages.PostScreenMessage("Bon Voyage!!!");
		}

		[KSPEvent(guiActive = true, guiName = "Deactivate", active = false, isPersistent = true)]
		public void Deactivate()
		{
			isActive = false;
			targetLatitude = 0;
			targetLongitude = 0;
			distanceTravelled = 0;
			distanceToTarget = 0;
			wayPoints.Clear ();
			Events["Activate"].active = true;
			Events["Deactivate"].active = false;
			BonVoyage.Instance.UpdateRoverState(this.vessel, false);
		}

		[KSPEvent(guiActive = true, guiName = "Toggle utilities")]
		public void ToggleUtils()
		{
			showUtils = !showUtils;
			Events["CalculateSolar"].active = showUtils;
			Events["CalculateOther"].active = showUtils;
			Events["CalculateAverageSpeed"].active = showUtils;
			Events["CalculatePowerRequirement"].active = showUtils;

			//Clean up previous builds
			Events["CalculateSolar"].guiActive = true;
			Events["CalculateOther"].guiActive = true;
			if (Events["FindPath"] != null)
			{
				Events["FindPath"].guiActive = false;
				Events["FindPath"].active = false;
			}
			if (Events["PickTest"] != null)
			{
				Events["PickTest"].guiActive = false;
				Events["PickTest"].active = false;
			}
		}

		private void FindPath()
		{
			distanceToTarget = 0;
//			wayPoints.Clear();

			PathFinder finder = new PathFinder(
				this.vessel.latitude,
				this.vessel.longitude,
				targetLatitude,
				targetLongitude,
				this.vessel.mainBody
			);
			finder.FindPath();
			distanceToTarget = finder.GetDistance();
			if (distanceToTarget > 0)
			{
				pathEncoded = PathUtils.EncodePath(finder.path);
				wayPoints = PathUtils.DecodePath (pathEncoded, this.vessel.mainBody);
			}
			else
				ScreenMessages.PostScreenMessage("No path found, bye!");
		}

		[KSPEvent(guiActive = true, guiName = "Calculate solar", active = false)]
		public void CalculateSolar()
		{
			double solarPower = CalculateSolarPower();
			ScreenMessages.PostScreenMessage(String.Format("{0:F} electric charge/second", solarPower));
		}

		[KSPEvent(guiActive = true, guiName = "Calculate other", active = false)]
		public void CalculateOther()
		{
			double otherPower = CalculateOtherPower();
			ScreenMessages.PostScreenMessage(String.Format("{0:F} electric charge/second", otherPower));
		}

		[KSPEvent(guiActive = true, guiName = "Calculate average speed", active = false)]
		public void CalculateAverageSpeed() {
			List<ModuleWheels.ModuleWheelMotor> operableWheels = new List<ModuleWheels.ModuleWheelMotor>();
			foreach (var part in this.vessel.parts)
			{
				ModuleWheels.ModuleWheelMotor wheelMotor = part.FindModuleImplementing<ModuleWheels.ModuleWheelMotor>();
				if (wheelMotor != null)
				{
					operableWheels.Add(wheelMotor);
				}
			}

			// Average speed will vary depending on number of wheels online from 50 to 70 percent of wheel max speed
			this.averageSpeed = GetAverageSpeed(operableWheels);
		}

		[KSPEvent(guiActive = true, guiName = "Calculate power requirement", active = false)]
		public void CalculatePowerRequirement()
		{
			double powerRequired = 0;
//			List<ModuleWheels.ModuleWheelMotor> operableWheels = new List<ModuleWheels.ModuleWheelMotor>();
			foreach (var part in this.vessel.parts)
			{
				ModuleWheels.ModuleWheelMotor wheelMotor = part.FindModuleImplementing<ModuleWheels.ModuleWheelMotor>();
				if (wheelMotor != null)
				{
					if (wheelMotor.motorEnabled)
						//						powerRequired += wheelMotor.inputResource.rate;
						powerRequired += wheelMotor.avgResRate;
				}
			}

			// Average speed will vary depending on number of wheels online from 50 to 70 percent of wheel max speed
			powerRequired = powerRequired / 100 * 35;
			ScreenMessages.PostScreenMessage("Current power requirements " + powerRequired.ToString("F2") + "/s");
		}

		public override string GetInfo()
		{
			return "Bon Voyage controller";
		}

		public override void OnStart(PartModule.StartState state)
		{
			if (HighLogic.LoadedSceneIsEditor)
				return;
			
//			guiRect = new Rect(0, 0, Screen.width, Screen.height / 2);
			labelStyle = new GUIStyle();
			labelStyle.stretchWidth = true;
			labelStyle.stretchHeight = true;
			labelStyle.alignment = TextAnchor.MiddleCenter;
			labelStyle.fontSize = Screen.height / 20;
			labelStyle.fontStyle = FontStyle.Bold;
			labelStyle.normal.textColor = Color.red;
			wayPoints = PathUtils.DecodePath (pathEncoded, this.vessel.mainBody);
		}

		private void Update() {
			if (isActive)
				lastTime = Planetarium.GetUniversalTime();
		}

		private void OnGUI()
		{
			if (HighLogic.LoadedSceneIsEditor)
				return;


			if (MapView.MapIsEnabled)
			{
				if (wayPoints.Count > 0)
				{
					GLUtils.DrawCurve (wayPoints);
				}
			}

			if (mapLocationMode)
			{
				if (!MapView.MapIsEnabled)
				{
					mapLocationMode = false;
					return;
				}

				PlaceTargetAtCursor();
				GUI.Label(
					new Rect(Input.mousePosition.x + 15, Screen.height - Input.mousePosition.y, 200, 50),
					"Latitude:" + this.targetLatitude.ToString("F") + "\n" +
					"Longitude:" + this.targetLongitude.ToString("F") + "\n" +
					"Biome:" + ScienceUtil.GetExperimentBiome(this.vessel.mainBody, targetLatitude, targetLongitude)
				);
				// Lock the waypoint if user clicks
				if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
				{
					FindPath();
					if (distanceToTarget > 0)
					{
						mapLocationMode = false;
						MapView.ExitMapView();
					}
				}
			}
		}

		private double GetAverageSpeed(List<ModuleWheels.ModuleWheelMotor> operableWheels)
		{
			double averageSpeed = 0;
			int wheelsOnline = 0;
			foreach (ModuleWheels.ModuleWheelMotor wheelMotor in operableWheels)
			{
				if (wheelMotor.motorEnabled)
				{
					wheelsOnline++;
					double maxWheelSpeed = 0;
					if (wheelMotor.part.name == "roverWheel1") //RoveMax Model M1 gives crazy values
						maxWheelSpeed = 42;
					else
						maxWheelSpeed = wheelMotor.wheelSpeedMax;
					averageSpeed = Math.Max(averageSpeed, maxWheelSpeed);
				}
			}
			if (wheelsOnline < 2)
				return 0;

			averageSpeed = averageSpeed / 100 * Math.Min(70, (40 + 5 * wheelsOnline));
			return averageSpeed;
		}

		private double CalculateSolarPower()
		{
			double solarPower = 0;
			double distanceToSun = this.vessel.distanceToSun;
			double solarFlux = PhysicsGlobals.SolarLuminosity / (12.566370614359172 * distanceToSun * distanceToSun);
			float multiplier = 1;

			foreach (var part in this.vessel.parts)
			{
				ModuleDeployableSolarPanel solarPanel = part.FindModuleImplementing<ModuleDeployableSolarPanel>();
				if (solarPanel == null)
					continue;
				if (solarPanel.deployState != ModuleDeployableSolarPanel.DeployState.BROKEN)
				{
					if (solarPanel.useCurve)
					{
						multiplier = solarPanel.powerCurve.Evaluate((float)distanceToSun);
					}
					else {
						multiplier = (float)(solarFlux / PhysicsGlobals.SolarLuminosityAtHome);
					}
					solarPower += solarPanel.chargeRate * multiplier;
				}
			}
			return solarPower;
		}

		private double CalculateOtherPower()
		{
			double otherPower = 0;
			foreach (var part in this.vessel.parts)
			{
				// Find standard RTGs
				ModuleGenerator powerModule = part.FindModuleImplementing<ModuleGenerator>();
				if (powerModule != null) {
					if (powerModule.generatorIsActive || powerModule.isAlwaysActive) {
						foreach (var resource in powerModule.resHandler.outputResources) {
							if (resource.name == "ElectricCharge") {
								otherPower += resource.rate * powerModule.efficiency;
							}
						}
					}
				}

				// Search for other generators
				PartModuleList modules = part.Modules;

				// Near future fission reactors
				foreach (var module in modules) {
					if (module.moduleName == "FissionGenerator") {
						otherPower += double.Parse (module.Fields.GetValue ("CurrentGeneration").ToString());
					}
				}

				// USI reactors
				ModuleResourceConverter converterModule = part.FindModuleImplementing<ModuleResourceConverter>();
				if (converterModule != null) {
					if (converterModule.ModuleIsActive() && converterModule.ConverterName == "Reactor") {
						foreach (var resource in converterModule.outputList) {
							if (resource.ResourceName == "ElectricCharge") {
								otherPower += resource.Ratio * converterModule.Efficiency;
							}
						}
					}
				}
			}
			return otherPower;
		} // So many ifs.....

		/// <summary>
		/// Decodes the path.
		/// </summary>
//		private void DecodePath() {
//			dots.Clear ();
//			if (pathEncoded == null || pathEncoded.Length == 0)
//				return;
//
//			char[] separators = new char[] { ';' };
//			string[] wps = pathEncoded.Split (separators, StringSplitOptions.RemoveEmptyEntries);
//			foreach (var wp in wps) {
//				string[] latlon = wp.Split (':');
//				Vector3d localSpacePoint =
//					this.vessel.mainBody.GetWorldSurfacePosition (
//						double.Parse(latlon[0]),
//						double.Parse(latlon[1]),
//						0
//					);
//				dots.Add (localSpacePoint);
//			}
//		}

		// Cinically stolen from Waypoint Manager source code :D
		private void PlaceTargetAtCursor()
		{
			CelestialBody targetBody = this.vessel.mainBody;

			if (targetBody.pqsController == null)
			{
				return;
			}

			Ray mouseRay = PlanetariumCamera.Camera.ScreenPointToRay(Input.mousePosition);
			mouseRay.origin = ScaledSpace.ScaledToLocalSpace(mouseRay.origin);
			var bodyToOrigin = mouseRay.origin - targetBody.position;
			double curRadius = targetBody.pqsController.radiusMax;
			double lastRadius = 0;
			int loops = 0;
			while (loops < 50)
			{
				Vector3d relSurfacePosition;
				if (PQS.LineSphereIntersection(bodyToOrigin, mouseRay.direction, curRadius, out relSurfacePosition))
				{
					var surfacePoint = targetBody.position + relSurfacePosition;
					double alt = targetBody.pqsController.GetSurfaceHeight(
						QuaternionD.AngleAxis(targetBody.GetLongitude(surfacePoint), Vector3d.down) * QuaternionD.AngleAxis(targetBody.GetLatitude(surfacePoint), Vector3d.forward) * Vector3d.right);
					double error = Math.Abs(curRadius - alt);
					if (error < (targetBody.pqsController.radiusMax - targetBody.pqsController.radiusMin) / 100)
					{
						targetLatitude = (targetBody.GetLatitude(surfacePoint) + 360) % 360;
						targetLongitude = (targetBody.GetLongitude(surfacePoint) + 360) % 360;
						return;
					}
					else
					{
						lastRadius = curRadius;
						curRadius = alt;
						loops++;
					}
				}
				else
				{
					if (loops == 0)
					{
						break;
					}
					// Went too low, needs to try higher
					else
					{
						curRadius = (lastRadius * 9 + curRadius) / 10;
						loops++;
					}
				}
			}
		}
	}
}
