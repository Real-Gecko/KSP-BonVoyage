using System;
using System.Collections.Generic;
//using System.Linq;
using UnityEngine;
using KSP.UI.Screens;
using KSP.IO;

namespace BonVoyage
{
	[KSPAddon(KSPAddon.Startup.SpaceCentre, true)]
	public class BonVoyage : MonoBehaviour
	{
		private List<ActiveRover> activeRovers;
		private const double PI = Math.PI;
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
			sample.width = 500;
			sample.height = 400;
			sample.center = new Vector2(Screen.width / 2, Screen.height / 2);
			guiRect = config.GetValue<Rect> ("guiRect", new Rect (sample));
			config.save ();
			lastUpdated = DateTime.Now;
			activeRovers = new List<ActiveRover>();
			mainWindowScrollPosition = new Vector2 (0, 0);
		}

		public void Start()
		{
//			Debug.Log("Bon Voyage Start()");
			DontDestroyOnLoad(this);
			GameEvents.onGUIApplicationLauncherReady.Add(onGUIApplicationLauncherReady);
			GameEvents.onGameSceneSwitchRequested.Add (onGameSceneSwitchRequested);
			GameEvents.onHideUI.Add (onHideUI);
			GameEvents.onShowUI.Add (onShowUI);
		}

		public void OnDestroy()
		{
			GameEvents.onGUIApplicationLauncherReady.Remove(onGUIApplicationLauncherReady);
			GameEvents.onGameSceneSwitchRequested.Remove (onGameSceneSwitchRequested);
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

		public void Update()
		{
			if (lastUpdated.AddSeconds(1) < DateTime.Now) {
				lastUpdated = DateTime.Now;

				double currentTime = Planetarium.GetUniversalTime();
				activeRovers.Clear ();

				foreach (var vessel in FlightGlobals.Vessels) {
					if (!vessel.loaded && !vessel.isActiveVessel && vessel.situation == Vessel.Situations.LANDED && vessel.protoVessel != null) {

						/* 
						 * If rover gets too close to active vessel we will have tons of errors, dunno what to do
						 * 
						 * NullReferenceException: Object reference not set to an instance of an object
						 * at ProtoPartSnapshot.Load (.Vessel vesselRef, Boolean loadAsRootPart) [0x00000] in <filename unknown>:0 
						 * at ProtoVessel.LoadObjects () [0x00000] in <filename unknown>:0
						 * at Vessel.Load () [0x00000] in <filename unknown>:0
						 * at Vessel.Update () [0x00000] in <filename unknown>:0 
						*/
/*						if (FlightGlobals.ActiveVessel != null) {
							Vector3d toRover = FlightGlobals.ActiveVessel.GetWorldPos3D () - vessel.GetWorldPos3D ();
							double distance = toRover.magnitude;
							TimeWarp.SetRate (0, true);
							if(distance <= 2500) {
								GamePersistence.SaveGame ("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
								FlightDriver.StartAndFocusVessel ("persistent", FlightGlobals.Vessels.IndexOf (vessel));					
							}
						}
*/
						ConfigNode vesselConfigNode = new ConfigNode();
						vessel.protoVessel.Save(vesselConfigNode);

						var BVPart = vesselConfigNode.GetNode ("PART", "name", "BonVoyageModule");
						if (BVPart == null) continue;

						var BVModule = BVPart.GetNode ("MODULE", "name", "BonVoyageModule");
						if (BVModule == null) continue;

						double lastTime = 0;
						double targetLatitude = 0;
						double targetLongitude = 0;
						double vesselAltitude = 0;
						double averageSpeed = 0;

						if (!bool.Parse (BVModule.GetValue ("isActive"))) {
							activeRovers.Add(new ActiveRover(vessel.vesselName, vessel.mainBody.name, "goofing", 0, vessel.mapObject));
							continue;
						}
						Debug.Log ("bon voyage - stargin parse");
						lastTime = double.Parse (BVModule.GetValue ("lastTime"));
						BVModule.SetValue ("lastTime", currentTime.ToString ());

						if (lastTime == 0) {
							vessel.protoVessel = new ProtoVessel (vesselConfigNode, HighLogic.CurrentGame);
							continue;
						}
							
//						Vector3d vesselPos = vessel.mainBody.GetRelSurfacePosition(vessel.latitude, vessel.longitude, vessel.altitude);
						Vector3d vesselPos = vessel.mainBody.position - vessel.GetWorldPos3D();
						Vector3d toKerbol = vessel.mainBody.position - FlightGlobals.Bodies[0].position;
						double angle = Vector3d.Angle (vesselPos, toKerbol);
//						ScreenMessages.PostScreenMessage(angle.ToString());

						double distanceTravelled = double.Parse(BVModule.GetValue ("distanceTravelled"));
						double distanceToTarget = double.Parse (BVModule.GetValue ("distanceToTarget"));
						bool solarPowered = bool.Parse (BVModule.GetValue ("solarPowered"));
						Debug.Log ("bon voyage - time parsed");

						// No moving at night, or when there's not enougth solar light 
						if (angle >= 85 && solarPowered) {
							vessel.protoVessel = new ProtoVessel (vesselConfigNode, HighLogic.CurrentGame);
							activeRovers.Add(new ActiveRover(vessel.vesselName, vessel.mainBody.name, "awaiting sunlight", distanceToTarget - distanceTravelled, vessel.mapObject));
							continue;
						}

						targetLatitude = double.Parse (BVModule.GetValue ("targetLatitude"));
						targetLongitude = double.Parse (BVModule.GetValue ("targetLongitude"));
						averageSpeed = double.Parse(BVModule.GetValue ("averageSpeed"));

						double deltaT = currentTime - lastTime;

						double deltaS = averageSpeed * deltaT;
						double bearing = GeoUtils.InitialBearing (vessel.latitude, vessel.longitude, targetLatitude, targetLongitude);

						distanceTravelled += deltaS;
						if (distanceTravelled >= distanceToTarget) {
							distanceTravelled = distanceToTarget;
							vessel.latitude = targetLatitude;
							vessel.longitude = targetLongitude;
							vesselAltitude = GeoUtils.TerrainHeightAt(vessel.altitude, vessel.longitude, vessel.mainBody);
							vessel.altitude = vesselAltitude;

							vesselConfigNode.SetValue ("lat", targetLatitude.ToString());
							vesselConfigNode.SetValue ("lon", targetLongitude.ToString());
							vesselConfigNode.SetValue ("alt", vesselAltitude.ToString ());

							BVModule.SetValue ("isActive", "False");
							BVModule.SetValue ("distanceTravelled", distanceToTarget.ToString ());

							BVModule.GetNode ("EVENTS").GetNode ("Activate").SetValue ("active", "True");
							BVModule.GetNode ("EVENTS").GetNode ("Deactivate").SetValue ("active", "False");

							if (autoDewarp) {
								TimeWarp.SetRate (0, false);
								ScreenMessages.PostScreenMessage (vessel.vesselName + " has arrived to destination at " + vessel.mainBody.name);
							}

							MessageSystem.Message message = new MessageSystem.Message (
								"Rover arrived",
								//------------------------------------------
								vessel.vesselName + " has arrived to destination\nLAT:" +
								targetLatitude.ToString("F2") + "\nLON:" + targetLongitude.ToString("F2") +
								" at " + vessel.mainBody.name + ". \n" +
								"Distance travelled: " + distanceTravelled.ToString("N") + " meters",
								//------------------------------------------
								MessageSystemButton.MessageButtonColor.GREEN,
								MessageSystemButton.ButtonIcons.COMPLETE
							);
							MessageSystem.Instance.AddMessage (message);
						} else {
							BVModule.SetValue ("distanceTravelled", (distanceTravelled).ToString ());

							double[] newCoordinates = GeoUtils.GetLatitudeLongitude (vessel.latitude, vessel.longitude, bearing, deltaS, vessel.mainBody.Radius);

							vessel.latitude = newCoordinates [0];
							vessel.longitude = newCoordinates [1];
							vesselAltitude = GeoUtils.TerrainHeightAt (newCoordinates[0], newCoordinates[1], vessel.mainBody);
							vessel.altitude = vesselAltitude;

							vesselConfigNode.SetValue ("lat", newCoordinates [0].ToString ());
							vesselConfigNode.SetValue ("lon", newCoordinates [1].ToString ());
							vesselConfigNode.SetValue ("alt", vesselAltitude.ToString ());
						}
						vessel.protoVessel = new ProtoVessel(vesselConfigNode, HighLogic.CurrentGame);
						activeRovers.Add(new ActiveRover(vessel.vesselName, vessel.mainBody.name, "roving", distanceToTarget - distanceTravelled, vessel.mapObject));
					}
				}
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
			GUILayout.BeginVertical();
			mainWindowScrollPosition = GUILayout.BeginScrollView (mainWindowScrollPosition);
			foreach (var rover in activeRovers) {
				switch (rover.status) {
					case "roving":
						GUI.contentColor = Color.green;
						break;
					case "goofing":
						GUI.contentColor = Color.yellow;
						break;
					case "awaiting sunlight":
						GUI.contentColor = Color.red;
						break;
				}
				GUILayout.BeginHorizontal ();
				if (GUILayout.Button (
					   rover.name + " " + rover.status + " at " + rover.bodyName + ", yet to travel " + rover.toTravel.ToString ("N") + " meters",
						GUILayout.Height (25),
						GUILayout.Width (400)
				   ))
				{
					if (HighLogic.LoadedScene == GameScenes.TRACKSTATION) {
						PlanetariumCamera.fetch.SetTarget (rover.mapObject);
					}
					if (HighLogic.LoadedSceneIsFlight) {
						MapView.EnterMapView ();
						PlanetariumCamera.fetch.SetTarget (rover.mapObject);
					}
				}
				if (GUILayout.Button ("Switch to", GUILayout.Height (25))) {
					GamePersistence.SaveGame ("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
					FlightDriver.StartAndFocusVessel ("persistent", FlightGlobals.Vessels.IndexOf (rover.mapObject.vessel));					
				}
				GUILayout.EndHorizontal ();
			}
			GUILayout.EndScrollView ();
			GUI.contentColor = Color.white;
			autoDewarp = GUILayout.Toggle (autoDewarp, "Automagic dewarp"); 

//			GUILayout.TextField (latitude.ToString());
//			GUILayout.TextField (longitude.ToString());
//			GUILayout.TextField (bearing.ToString());
//			GUILayout.Label("Latitude: " + latitude.ToString());
//			GUILayout.Label("Longitude: " + longitude.ToString());
//			GUILayout.Label ("Bearing: " + bearing.ToString());
//			GUILayout.Label("Distance: " + latitude);

/*			if (GUILayout.Button ("Pick on map")) {
				//targetBody = FlightGlobals.ActiveVessel.mainBody;
				MapView.EnterMapView();
				guiVisible = false;
				//mapLocationMode = true;
			}
			if (GUILayout.Button ("Poehali")) {
//				KSP.IO.FileStream file = KSP.IO.File.Create<BonVoyage> ("route", FlightGlobals.ActiveVessel);
//				ConfigNode route = new ConfigNode ("ROUTE");
//				route.AddValue ("latitude", latitude);
//				route.AddValue ("longitude", longitude);
//				route.Save (file.Name);

//				KSP.IO.File.
//				FlightGlobals.ActiveVessel.id
				//FlightGlobals.Vessels.
			}*/
			GUILayout.EndVertical();
			GUI.DragWindow();
		}
	}
}
