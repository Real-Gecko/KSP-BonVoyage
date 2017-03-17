using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace BonVoyage
{
	public class BonVoyageModule : PartModule
	{
		public struct WheelTestResult
		{
			public double powerRequired;
			public double maxSpeedSum;
			public int inTheAir;
			public int operable;
			public int damaged;
			public int online;

			public WheelTestResult(double powerRequired, double maxSpeedSum, int inTheAir, int operable, int damaged, int online)
			{
				this.powerRequired = powerRequired;
				this.maxSpeedSum = maxSpeedSum;
				this.inTheAir = inTheAir;
				this.operable = operable;
				this.damaged = damaged;
				this.online = online;
			}
		}

		private bool mapLocationMode;
//		private bool showUtils = false;

//		private List<Vector3d> wayPoints;

//		private Rect guiRect;
//		private GUIStyle labelStyle;

		[KSPField(isPersistant = true)] //, guiName = "Active", guiActive = true)]
		public bool isActive = false;

		[KSPField(isPersistant = true)] //, guiName = "Target latitude", guiActive = true, guiFormat = "F2")]
		public double targetLatitude = 0;

		[KSPField(isPersistant = true)] //, guiName = "Target longitude", guiActive = true, guiFormat = "F2")]
		public double targetLongitude = 0;

		[KSPField(isPersistant = true)] //, guiName = "Distance to target", guiActive = true, guiFormat = "N0", guiUnits = " meters")]
		public double distanceToTarget = 0;

		[KSPField(isPersistant = true)] //, guiName = "Distance travelled", guiActive = true, guiFormat = "N0", guiUnits = " meters")]
		public double distanceTravelled = 0;

		[KSPField(isPersistant = true)] //, guiName = "Average speed", guiActive = true)]
		public double averageSpeed = 0;

		[KSPField(isPersistant = true)] //, guiName = "Last Updated", guiActive = false)]
		public double lastTime = 0;

		[KSPField(isPersistant = true)] //, guiName = "Solar powered", guiActive = false)]
		public bool solarPowered = true;

		[KSPField(isPersistant = true)] //, guiName = "Is manned", guiActive = false)]
		public bool isManned = true;

		[KSPField(isPersistant = true)] //, guiName = "pathEncoded", guiActive = false)]
		public string pathEncoded = "";

		public double solarPower;
		public double otherPower;
		public double powerRequired;

		public WheelTestResult testResult = new WheelTestResult();

		public void SystemCheck() {
			// Test stock wheels
			WheelTestResult wheelsTest = CheckWheels ();

			// Test KSPWheels
			WheelTestResult KSPWheelsTest = CheckKSPWheels ();

			// Combine the two
			testResult.powerRequired = wheelsTest.powerRequired + KSPWheelsTest.powerRequired;
			testResult.maxSpeedSum = wheelsTest.maxSpeedSum + KSPWheelsTest.maxSpeedSum;
			testResult.inTheAir = wheelsTest.inTheAir + KSPWheelsTest.inTheAir;
			testResult.operable = wheelsTest.operable + KSPWheelsTest.operable;
			testResult.damaged = wheelsTest.damaged + KSPWheelsTest.damaged;
			testResult.online = wheelsTest.online + KSPWheelsTest.online;

			// Average speed will vary depending on number of wheels online from 50 to 70 percent
			// of average wheels' max speed
			if (testResult.online != 0)
				averageSpeed = testResult.maxSpeedSum / testResult.online / 100 * Math.Min (70, (40 + 5 * testResult.online));
			else
				averageSpeed = 0;

			// Unmanned rovers drive with 80% speed penalty
			this.isManned = (this.vessel.GetCrewCount () > 0);
			if (!this.isManned) //{
				averageSpeed = averageSpeed * 0.2;
//			}

			// Generally moving at high speed requires less power than wheels' max consumption
			// To start BV online wheels consumption must be less than or equal to 35% of max power production
			powerRequired = wheelsTest.powerRequired / 100 * 35;

			// Check for power production
			solarPower = CalculateSolarPower ();
			otherPower = CalculateOtherPower ();

			// If alternative power sources produce more then required
			// Rover will ride forever :D
			if (otherPower >= powerRequired)
				solarPowered = false;
			else
				solarPowered = true;
		}

//		[KSPEvent(guiActive = true, guiName = "Pick target on map")]
		public void PickTarget()
		{
			if (this.vessel.situation != Vessel.Situations.LANDED)
				return;
			Deactivate();
			MapView.EnterMapView();
			mapLocationMode = true;
		}

//		[KSPEvent(guiActive = true, guiName = "KSPWheel Check")]
//		public void KSPWheelCheck()
//		{
////			DealWithKSPWheel ();
//		}

		[KSPEvent(guiActive = true, guiName = "BV Control Panel")]
		public void ControlPanel() {
//			BonVoyage.Instance.ControlThis (this);
			BonVoyage.Instance.ShowModuleControl();
		}

//		[KSPEvent(guiActive = true, guiName = "Set to active target")]
		public void SetToActive()
		{
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
				double[] newCoordinates =
					GeoUtils.StepBack(
						this.vessel.latitude,
						this.vessel.longitude,
						targetVessel.latitude,
						targetVessel.longitude,
						this.vessel.mainBody.Radius,
						200
					);
				this.targetLatitude = newCoordinates[0];
				this.targetLongitude = newCoordinates[1];
				this.distanceTravelled = 0;
				FindPath();
			}
			else {
				ScreenMessages.PostScreenMessage("Your target is out there somewhere, this won't work!");
			}
		}

//		[KSPEvent(guiActive = true, guiName = "Set to active waypoint", isPersistent = true)]
		public void SetToWaypoint() {
			NavWaypoint navPoint = NavWaypoint.fetch;
			if (navPoint == null || !navPoint.IsActive || navPoint.Body != this.vessel.mainBody) {
				ScreenMessages.PostScreenMessage ("No valid nav point");
			} else {
				Deactivate();
				double[] newCoordinates =
					GeoUtils.StepBack(
						this.vessel.latitude,
						this.vessel.longitude,
						navPoint.Latitude,
						navPoint.Longitude,
						this.vessel.mainBody.Radius,
						200
					);
				this.targetLatitude = newCoordinates[0];
				this.targetLongitude = newCoordinates[1];
				this.distanceTravelled = 0;
				FindPath();
			}
		}

//		[KSPEvent(guiActive = true, guiName = "Poehali!!!", isPersistent = true)]
		public void Activate()
		{
			if (this.vessel.situation != Vessel.Situations.LANDED)
			{
				ScreenMessages.PostScreenMessage("Something is wrong", 5);
				ScreenMessages.PostScreenMessage("Hmmmm, what can it be?", 6);
				ScreenMessages.PostScreenMessage("Ah, yes! You're not landed!", 7);
				return;
			}

			if (distanceToTarget == 0)
			{
				ScreenMessages.PostScreenMessage("No path to target calculated");
				return;
			}

//			averageSpeed = 0;
//			double powerRequired = 0;

			SystemCheck ();

//			WheelTestResult wheelsTest = CheckWheels ();
//			WheelTestResult KSPWheelsTest = CheckKSPWheels ();
//
//			// Combine the two
//			testResult.powerRequired = wheelsTest.powerRequired + KSPWheelsTest.powerRequired;
//			testResult.maxSpeedSum = wheelsTest.maxSpeedSum + KSPWheelsTest.maxSpeedSum;
//			testResult.inTheAir = wheelsTest.inTheAir + KSPWheelsTest.inTheAir;
//			testResult.operable = wheelsTest.operable + KSPWheelsTest.operable;
//			testResult.damaged = wheelsTest.damaged + KSPWheelsTest.damaged;
//			testResult.online = wheelsTest.online + KSPWheelsTest.online;

			// No driving until 4 operable wheels are touching the ground
			if (testResult.inTheAir > 0 && testResult.operable < 4)
			{
				ScreenMessages.PostScreenMessage("Wheels are not touching the ground, are you serious???");
				return;
			}

			//Buy some wheels, maaaan
			if (testResult.operable < 4)
			{
				ScreenMessages.PostScreenMessage("Don't be a miser, add some more wheels to you rover!");
				return;
			}

			// Looks like no wheels are on
			if (testResult.online < 2)
			{
				ScreenMessages.PostScreenMessage("At least two wheels must be online!");
				return;
			}

			// Average speed will vary depending on number of wheels online from 50 to 70 percent
			// of average wheels' max speed
//			averageSpeed = testResult.maxSpeedSum / testResult.online / 100 * Math.Min(70, (40 + 5 * testResult.online));

			// Unmanned rovers drive with 80% speed penalty
//			this.isManned = (this.vessel.GetCrewCount () > 0);
			if (!this.isManned) //{
//				averageSpeed = averageSpeed * 0.2;
				ScreenMessages.PostScreenMessage ("Rover is unmanned, 80% speed penalty!");
//			}
//
//			// Generally moving at high speed requires less power than wheels' max consumption
//			// BV will require max online wheels consumption to be less than 35% of max power production
//			powerRequired = wheelsTest.powerRequired / 100 * 35;

//			double solarPower = CalculateSolarPower();
//			double otherPower = CalculateOtherPower();

			if (solarPower + otherPower < powerRequired)
			{
				ScreenMessages.PostScreenMessage ("Your power production is low", 5);
				ScreenMessages.PostScreenMessage ("You need MOAR solar panels", 6);
				ScreenMessages.PostScreenMessage ("Or maybe a dozen of fission reactors", 7);
				return;
			}

//			// If alternative power sources produce more then required
//			// Rover will ride forever :D
//			if (otherPower >= powerRequired)
//				solarPowered = false;

			isActive = true;
			distanceTravelled = 0;
//			Events["Activate"].active = false;
//			Events["Deactivate"].active = true;
			BonVoyage.Instance.UpdateRoverState(this.vessel, true);
			ScreenMessages.PostScreenMessage("Bon Voyage!!!");
		}

//		[KSPEvent(guiActive = true, guiName = "Deactivate", active = false, isPersistent = true)]
		public void Deactivate()
		{
			isActive = false;
			targetLatitude = 0;
			targetLongitude = 0;
			distanceTravelled = 0;
			distanceToTarget = 0;
//			wayPoints.Clear ();
//			Events["Activate"].active = true;
//			Events["Deactivate"].active = false;
			BonVoyage.Instance.UpdateRoverState(this.vessel, false);
		}

//		[KSPEvent(guiActive = true, guiName = "Toggle utilities")]
//		public void ToggleUtils()
//		{
//			showUtils = !showUtils;
//			Events["CalculateSolar"].active = showUtils;
//			Events["CalculateOther"].active = showUtils;
////			Events["CalculateAverageSpeed"].active = showUtils;
//			Events["CalculatePowerRequirement"].active = showUtils;
//
//			//Clean up previous builds
//			Events["CalculateSolar"].guiActive = true;
//			Events["CalculateOther"].guiActive = true;
//			if (Events["FindPath"] != null)
//			{
//				Events["FindPath"].guiActive = false;
//				Events["FindPath"].active = false;
//			}
//			if (Events["PickTest"] != null)
//			{
//				Events["PickTest"].guiActive = false;
//				Events["PickTest"].active = false;
//			}
//		}

		private void FindPath()
		{
			distanceToTarget = 0;

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
				BonVoyage.Instance.UpdateWayPoints ();
//				wayPoints = PathUtils.DecodePath (pathEncoded, this.vessel.mainBody);
			}
			else
				ScreenMessages.PostScreenMessage("No path found, try some other location!");
		}

//		public void TestLZString() {
//			KSP.IO.File.WriteAllText<BonVoyage> (LZString.compressToBase64(pathEncoded), "lzstring");
//		}

//		[KSPEvent(guiActive = true, guiName = "Calculate solar", active = false)]
//		public void CalculateSolar()
//		{
//			double solarPower = CalculateSolarPower();
//			ScreenMessages.PostScreenMessage(String.Format("{0:F} electric charge/second", solarPower));
//		}

//		[KSPEvent(guiActive = true, guiName = "Calculate other", active = false)]
//		public void CalculateOther()
//		{
//			double otherPower = CalculateOtherPower();
//			ScreenMessages.PostScreenMessage(String.Format("{0:F} electric charge/second", otherPower));
//		}

//		[KSPEvent(guiActive = true, guiName = "Calculate average speed", active = false)]
//		public void CalculateAverageSpeed() {
//			List<ModuleWheels.ModuleWheelMotor> operableWheels = new List<ModuleWheels.ModuleWheelMotor>();
//			for(int i=0; i< this.vessel.parts.Count;++i)
//			{
//				ModuleWheels.ModuleWheelMotor wheelMotor = this.vessel.parts[i].FindModuleImplementing<ModuleWheels.ModuleWheelMotor>();
//				if (wheelMotor != null)
//				{
//					operableWheels.Add(wheelMotor);
//				}
//			}
//
//			// Average speed will vary depending on number of wheels online from 50 to 70 percent of wheel max speed
//			this.averageSpeed = GetAverageSpeed(operableWheels);
//		}

//		[KSPEvent(guiActive = true, guiName = "Calculate power requirement", active = false)]
		public void CalculatePowerRequirement()
		{
//			double powerRequired = 0;
//			for (int i = 0; i < this.vessel.parts.Count; ++i) {
//				ModuleWheels.ModuleWheelMotor wheelMotor = this.vessel.parts [i].FindModuleImplementing<ModuleWheels.ModuleWheelMotor> ();
//				if (wheelMotor != null) {
//					if (wheelMotor.motorEnabled)
//						//						powerRequired += wheelMotor.inputResource.rate;
//						powerRequired += wheelMotor.avgResRate;
//				}
//			}

			// Average speed will vary depending on number of wheels online from 50 to 70 percent of wheel max speed
//			powerRequired = powerRequired / 100 * 35;
//			ScreenMessages.PostScreenMessage("Current power requirements " + powerRequired.ToString("F2") + "/s");
			double powerRequired = 0;

			WheelTestResult wheelsTest = CheckWheels ();
			WheelTestResult KSPWheelsTest = CheckKSPWheels ();

			// Combine the two
			wheelsTest.powerRequired += KSPWheelsTest.powerRequired;
			wheelsTest.maxSpeedSum += KSPWheelsTest.maxSpeedSum;
			wheelsTest.inTheAir += KSPWheelsTest.inTheAir;
			wheelsTest.operable += KSPWheelsTest.operable;
			wheelsTest.damaged += KSPWheelsTest.damaged;
			wheelsTest.online += KSPWheelsTest.online;
			powerRequired = wheelsTest.powerRequired / 100 * 35;
			ScreenMessages.PostScreenMessage("Current power requirements " + powerRequired.ToString("F2") + "/s", 15);
		}

		public override string GetInfo()
		{
			return "Bon Voyage controller";
		}

		public override void OnStart(PartModule.StartState state)
		{
			if (HighLogic.LoadedSceneIsEditor)
				return;
//			wayPoints = PathUtils.DecodePath (pathEncoded, this.vessel.mainBody);
		}

		private void Update() {
			if (isActive)
				lastTime = Planetarium.GetUniversalTime();
		}

		private void OnGUI()
		{
			if (HighLogic.LoadedSceneIsEditor)
				return;


//			if (MapView.MapIsEnabled)
//			{
//				if (wayPoints.Count > 0)
//				{
//					GLUtils.DrawCurve (wayPoints);
//				}
//			}

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

//		private double GetAverageSpeed(List<ModuleWheels.ModuleWheelMotor> operableWheels)
//		{
//			double averageSpeed = 0;
//			int wheelsOnline = 0;
//			for(int i=0;i<operableWheels.Count;++i)
//			{
//			    var wheelMotor = operableWheels[i];
//				if (wheelMotor.motorEnabled)
//				{
//					wheelsOnline++;
//					double maxWheelSpeed = 0;
//					if (wheelMotor.part.name == "roverWheel1") //RoveMax Model M1 gives crazy values
//						maxWheelSpeed = 42;
//					else
//						maxWheelSpeed = wheelMotor.wheelSpeedMax;
//					averageSpeed = Math.Max(averageSpeed, maxWheelSpeed);
//				}
//			}
//			if (wheelsOnline < 2)
//				return 0;
//
//			averageSpeed = averageSpeed / 100 * Math.Min(70, (40 + 5 * wheelsOnline));
//			return averageSpeed;
//		}

		private double CalculateSolarPower()
		{
			double solarPower = 0;
			double distanceToSun = this.vessel.distanceToSun;
			double solarFlux = PhysicsGlobals.SolarLuminosity / (12.566370614359172 * distanceToSun * distanceToSun);
			float multiplier = 1;

			for(int i=0;i<this.vessel.parts.Count;++i)
			{
				ModuleDeployableSolarPanel solarPanel = this.vessel.parts[i].FindModuleImplementing<ModuleDeployableSolarPanel>();
				if (solarPanel == null)
					continue;
				if (solarPanel.deployState != ModuleDeployableSolarPanel.DeployState.BROKEN &&
					solarPanel.deployState != ModuleDeployableSolarPanel.DeployState.RETRACTED &&
					solarPanel.deployState != ModuleDeployableSolarPanel.DeployState.RETRACTING)
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
			for(int i=0;i<this.vessel.parts.Count;++i)
			{
			    var part = this.vessel.parts[i];
				// Find standard RTGs
				ModuleGenerator powerModule = part.FindModuleImplementing<ModuleGenerator>();
				if (powerModule != null) {
					if (powerModule.generatorIsActive || powerModule.isAlwaysActive) {
					    for(int j=0; j<powerModule.resHandler.outputResources.Count;++j)
						{
						    var resource = powerModule.resHandler.outputResources[j];
							if (resource.name == "ElectricCharge") {
								otherPower += resource.rate * powerModule.efficiency;
							}
						}
					}
				}

				// Search for other generators
				PartModuleList modules = part.Modules;

				for(int j=0;j<modules.Count;++j)
				{
				    var module = modules[j];

					// Near future fission reactors
					if (module.moduleName == "FissionGenerator") {
						otherPower += double.Parse (module.Fields.GetValue ("CurrentGeneration").ToString());
					}

					// KSP Interstellar generators
					if (module.moduleName == "FNGenerator") {
						string maxPowerStr = module.Fields.GetValue ("MaxPowerStr").ToString ();
						double maxPower = 0;
						ScreenMessages.PostScreenMessage ("MAXPOWER: " + maxPowerStr);
						if (maxPowerStr.Contains ("GW"))
							maxPower = double.Parse (maxPowerStr.Replace (" GW", "")) * 1000000;
						else if (maxPowerStr.Contains ("MW"))
							maxPower = double.Parse (maxPowerStr.Replace (" MW", "")) * 1000;
						else
							maxPower = double.Parse (maxPowerStr.Replace (" KW", ""));
							
						otherPower += maxPower;
					}
				}

				// USI reactors
				ModuleResourceConverter converterModule = part.FindModuleImplementing<ModuleResourceConverter>();
				if (converterModule != null) {
					if (converterModule.ModuleIsActive() && converterModule.ConverterName == "Reactor") {
						for(int j=0;j<converterModule.outputList.Count;++j)
						{
						    var resource = converterModule.outputList[j];
							if (resource.ResourceName == "ElectricCharge") {
								otherPower += resource.Ratio * converterModule.GetEfficiencyMultiplier();
							}
						}
					}
				}
			}
			return otherPower;
		} // So many ifs.....


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

		/// <summary>
		/// Checks standard wheels with module ModuleWheelBase
		/// </summary>
		private WheelTestResult CheckWheels() {
			double powerRequired = 0;
			double maxSpeedSum = 0;
			int inTheAir = 0;
			int operable = 0;
			int damaged = 0;
			int online = 0;

			List<Part> wheels = new List<Part>();
			for (int i = 0; i < this.vessel.parts.Count; i++) {
				var part = this.vessel.parts [i];
				if (part.Modules.Contains ("ModuleWheelBase")) {
					wheels.Add (part);
				}
			}

			foreach (Part part in wheels) {
				ModuleWheelBase wheelBase = part.FindModuleImplementing<ModuleWheelBase> ();
				if (wheelBase.wheelType == WheelType.LEG)
					continue;

				ModuleWheels.ModuleWheelDamage wheelDamage = part.FindModuleImplementing<ModuleWheels.ModuleWheelDamage> ();
				// Malemute and Karibou wheels do not implement moduleDamage, so they're unbreakable?
				if (wheelDamage != null) {
					// Wheel is damaged
					if (wheelDamage.isDamaged) {
						damaged++;
						continue;
					}
				}

				// Whether or not wheel is touching the ground
				if (!wheelBase.isGrounded) {
					inTheAir++;
					continue;
				} else
					operable++;

				ModuleWheels.ModuleWheelMotor wheelMotor = part.FindModuleImplementing<ModuleWheels.ModuleWheelMotor> ();
				if (wheelMotor != null) {
					// Wheel is on
					if (wheelMotor.motorEnabled) {
						powerRequired += wheelMotor.avgResRate;
						online++;
						double maxWheelSpeed = 0;
						if (wheelMotor.part.name == "roverWheel1") //RoveMax Model M1 gives crazy values
							maxWheelSpeed = 42;
						else
							maxWheelSpeed = wheelMotor.wheelSpeedMax;
						maxSpeedSum += maxWheelSpeed;
					}
				}
			}
			return new WheelTestResult (powerRequired, maxSpeedSum, inTheAir, operable, damaged, online);
		}

		/// <summary>
		/// Checks KSPWheels implementing module KSPWheelBase
		/// </summary>
		private WheelTestResult CheckKSPWheels() {
			double powerRequired = 0;
			double maxSpeedSum = 0;
			int inTheAir = 0;
			int operable = 0;
			int damaged = 0;
			int online = 0;

			// Let's find some KSPWheel parts
			List<Part> KSPWheels = new List<Part>();
			for (int i = 0; i < this.vessel.parts.Count; ++i) {
				var part = this.vessel.parts [i];
				if (part.Modules.Contains ("KSPWheelBase")) {
					KSPWheels.Add (part);
				}
			}

			foreach (var part in KSPWheels) {
				// PartModuleList is not generic List<T>??? Fuck this API!!!
				List<PartModule> partModules = part.Modules.GetModules<PartModule>();
//				ScreenMessages.PostScreenMessage (part.name);
				PartModule wheelBase = partModules.Find (t => t.moduleName == "KSPWheelBase");
				// Wheel is damaged
				if (wheelBase.Fields.GetValue ("persistentState").ToString() == "BROKEN") {
					damaged++;
					continue;
				}

				PartModule wheelDamage = partModules.Find (t => t.moduleName == "KSPWheelDamage");
				if (wheelDamage != null) {
					// Wheel is damaged
//					if (double.Parse (wheelDamage.Fields.GetValue ("wheelWear").ToString ()) == 1 &&
//					    double.Parse (wheelDamage.Fields.GetValue ("motorWear").ToString ()) == 1 &&
//					    double.Parse (wheelDamage.Fields.GetValue ("suspensionWear").ToString ()) == 1) {
//						damaged++;
//						continue;
//					}
					// Wheel is not touching the ground
					if (double.Parse (wheelDamage.Fields.GetValue ("loadStress").ToString ()) == 0) {
						inTheAir++;
						continue;
					} else
						operable++;
				}

				PartModule wheelMotor = partModules.Find (t => t.moduleName == "KSPWheelMotor");
				if (wheelMotor != null) {
					// Wheel is on
					if (!bool.Parse (wheelMotor.Fields.GetValue ("motorLocked").ToString ())) {
						online++;
						maxSpeedSum += double.Parse (wheelDamage.Fields.GetValue ("maxSafeSpeed").ToString ());
					}
				}
				PartModule wheelTracks = partModules.Find (t => t.moduleName == "KSPWheelTracks");
				if (wheelTracks != null) {
					// Let's count one track as 2 wheels
					// Anyway who cares :D
					operable++;
					if (!bool.Parse (wheelTracks.Fields.GetValue ("motorLocked").ToString ())) {
						online += 2;
						maxSpeedSum += 2 * double.Parse (wheelDamage.Fields.GetValue ("maxSafeSpeed").ToString ());
					}
				}
				double scale = double.Parse (wheelBase.Fields.GetValue ("scale").ToString ());
				powerRequired += KSPWheelPower (part.name, scale);
			}
			return new WheelTestResult (powerRequired, maxSpeedSum, inTheAir, operable, damaged, online);
		}

		// Most elegant solution ever :D
		private double KSPWheelPower(string name, double scale) {
		//	KF_SurfaceTrack = 1.28
		//	KF_WheelTiny = 0.5
		//	KF_WheelLarge = 24.7
		//	KF_TrackLong = 6
		//	KF_TrackMedium = 3.47
		//	KF_WheelMedium = 4
		//	KF_TrackRBIInverting = 10
		//	KF_TrackRBIMole = 57
		//	KF_TrackRBITiny = 2.32
		//	KF_ScrewDrive2 = 7.37
		//	KF_TrackS = 1.3
		//	KF_WheelSmall = 4
		//	KF_TrackSmall = 2.5
		//	KF-WheelTruck-Dual = 5
		//	KF-WheelTruck-Single = 4
//			name = name.Replace (".", "_");
			switch (name) {
			case "KF.SurfaceTrack":
				return 1.28 * scale;
			case "KF.WheelTiny":
				return 0.5 * scale;
			case "KF.WheelLarge":
				return 24.7 * scale;
			case "KF.TrackLong":
				return 6 * scale;
			case "KF.TrackMedium":
				return 3.47 * scale;
			case "KF.WheelMedium":
				return 4 * scale;
			case "KF.TrackRBIInverting":
				return 10 * scale;
			case "KF.TrackRBIMole":
				return 57 * scale;
			case "KF.TrackRBITiny":
				return 2.32 * scale;
			case "KF.ScrewDrive2":
				return 7.37 * scale;
			case "KF.TrackS":
				return 1.3 * scale;
			case "KF.WheelSmall":
				return 4 * scale;
			case "KF.TrackSmall":
				return 2.5 * scale;
			case "KF-WheelTruck-Dual":
				return 5 * scale;
			case "KF-WheelTruck-Single":
				return 4 * scale;
			default:
				return 0;
			}
		}
	}
}
