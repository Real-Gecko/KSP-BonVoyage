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
		private IButton toolbarButton;
		private DateTime lastUpdated;
		private bool gamePaused;

		// Config stuff
		private PluginConfiguration config;
		private bool autoDewarp;
		public bool AutoDewarp { get { return autoDewarp; } }
		private bool useKSPSkin;

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
		private bool useToolbar;
		private bool guiVisible;
		private bool globalHidden;
		private Rect guiRect;
		private int guiId;
		Vector2 mainWindowScrollPosition;

		/// <summary>
		/// Instead of constructor.
		/// </summary>
		public void Awake()
		{
			if (Instance != null)
			{
				Destroy(this);
				return;
			}
			Instance = this;

			toolbarButton = null;
			guiVisible = false;
			globalHidden = false;
			gamePaused = false;

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
			useKSPSkin = config.GetValue<bool> ("useKSPSkin", false);
			useToolbar = config.GetValue<bool> ("useToolbar", false);
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

		/// <summary>
		/// Initial instance start.
		/// </summary>
		public void Start()
		{
			DontDestroyOnLoad(this);
			GameEvents.onGUIApplicationLauncherReady.Add(onGUIApplicationLauncherReady);
			GameEvents.onGameSceneSwitchRequested.Add(onGameSceneSwitchRequested);
			GameEvents.onLevelWasLoaded.Add(onLevelWasLoaded);
			GameEvents.onVesselChange.Add(onVesselChange);
			GameEvents.onHideUI.Add(onHideUI);
			GameEvents.onShowUI.Add(onShowUI);
			GameEvents.onGamePause.Add (onGamePause);
			GameEvents.onGameUnpause.Add (onGameUnpause);
			LoadRovers();
		}

		/// <summary>
		/// Cleanup on destruction.
		/// </summary>
		public void OnDestroy()
		{
			GameEvents.onGUIApplicationLauncherReady.Remove(onGUIApplicationLauncherReady);
			GameEvents.onGameSceneSwitchRequested.Remove(onGameSceneSwitchRequested);
			GameEvents.onLevelWasLoaded.Remove(onLevelWasLoaded);
			GameEvents.onVesselChange.Remove(onVesselChange);
			GameEvents.onHideUI.Remove(onHideUI);
			GameEvents.onShowUI.Remove(onShowUI);
			GameEvents.onGamePause.Remove (onGamePause);
			GameEvents.onGameUnpause.Remove (onGameUnpause);

			DestroyLauncher ();

			config.SetValue("autoDewarp", autoDewarp);
			config.SetValue("guiRect", guiRect);
			config.SetValue ("useKSPSkin", useKSPSkin);
			config.SetValue ("useToolbar", useToolbar);
			config.save();
			InputLockManager.RemoveControlLock("BonVoyageInputLock");
		}

		public void onGamePause() {
			gamePaused = true;
		}

		public void onGameUnpause() {
			gamePaused = false;
		}

		/// <summary>
		/// Hide UI on scene switch.
		/// </summary>
		/// <param name="ev">Ev.</param>
		public void onGameSceneSwitchRequested(GameEvents.FromToAction<GameScenes, GameScenes> ev)
		{
			if (appLauncherButton != null)
				appLauncherButton.SetFalse();
			guiVisible = false;
		}

		/// <summary>
		/// Active vessel changed, deal with it.
		/// </summary>
		/// <param name="vessel">Vessel.</param>
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

		/// <summary>
		/// Hide UI.
		/// </summary>
		private void onHideUI()
		{
			globalHidden = true;
		}

		/// <summary>
		/// Show UI.
		/// </summary>
		private void onShowUI()
		{
			globalHidden = false;
		}

		/// <summary>
		/// Scene was loaded, update controlled rover list.
		/// </summary>
		/// <param name="scene">Scene.</param>
		public void onLevelWasLoaded(GameScenes scene)
		{
			gamePaused = false;
			LoadRovers();
			onVesselChange(FlightGlobals.ActiveVessel);
		}

		/// <summary>
		/// Update controlled rovers once a second.
		/// </summary>
		public void Update()
		{
			if (gamePaused)
				return;

			if (lastUpdated.AddSeconds(1) > DateTime.Now)
				return;

			lastUpdated = DateTime.Now;

			double currentTime = Planetarium.GetUniversalTime();
			
			for(int i=0; i<activeRovers.Count;++i)
				activeRovers[i].Update (currentTime);
		}

		/// <summary>
		/// Deal with launcher button.
		/// </summary>
		private void CreateLauncher() {
			if (ToolbarManager.ToolbarAvailable && useToolbar && !HighLogic.LoadedSceneIsEditor) {
				toolbarButton = ToolbarManager.Instance.add ("BonVoyage", "AppLaunch");
				toolbarButton.TexturePath = "BonVoyage/Textures/bon-voyage-icon-toolbar";
				toolbarButton.ToolTip = "Bon Voyage Controller";
				toolbarButton.Visible = true;
				toolbarButton.OnClick += (ClickEvent e) => {
					onToggle();
				};
			}
			else if (appLauncherButton == null)
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

		/// <summary>
		/// Clear launcher button
		/// </summary>
		private void DestroyLauncher() {
			if (appLauncherButton != null) {
				ApplicationLauncher.Instance.RemoveModApplication (appLauncherButton);
				appLauncherButton = null;
			}

			if (toolbarButton != null) {
				toolbarButton.Destroy ();
				toolbarButton = null;
			}
		}

		/// <summary>
		/// Laucnher ready to be initialized
		/// </summary>
		public void onGUIApplicationLauncherReady()
		{
			CreateLauncher ();
		}

		/// <summary>
		/// Draw UI
		/// </summary>
		public void OnGUI()
		{
			if (gamePaused || globalHidden) return;

			if (InputLockManager.GetControlLock("BonVoyageInputLock") != 0)
			{
				GUILayout.BeginArea(labelRect);
				GUILayout.Label("Bon Voyage control lock active", labelStyle);
				GUILayout.EndArea();
			}

			if (!guiVisible) return;

			if (useKSPSkin)
				GUI.skin = HighLogic.Skin;
			else
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

		public void onToggle() {
			guiVisible = !guiVisible;
		}

		public void DrawGUI(int guiId)
		{
			double currentTime = Planetarium.GetUniversalTime();
			GUILayout.BeginVertical();
			mainWindowScrollPosition = GUILayout.BeginScrollView(mainWindowScrollPosition);
			for(int i=0; i<activeRovers.Count;++i)
			{
				var rover = activeRovers[i];
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
					if (rover.vessel.loaded)
						FlightGlobals.SetActiveVessel (rover.vessel);
					else {
						GamePersistence.SaveGame ("persistent", HighLogic.SaveFolder, SaveMode.OVERWRITE);
						FlightDriver.StartAndFocusVessel ("persistent", FlightGlobals.Vessels.IndexOf (rover.vessel));
					}
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
						"v̅ = " + rover.AverageSpeed.ToString("N") + ", yet to travel " +
						rover.yetToTravel.ToString("N0") + " meters"
					);
				}

				if (rover.status == "idle")
				{
					TimeSpan t = TimeSpan.FromSeconds(currentTime - rover.LastTime);

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
			autoDewarp = GUILayout.Toggle(autoDewarp, "Automagic Dewarp");
			useKSPSkin = GUILayout.Toggle (useKSPSkin, "Use KSP Skin");
			GUILayout.BeginHorizontal ();
			if (GUILayout.Button ("Close", GUILayout.Height(25))) {
				onToggle ();
			}
			if (GUILayout.Button("Switch Toolbar", GUILayout.Height(25), GUILayout.Width(150))) {
				useToolbar = !useToolbar;
				DestroyLauncher ();
				CreateLauncher ();
			}
			GUILayout.EndHorizontal ();
			GUILayout.EndVertical();
			GUI.DragWindow();
		}

		public void UpdateRoverState(Vessel vessel, bool stateActive) {
			for(int i=0; i<activeRovers.Count;++i)
			{
				var rover = activeRovers[i];
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

				foreach(ConfigNode part in vesselConfigNode.GetNodes("PART")) {
					ConfigNode BVModule = part.GetNode("MODULE", "name", "BonVoyageModule");
					if (BVModule != null) {
						activeRovers.Add (new ActiveRover (vessel, BVModule, vesselConfigNode));
						break;
					}
				}

			}
		}
	}
}
