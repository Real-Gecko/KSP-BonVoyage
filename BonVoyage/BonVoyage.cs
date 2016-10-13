using System;
using System.Collections.Generic;
using UnityEngine;
using KSP.UI.Screens;
using KSP.IO;

namespace BonVoyage
{
	/*
	 * Addon start at KSC screen
	 * Loads rovers with BonVoyage module onboard
	 * Keeps all rovers in memory until scene switch
	 * Every scene switch rebuilds rover list
	*/
	[KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
	public class BonVoyage : MonoBehaviour
	{
		public static BonVoyage Instance;
//		public Texture2D mapMarker;

		private List<ActiveRover> activeRovers;
		private ApplicationLauncherButton appLauncherButton;
		private DateTime lastUpdated;
		private PluginConfiguration config;
		private bool autoDewarp;

		// Input locking stuff
		private ControlTypes lockMask = (
			ControlTypes.YAW |
			ControlTypes.PITCH |
			ControlTypes.ROLL |
			ControlTypes.THROTTLE |
			ControlTypes.STAGING |
			ControlTypes.CUSTOM_ACTION_GROUPS |
			ControlTypes.GROUPS_ALL |
			ControlTypes.RCS |
			ControlTypes.SAS |
			ControlTypes.WHEEL_STEER |
			ControlTypes.WHEEL_THROTTLE
		);
		private Rect labelRect;
		private GUIStyle labelStyle;

		//GUI variables
		private bool guiVisible;
		private bool globalHidden;
		private Rect guiRect;
		private int guiId;
		Vector2 mainWindowScrollPosition;

		public void Awake()
		{
			if (Instance != null)
			{
				Destroy(this);
				return;
			}
			Instance = this;

			guiVisible = false;
			globalHidden = false;
			guiId = GUIUtility.GetControlID(FocusType.Passive);
			config = PluginConfiguration.CreateForType<BonVoyage>();
			config.load();
			autoDewarp = config.GetValue<bool>("autoDewarp", false);
			activeRovers = new List<ActiveRover>();

			Rect sample = new Rect();
			sample.width = 700;
			sample.height = 500;
			sample.center = new Vector2(Screen.width / 2, Screen.height / 2);
			guiRect = config.GetValue<Rect>("guiRect", new Rect(sample));
			config.save();
			lastUpdated = DateTime.Now;
			mainWindowScrollPosition = new Vector2(0, 0);

			labelRect = new Rect(0, 0, Screen.width, Screen.height / 2);
			labelStyle = new GUIStyle();
			labelStyle.stretchWidth = true;
			labelStyle.stretchHeight = true;
			labelStyle.alignment = TextAnchor.MiddleCenter;
			labelStyle.fontSize = Screen.height / 20;
			labelStyle.fontStyle = FontStyle.Bold;
			labelStyle.normal.textColor = Color.red;

//			mapMarker = GameDatabase.Instance.GetTexture("BonVoyage/Textures/map-marker", false);
		}

		public void Start()
		{
			DontDestroyOnLoad(this);
			GameEvents.onGUIApplicationLauncherReady.Add(onGUIApplicationLauncherReady);
			GameEvents.onGameSceneSwitchRequested.Add(onGameSceneSwitchRequested);
			GameEvents.onLevelWasLoaded.Add(onLevelWasLoaded);
			GameEvents.onVesselChange.Add(onVesselChange);
			GameEvents.onHideUI.Add(onHideUI);
			GameEvents.onShowUI.Add(onShowUI);
			LoadRovers();
		}

		public void OnDestroy()
		{
			GameEvents.onGUIApplicationLauncherReady.Remove(onGUIApplicationLauncherReady);
			GameEvents.onGameSceneSwitchRequested.Remove(onGameSceneSwitchRequested);
			GameEvents.onLevelWasLoaded.Remove(onLevelWasLoaded);
			GameEvents.onVesselChange.Remove(onVesselChange);
			GameEvents.onHideUI.Remove(onHideUI);
			GameEvents.onShowUI.Remove(onShowUI);
			if (appLauncherButton != null)
			{
				ApplicationLauncher.Instance.RemoveModApplication(appLauncherButton);
			}
			config.SetValue("autoDewarp", autoDewarp);
			config.SetValue("guiRect", guiRect);
			config.save();
			InputLockManager.RemoveControlLock("BonVoyageInputLock");
		}

		public void onGameSceneSwitchRequested(GameEvents.FromToAction<GameScenes, GameScenes> ev)
		{
			if (appLauncherButton != null)
				appLauncherButton.SetFalse();
		}

		public void onVesselChange(Vessel vessel)
		{
			foreach (var rover in activeRovers)
			{
				if (rover.vessel == vessel && rover.bvActive)
				{
					InputLockManager.SetControlLock(lockMask, "BonVoyageInputLock");
					return;
				}
			}
			InputLockManager.RemoveControlLock("BonVoyageInputLock");
		}

		private void onHideUI()
		{
			globalHidden = true;
		}

		private void onShowUI()
		{
			globalHidden = false;
		}

		public void onLevelWasLoaded(GameScenes scene)
		{
			LoadRovers();
			onVesselChange(FlightGlobals.ActiveVessel);
		}

		public void Update()
		{
			if (lastUpdated.AddSeconds(1) > DateTime.Now)
				return;

			lastUpdated = DateTime.Now;

			double currentTime = Planetarium.GetUniversalTime();

			foreach (var rover in activeRovers)
			{
				if (rover.vessel.isActiveVessel)
				{
					rover.status = "current";
					continue;
				}

				if (!rover.bvActive || rover.vessel.loaded)
				{
					rover.status = "idle";
					continue;
				}

				Vector3d vesselPos = rover.vessel.mainBody.position - rover.vessel.GetWorldPos3D();
				Vector3d toKerbol = rover.vessel.mainBody.position - FlightGlobals.Bodies[0].position;
				double angle = Vector3d.Angle(vesselPos, toKerbol);

				// No moving at night, or when there's not enougth solar light 
				if (angle >= 85 && rover.solarPowered)
				{
					rover.status = "awaiting sunlight";
					rover.lastTime = currentTime;
					rover.BVModule.SetValue("lastTime", currentTime.ToString());
					rover.vessel.protoVessel = new ProtoVessel(rover.vesselConfigNode, HighLogic.CurrentGame);
					continue;
				}

				double deltaT = currentTime - rover.lastTime;

				double deltaS = rover.averageSpeed * deltaT;
				double bearing = GeoUtils.InitialBearing(
					rover.vessel.latitude,
					rover.vessel.longitude,
					rover.targetLatitude,
					rover.targetLongitude
				);
				rover.distanceTravelled += deltaS;
				if (rover.distanceTravelled >= rover.distanceToTarget)
				{
					rover.distanceTravelled = rover.distanceToTarget;
					rover.vessel.latitude = rover.targetLatitude;
					rover.vessel.longitude = rover.targetLongitude;

					rover.bvActive = false;
					rover.BVModule.SetValue("isActive", "False");
					rover.BVModule.SetValue("distanceTravelled", rover.distanceToTarget.ToString());

					rover.BVModule.GetNode("EVENTS").GetNode("Activate").SetValue("active", "True");
					rover.BVModule.GetNode("EVENTS").GetNode("Deactivate").SetValue("active", "False");

					if (autoDewarp)
					{
						TimeWarp.SetRate(0, false);
						ScreenMessages.PostScreenMessage(rover.vessel.vesselName + " has arrived to destination at " + rover.vessel.mainBody.name);
					}

					MessageSystem.Message message = new MessageSystem.Message(
						"Rover arrived",
						//------------------------------------------
						rover.vessel.vesselName + " has arrived to destination\nLAT:" +
						rover.targetLatitude.ToString("F2") + "\nLON:" + rover.targetLongitude.ToString("F2") +
						"\nAt " + rover.vessel.mainBody.name + ". \n" +
						"Distance travelled: " + rover.distanceTravelled.ToString("N") + " meters",
						//------------------------------------------
						MessageSystemButton.MessageButtonColor.GREEN,
						MessageSystemButton.ButtonIcons.COMPLETE
					);
					MessageSystem.Instance.AddMessage(message);
					rover.status = "idle";
				}
				else {
					int step = Convert.ToInt32(Math.Floor(rover.distanceTravelled / 1000));
					double remainder = rover.distanceTravelled % 1000;

					if (step < rover.path.Count - 1)
						bearing = GeoUtils.InitialBearing(
							rover.path[step].latitude,
							rover.path[step].longitude,
							rover.path[step + 1].latitude,
							rover.path[step + 1].longitude
						);
					else
						bearing = GeoUtils.InitialBearing(
							rover.path[step].latitude,
							rover.path[step].longitude,
							rover.targetLatitude,
							rover.targetLongitude
						);

					double[] newCoordinates = GeoUtils.GetLatitudeLongitude(
						rover.path[step].latitude,
						rover.path[step].longitude,
						bearing,
						remainder,
						rover.vessel.mainBody.Radius
					);

					rover.vessel.latitude = newCoordinates[0];
					rover.vessel.longitude = newCoordinates[1];
					rover.status = "roving";
				}
				rover.vessel.altitude = GeoUtils.TerrainHeightAt(rover.vessel.latitude, rover.vessel.longitude, rover.vessel.mainBody);
//				rover.toTravel = rover.distanceToTarget - rover.distanceTravelled;
				rover.lastTime = currentTime;

				// Save data to protovessel
				rover.vesselConfigNode.SetValue("lat", rover.vessel.latitude.ToString());
				rover.vesselConfigNode.SetValue("lon", rover.vessel.longitude.ToString());
				rover.vesselConfigNode.SetValue("alt", rover.vessel.altitude.ToString());
				rover.vesselConfigNode.SetValue("landedAt", rover.vessel.mainBody.theName);
				rover.BVModule.SetValue("distanceTravelled", (rover.distanceTravelled).ToString());
				rover.BVModule.SetValue("lastTime", currentTime.ToString());
				rover.vessel.protoVessel = new ProtoVessel(rover.vesselConfigNode, HighLogic.CurrentGame);
			}
		}

		public void onGUIApplicationLauncherReady()
		{
			if (appLauncherButton == null)
			{
				appLauncherButton = ApplicationLauncher.Instance.AddModApplication(
					onAppTrue,
					onAppFalse,
					null,
					null,
					null,
					null,
					ApplicationLauncher.AppScenes.SPACECENTER |
					ApplicationLauncher.AppScenes.TRACKSTATION |
					ApplicationLauncher.AppScenes.FLIGHT |
					ApplicationLauncher.AppScenes.MAPVIEW,
					GameDatabase.Instance.GetTexture("BonVoyage/Textures/bon-voyage-icon", false)
				);
			}
		}

		public void OnGUI()
		{
			if (InputLockManager.GetControlLock("BonVoyageInputLock") != 0)
			{
				GUILayout.BeginArea(labelRect);
				GUILayout.Label("Bon Voyage control lock active", labelStyle);
				GUILayout.EndArea();
			}

			if (!guiVisible || globalHidden) return;
			GUI.skin = UnityEngine.GUI.skin;
			guiRect = GUILayout.Window(
				guiId,
				guiRect,
				DrawGUI,
				"Bon Voyage powered rovers",
				GUILayout.ExpandWidth(true),
				GUILayout.ExpandHeight(true)
			);
		}

		public void onAppTrue()
		{
			guiVisible = true;
		}

		public void onAppFalse()
		{
			guiVisible = false;
		}

		public void DrawGUI(int guiId)
		{
			double currentTime = Planetarium.GetUniversalTime();
			GUILayout.BeginVertical();
			mainWindowScrollPosition = GUILayout.BeginScrollView(mainWindowScrollPosition);
			foreach (var rover in activeRovers)
			{
				switch (rover.status)
				{
					case "current":
						GUI.contentColor = Color.white;
						break;
					case "roving":
						GUI.contentColor = Color.green;
						break;
					case "idle":
						GUI.contentColor = Color.yellow;
						break;
					case "awaiting sunlight":
						GUI.contentColor = Color.red;
						break;
				}
				GUILayout.BeginHorizontal();
				if (GUILayout.Button(
						rover.vessel.vesselName,
						GUILayout.Height(25),
						GUILayout.Width(150)
				   ))
				{
					if (HighLogic.LoadedScene == GameScenes.TRACKSTATION)
					{
						PlanetariumCamera.fetch.SetTarget(rover.vessel.mapObject);
					}
					if (HighLogic.LoadedSceneIsFlight)
					{
						MapView.EnterMapView();
						PlanetariumCamera.fetch.SetTarget(rover.vessel.mapObject);
					}
				}
				if (GUILayout.Button("Switch to", GUILayout.Height(25), GUILayout.Width(100)))
				{
					GamePersistence.SaveGame("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
					FlightDriver.StartAndFocusVessel("persistent", FlightGlobals.Vessels.IndexOf(rover.vessel));
				}

				GUILayout.Label(
					rover.vessel.mainBody.bodyName,
					GUILayout.Width(45)
				);
				GUILayout.Label(
					rover.status,
					GUILayout.Width(105)
				);
				if (rover.status == "roving" || rover.status == "awaiting sunlight")
				{
					GUILayout.Label(
						"v̅ = " + rover.averageSpeed.ToString("N") + ", yet to travel " +
						rover.yetToTravel.ToString("N0") + " meters"
					);
				}

				if (rover.status == "idle")
				{
					TimeSpan t = TimeSpan.FromSeconds(currentTime - rover.lastTime);

					string idlePeriod = string.Format(
						"{0:D2}h:{1:D2}m:{2:D2}s",
						t.Hours,
						t.Minutes,
						t.Seconds
					);

					GUILayout.Label(idlePeriod);
				}
				GUILayout.EndHorizontal();
			}
			GUILayout.EndScrollView();
			GUI.contentColor = Color.white;
			autoDewarp = GUILayout.Toggle(autoDewarp, "Automagic dewarp");

			GUILayout.EndVertical();
			GUI.DragWindow();
		}

		public void UpdateRoverState(Vessel vessel, bool stateActive) {
			foreach (var rover in activeRovers)
			{
				if (rover.vessel == vessel)
				{
					rover.bvActive = stateActive;
					if (stateActive)
						InputLockManager.SetControlLock(lockMask, "BonVoyageInputLock");	
					else
						InputLockManager.RemoveControlLock("BonVoyageInputLock");	
					return;
				}
			}
		}

		public void LoadRovers()
		{
			activeRovers.Clear();
			foreach (var vessel in FlightGlobals.Vessels)
			{
				ConfigNode vesselConfigNode = new ConfigNode();
				vessel.protoVessel.Save(vesselConfigNode);
				//FindPartModulesImplementing
				// This is annoying
				var BVPart = vesselConfigNode.GetNode("PART", "name", "BonVoyageModule");
				if (BVPart == null)
					BVPart = vesselConfigNode.GetNode("PART", "name", "Malemute.RoverCab");
				if (BVPart == null)
					BVPart = vesselConfigNode.GetNode("PART", "name", "KER.RoverCab");
				if (BVPart == null)
					BVPart = vesselConfigNode.GetNode("PART", "name", "WBI.BuffaloCab");
				if (BVPart == null)
					BVPart = vesselConfigNode.GetNode("PART", "name", "ARESrovercockpit");
				if (BVPart == null)
					BVPart = vesselConfigNode.GetNode("PART", "name", "Puma Pod");

				if (BVPart == null) continue;

				var BVModule = BVPart.GetNode("MODULE", "name", "BonVoyageModule");
				if (BVModule == null) continue;

				activeRovers.Add(new ActiveRover(vessel));
			}
		}
	}
}
