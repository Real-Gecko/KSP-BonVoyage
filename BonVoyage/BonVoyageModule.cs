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

		[KSPField(isPersistant = true)] //, guiName = "Active", guiActive = true)]
		public bool isActive = false;

        [KSPField(isPersistant = true)]
        public bool isShutdown = false;

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

        // Average speed at night
        [KSPField(isPersistant = true)]
        public double averageSpeedAtNight = 0;

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

        public double maxSpeedBase;
        public int wheelsPercentualModifier;
        public int crewSpeedBonus;

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

			// Generally moving at high speed requires less power than wheels' max consumption
			// To start BV online wheels consumption must be less than or equal to 35% of max power production
			powerRequired = wheelsTest.powerRequired / 100 * 35;

			// Check for power production
			solarPower = CalculateSolarPower();
			otherPower = CalculateOtherPower();

            // Solar powered?
            if (solarPower > 0.0)
                solarPowered = true;
            else
                solarPowered = false;

            this.isManned = (this.vessel.GetCrewCount() > 0);

            // Pilots and Scouts (USI) increase base average speed
            crewSpeedBonus = 0;
            if (this.isManned)
            {
                int maxPilotLevel = -1;
                int maxScoutLevel = -1;
                foreach (ProtoCrewMember crewMember in this.vessel.GetVesselCrew())
                {
                    switch (crewMember.trait)
                    {
                        case "Pilot":
                            if (maxPilotLevel < crewMember.experienceLevel)
                                maxPilotLevel = crewMember.experienceLevel;
                            break;
                        case "Scout":
                            if (maxScoutLevel < crewMember.experienceLevel)
                                maxScoutLevel = crewMember.experienceLevel;
                            break;
                    }
                }
                if (maxPilotLevel > 0)
                    crewSpeedBonus = 5 * maxPilotLevel; // up to 25% for pilot
                else if (maxScoutLevel > 0)
                    crewSpeedBonus = 2 * maxScoutLevel; // up to 10% for scout
            }

            // Average speed will vary depending on number of wheels online and crew present from 50 to 95 percent
            // of average wheels' max speed
            if (testResult.online != 0)
            {
                maxSpeedBase = testResult.maxSpeedSum / testResult.online;
                wheelsPercentualModifier = Math.Min(70, (40 + 5 * testResult.online));
                averageSpeed = maxSpeedBase / 100 * (wheelsPercentualModifier + crewSpeedBonus);
                //averageSpeed = testResult.maxSpeedSum / testResult.online / 100 * (Math.Min(70, (40 + 5 * testResult.online)) + crewSpeedBonus);
            }
            else
                averageSpeed = 0;

            // Unmanned rovers drive with 80% speed penalty
            if (!this.isManned)
                averageSpeed = averageSpeed * 0.2;

            // Base average speed at night is the same as average speed, if there is other power source. Zero otherwise.
            if (otherPower > 0.0)
                averageSpeedAtNight = averageSpeed;
            else
                averageSpeedAtNight = 0;

            // If required power is greater then total power generated, then average speed can be lowered up to 50%
            if (powerRequired > (solarPower + otherPower))
            {
                double speedReduction = (powerRequired - (solarPower + otherPower)) / powerRequired;
                if (speedReduction <= 0.5)
                    averageSpeed = averageSpeed * (1 - speedReduction);
            }

            // If required power is greater then other power generated, then average speed at night can be lowered up to 50%
            if (powerRequired > otherPower)
            {
                double speedReduction = (powerRequired - otherPower) / powerRequired;
                if (speedReduction <= 0.5)
                    averageSpeedAtNight = averageSpeedAtNight * (1 - speedReduction);
                else
                    averageSpeedAtNight = 0;
            }
        }

        //[KSPEvent(guiActive = true, guiName = "Pick target on map")]
        public void PickTarget()
		{
			if (this.vessel.situation != Vessel.Situations.LANDED)
				return;
			Deactivate();
            BonVoyage.Instance.HideModuleControl();
            MapView.EnterMapView();
			mapLocationMode = true;
        }

        // Shutdown/Activate BV controller
		[KSPEvent(guiActive = true, guiName = "Shutdown BV Controller")]
        public void ToggleBVController()
        {
            isShutdown = !isShutdown;
            Events["ToggleBVController"].guiName = (!isShutdown? "Shutdown" : "Activate") + " BV Controller";
            Events["BVControlPanel"].guiActive = !isShutdown;
            if (isShutdown)
            {
                if (isActive)
                    Deactivate();
                BonVoyage.Instance.HideModuleControl();
            }
            BonVoyage.Instance.LoadRovers();
        }

        [KSPEvent(guiActive = true, guiName = "BV Control Panel")]
		public void BVControlPanel() {
			BonVoyage.Instance.ShowModuleControl();
		}

        //[KSPEvent(guiActive = true, guiName = "Set to active target")]
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

        //[KSPEvent(guiActive = true, guiName = "Set to active waypoint", isPersistent = true)]
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

        //[KSPEvent(guiActive = true, guiName = "Poehali!!!", isPersistent = true)]
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

			SystemCheck ();

            // No driving until 3 operable wheels are touching the ground - tricycles are allowed
            //if (testResult.inTheAir > 0 && testResult.operable < 4)
            if (testResult.inTheAir > 0 && testResult.operable < 3)
            {
				ScreenMessages.PostScreenMessage("Wheels are not touching the ground, are you serious???");
				return;
			}

            //Buy some wheels, maaaan
            //if (testResult.operable < 4)
            if (testResult.operable < 3)
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

			// Unmanned rovers drive with 80% speed penalty
			if (!this.isManned)
				ScreenMessages.PostScreenMessage ("Rover is unmanned, 80% speed penalty!");

			if (solarPower + otherPower < powerRequired)
			{
                // If required power is greater ther total power generated, then average speed can be lowered up to 50%
                double speedReduction = (powerRequired - (solarPower + otherPower)) / powerRequired;

                // Quick and dirty hack, when Kerbalism is present -> disable power check
                // Kerbalism changes power production and rovers has zero chargeRate
                if ((speedReduction > 0.5) && !AssemblyUtils.AssemblyIsLoaded("Kerbalism"))
                {
                    ScreenMessages.PostScreenMessage("Your power production is low", 5);
                    ScreenMessages.PostScreenMessage("You need MOAR solar panels", 6);
                    ScreenMessages.PostScreenMessage("Or maybe a dozen of fission reactors", 7);
                    return;
                }
			}

			isActive = true;
			distanceTravelled = 0;
			BonVoyage.Instance.UpdateRoverState(this.vessel, true);
			ScreenMessages.PostScreenMessage("Bon Voyage!!!");
		}

        //[KSPEvent(guiActive = true, guiName = "Deactivate", active = false, isPersistent = true)]
		public void Deactivate()
		{
			isActive = false;
			targetLatitude = 0;
			targetLongitude = 0;
			distanceTravelled = 0;
			distanceToTarget = 0;
            pathEncoded = "";
			BonVoyage.Instance.UpdateRoverState(this.vessel, false);
		}

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
			}
			else
				ScreenMessages.PostScreenMessage("No path found, try some other location!");
		}

        //[KSPEvent(guiActive = true, guiName = "Calculate power requirement", active = false)]
		public void CalculatePowerRequirement()
		{
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
            if (HighLogic.LoadedSceneIsFlight)
            {
                Events["ToggleBVController"].guiName = (!isShutdown ? "Shutdown" : "Activate") + " BV Controller";
                Events["BVControlPanel"].guiActive = !isShutdown;
            }
        }

		private void Update() {
			if (isActive)
				lastTime = Planetarium.GetUniversalTime();
		}

		private void OnGUI()
		{
			if (HighLogic.LoadedSceneIsEditor)
				return;

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
                        BonVoyage.Instance.ShowModuleControl();
                        mapLocationMode = false;
						MapView.ExitMapView();
					}
				}
			}
		}

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

                // WBI reactors, USI reactors and MKS Power Pack
                ModuleResourceConverter converterModule = part.FindModuleImplementing<ModuleResourceConverter>();
				if (converterModule != null) {
                    if (converterModule.ModuleIsActive()
                        && ((converterModule.ConverterName == "Nuclear Reactor") || (converterModule.ConverterName == "Reactor") || (converterModule.ConverterName == "Generator")))
                    {
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
				PartModule wheelBase = partModules.Find (t => t.moduleName == "KSPWheelBase");
				// Wheel is damaged
				if (wheelBase.Fields.GetValue ("persistentState").ToString() == "BROKEN") {
					damaged++;
					continue;
				}

				PartModule wheelDamage = partModules.Find (t => t.moduleName == "KSPWheelDamage");
				if (wheelDamage != null) {
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
