using System;
using System.Collections.Generic;
//using System.Linq;
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
		private List<ActiveRover> activeRovers;
//		private const double PI = Math.PI;
		public static BonVoyage Instance;
		private ApplicationLauncherButton appLauncherButton;
		private DateTime lastUpdated;
		private PluginConfiguration config;
		bool autoDewarp;

		//GUI
		private bool guiVisible;
		private bool globalHidden;
		private Rect guiRect;
		private int guiId;
		Vector2 mainWindowScrollPosition;

		public void Awake()
		{
//			Debug.Log("Bon Voyage Awake()");
			if (Instance != null)
			{
				Destroy(this);
				return;
			}
			Instance = this;

			guiVisible = false;
			globalHidden = false;
			guiId = GUIUtility.GetControlID(FocusType.Passive);
			config = PluginConfiguration.CreateForType<BonVoyage> ();
			config.load ();
			autoDewarp = config.GetValue<bool> ("autoDewarp", false);
			Rect sample = new Rect ();
			sample.width = 700;
			sample.height = 500;
			sample.center = new Vector2(Screen.width / 2, Screen.height / 2);
			guiRect = config.GetValue<Rect> ("guiRect", new Rect (sample));
			config.save ();
			lastUpdated = DateTime.Now;
			activeRovers = new List<ActiveRover>();
			activeRovers = new List<ActiveRover> ();
			mainWindowScrollPosition = new Vector2 (0, 0);
		}

		public void Start()
		{
//			Debug.Log("Bon Voyage Start()");
			DontDestroyOnLoad(this);
			GameEvents.onGUIApplicationLauncherReady.Add(onGUIApplicationLauncherReady);
			GameEvents.onGameSceneSwitchRequested.Add (onGameSceneSwitchRequested);
//			GameEvents.onGameStatePostLoad.Add (onGameStatePostLoad);
			GameEvents.onLevelWasLoaded.Add (onLevelWasLoaded);
			GameEvents.onHideUI.Add (onHideUI);
			GameEvents.onShowUI.Add (onShowUI);
			LoadRovers ();
		}

		public void OnDestroy()
		{
			GameEvents.onGUIApplicationLauncherReady.Remove(onGUIApplicationLauncherReady);
			GameEvents.onGameSceneSwitchRequested.Remove (onGameSceneSwitchRequested);
//			GameEvents.onGameStatePostLoad.Remove (onGameStatePostLoad);
			GameEvents.onLevelWasLoaded.Remove (onLevelWasLoaded);
			GameEvents.onHideUI.Remove (onHideUI);
			GameEvents.onShowUI.Remove (onShowUI);
			if (appLauncherButton != null)
			{
				ApplicationLauncher.Instance.RemoveModApplication(appLauncherButton);
			}
			config.SetValue ("autoDewarp", autoDewarp);
			config.SetValue ("guiRect", guiRect);
			config.save ();
		}

		private void onHideUI() {
			globalHidden = true;
		}

		private void onShowUI() {
			globalHidden = false;
		}

		public void onGameSceneSwitchRequested(GameEvents.FromToAction<GameScenes, GameScenes> ev) {
			if (appLauncherButton != null)
				appLauncherButton.SetFalse ();
			guiVisible = false; // Why?? WHYYYY????!!!
		}

//		public void onGameStatePostLoad (ConfigNode cn) {
		public void onLevelWasLoaded(GameScenes scene) {
			LoadRovers ();
		}

		public void Update() {
			if (lastUpdated.AddSeconds (1) > DateTime.Now)
				return;
			
			lastUpdated = DateTime.Now;

			double currentTime = Planetarium.GetUniversalTime();

			foreach (var rover in activeRovers) {
				if (rover.vessel.isActiveVessel) {
					rover.status = "current";
					continue;
				}

				if (!rover.bvActive) {
					rover.status = "idle";
					continue;
				}

				if (rover.vessel.loaded)
					continue;

				Vector3d vesselPos = rover.vessel.mainBody.position - rover.vessel.GetWorldPos3D();
				Vector3d toKerbol = rover.vessel.mainBody.position - FlightGlobals.Bodies[0].position;
				double angle = Vector3d.Angle (vesselPos, toKerbol);

				// No moving at night, or when there's not enougth solar light 
				if (angle >= 85 && rover.solarPowered) {
					rover.status = "awaiting sunlight";
					rover.lastTime = currentTime;
					rover.BVModule.SetValue ("lastTime", currentTime.ToString ());
					rover.vessel.protoVessel = new ProtoVessel(rover.vesselConfigNode, HighLogic.CurrentGame);
					continue;
				}

				double deltaT = currentTime - rover.lastTime;

				double deltaS = rover.averageSpeed * deltaT;
				double bearing = GeoUtils.InitialBearing (
					rover.vessel.latitude,
					rover.vessel.longitude,
					rover.targetLatitude,
					rover.targetLongitude
				);

				rover.distanceTravelled += deltaS;
				if (rover.distanceTravelled >= rover.distanceToTarget) {
					rover.distanceTravelled = rover.distanceToTarget;
					rover.vessel.latitude = rover.targetLatitude;
					rover.vessel.longitude = rover.targetLongitude;

					rover.bvActive = false;
					rover.BVModule.SetValue ("isActive", "False");
					rover.BVModule.SetValue ("distanceTravelled", rover.distanceToTarget.ToString ());

					rover.BVModule.GetNode ("EVENTS").GetNode ("Activate").SetValue ("active", "True");
					rover.BVModule.GetNode ("EVENTS").GetNode ("Deactivate").SetValue ("active", "False");

					if (autoDewarp) {
						TimeWarp.SetRate (0, false);
						ScreenMessages.PostScreenMessage (rover.vessel.vesselName + " has arrived to destination at " + rover.vessel.mainBody.name);
					}

					MessageSystem.Message message = new MessageSystem.Message (
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
					MessageSystem.Instance.AddMessage (message);
					rover.status = "idle";
				} else {
					int steps = Convert.ToInt32(Math.Floor(rover.distanceTravelled / 1000));
					double remainder = rover.distanceTravelled % 1000;

					bearing = GeoUtils.InitialBearing (
						rover.path[steps].latitude,
						rover.path[steps].longitude,
						rover.path[steps + 1].latitude,
						rover.path[steps + 1].longitude
					);

					double[] newCoordinates = GeoUtils.GetLatitudeLongitude (
						rover.path[steps].latitude,
						rover.path[steps].longitude,
						bearing,
						remainder,
						rover.vessel.mainBody.Radius
					);

					rover.vessel.latitude = newCoordinates [0];
					rover.vessel.longitude = newCoordinates [1];
					rover.status = "roving";
				}
				rover.vessel.altitude = GeoUtils.TerrainHeightAt(rover.vessel.latitude, rover.vessel.longitude, rover.vessel.mainBody);
				rover.toTravel = rover.distanceToTarget - rover.distanceTravelled;
				rover.lastTime = currentTime;

				// Save data to protovessel
				rover.vesselConfigNode.SetValue("lat", rover.vessel.latitude.ToString());
				rover.vesselConfigNode.SetValue("lon", rover.vessel.longitude.ToString());
				rover.vesselConfigNode.SetValue("alt", rover.vessel.altitude.ToString());
				rover.vesselConfigNode.SetValue("landedAt", rover.vessel.mainBody.bodyName);
				rover.BVModule.SetValue ("distanceTravelled", (rover.distanceTravelled).ToString ());
				rover.BVModule.SetValue ("lastTime", currentTime.ToString ());
				rover.vessel.protoVessel = new ProtoVessel(rover.vesselConfigNode, HighLogic.CurrentGame);
			}
		}

		public void onGUIApplicationLauncherReady()
		{
			if (appLauncherButton == null)
			{
				appLauncherButton = ApplicationLauncher.Instance.AddModApplication(
					ToggleGUI,
					ToggleGUI,
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
			if (guiVisible && !globalHidden)
			{
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
		}

		public void ToggleGUI()
		{
			guiVisible = !guiVisible;
		}

		public void DrawGUI(int guiId)
		{
			double currentTime = Planetarium.GetUniversalTime();
			GUILayout.BeginVertical();
			mainWindowScrollPosition = GUILayout.BeginScrollView (mainWindowScrollPosition);
//			foreach (var rover in activeRovers) {
			foreach (var rover in activeRovers) {
				switch (rover.status) {
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
				GUILayout.BeginHorizontal ();
				if (GUILayout.Button (
						rover.vessel.vesselName,
						GUILayout.Height (25),
						GUILayout.Width (150)
				   ))
				{
					if (HighLogic.LoadedScene == GameScenes.TRACKSTATION) {
						PlanetariumCamera.fetch.SetTarget (rover.vessel.mapObject);
					}
					if (HighLogic.LoadedSceneIsFlight) {
						MapView.EnterMapView ();
						PlanetariumCamera.fetch.SetTarget (rover.vessel.mapObject);
					}
				}
				if (GUILayout.Button ("Switch to", GUILayout.Height (25), GUILayout.Width(100))) {
					GamePersistence.SaveGame ("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
					FlightDriver.StartAndFocusVessel ("persistent", FlightGlobals.Vessels.IndexOf (rover.vessel.mapObject.vessel));					
				}

				GUILayout.Label (
					rover.vessel.mainBody.bodyName,
					GUILayout.Width(75)
				);
				GUILayout.Label (
					rover.status,
					GUILayout.Width(75)
				);
				if (rover.status == "roving" || rover.status == "awaiting sunlight") {
					GUILayout.Label ("v̅ = " + rover.averageSpeed.ToString("N") + ", yet to travel " +
						rover.toTravel.ToString ("N0") + " meters"
					);
				}
				if (rover.status == "idle") {
					TimeSpan t = TimeSpan.FromSeconds(currentTime - rover.lastTime);

					string idlePeriod = string.Format(
						"{0:D2}h:{1:D2}m:{2:D2}s", 
						t.Hours,
						t.Minutes,
						t.Seconds
					);
					
					GUILayout.Label (idlePeriod);
				}
				GUILayout.EndHorizontal ();
			}
			GUILayout.EndScrollView ();
			GUI.contentColor = Color.white;
			autoDewarp = GUILayout.Toggle (autoDewarp, "Automagic dewarp"); 

			GUILayout.EndVertical();
			GUI.DragWindow();
		}

		public void LoadRovers() {
			activeRovers.Clear ();
			foreach (var vessel in FlightGlobals.Vessels) {
				ConfigNode vesselConfigNode = new ConfigNode();
				vessel.protoVessel.Save(vesselConfigNode);

				// This is annoying
				var BVPart = vesselConfigNode.GetNode ("PART", "name", "BonVoyageModule");
				if (BVPart == null)
					BVPart = vesselConfigNode.GetNode ("PART", "name", "Malemute.RoverCab");
				if (BVPart == null)
					BVPart = vesselConfigNode.GetNode ("PART", "name", "KER.RoverCab");
				if (BVPart == null)
					BVPart = vesselConfigNode.GetNode ("PART", "name", "WBI.BuffaloCab");

				if (BVPart == null) continue;

				var BVModule = BVPart.GetNode ("MODULE", "name", "BonVoyageModule");
				if (BVModule == null) continue;

				activeRovers.Add (new ActiveRover (vessel));
			}
		}
	}
}
