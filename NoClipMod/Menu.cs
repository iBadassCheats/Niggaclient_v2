using System;
using System.Collections.Generic;
using System.Text;
using Il2CppSystem.Collections.Generic;
using MelonLoader;
using UnhollowerBaseLib;
using UnityEngine;
using VRC.SDKBase;

namespace NoClipMod;

public class Menu
{
	private enum MenuPage
	{
		LocalPlayer,
		Udon,
		Misc
	}

	private bool windowVisible = true;

	private Rect windowRect = new Rect(20f, 20f, 250f, 300f);

	private bool pickupSettingsVisible;

	private Rect pickupSettingsRect = new Rect(300f, 20f, 300f, 400f);

	private bool avatarSettingsVisible;

	private Rect avatarSettingsRect = new Rect(300f, 20f, 300f, 400f);

	private bool espSettingsVisible;

	private Rect espSettingsRect = new Rect(300f, 20f, 300f, 400f);

	private NoClip noClip;

	private bool wasRightCtrlPressed;

	private bool wasInsertPressed;

	private float currentFps;

	private float lastFpsUpdate;

	private const float FPS_UPDATE_INTERVAL = 0.5f;

	private bool showFpsCounter = true;

	private MenuPage currentPage;

	private MelonPreferences_Category category;

	private MelonPreferences_Entry<float> flightSpeedEntry;

	private MelonPreferences_Entry<KeyCode> flightToggleKeyEntry;

	private MelonPreferences_Entry<KeyCode> ascendKeyEntry;

	private MelonPreferences_Entry<KeyCode> descendKeyEntry;

	private MelonPreferences_Entry<KeyCode> forwardKeyEntry;

	private MelonPreferences_Entry<KeyCode> backwardKeyEntry;

	private MelonPreferences_Entry<KeyCode> leftKeyEntry;

	private MelonPreferences_Entry<KeyCode> rightKeyEntry;

	private MelonPreferences_Entry<Color> indicatorColorEntry;

	private MelonPreferences_Entry<bool> showIndicatorEntry;

	private Il2CppReferenceArray<GUILayoutOption> emptyOptions;

	private VRCPlayerApi selectedPlayer;

	private Vector2 playerListScrollPosition = Vector2.zero;

	private bool showPlayerOptions;

	private Il2CppReferenceArray<VRCPlayerApi> cachedPlayers;

	private float lastPlayerListUpdate;

	private const float PLAYER_LIST_UPDATE_INTERVAL = 0.5f;

	private float currentMovementSpeed = 2f;

	private float currentJumpImpulse = 3f;

	private float currentGravityStrength = 1f;

	private bool isOrbiterActive;

	private bool isNukeActive;

	private PickupManager pickupManager;

	private float avatarScale = 1f;

	private bool antiAvatars;

	private bool showESP;

	private bool showBoxESP = true;

	private bool showNameESP = true;

	private bool showDistanceESP = true;

	private float espDistance = 50f;

	private Color espColor = Color.yellow;

	private float espLineWidth = 2f;

	public Menu(NoClip noClip)
	{
		this.noClip = noClip;
		InitializePreferences();
		emptyOptions = new Il2CppReferenceArray<GUILayoutOption>(0L);
		pickupManager = new PickupManager();
	}

	private void InitializePreferences()
	{
		try
		{
			category = MelonPreferences.CreateCategory("NoClip");
			flightSpeedEntry = category.CreateEntry("FlightSpeed", 25f, "Flight Speed", "Speed multiplier for flight mode");
			flightToggleKeyEntry = category.CreateEntry("FlightToggleKey", KeyCode.F, "Flight Toggle Key", "Key to toggle flight mode (requires Ctrl)");
			ascendKeyEntry = category.CreateEntry("AscendKey", KeyCode.E, "Ascend Key", "Key to move upward");
			descendKeyEntry = category.CreateEntry("DescendKey", KeyCode.Q, "Descend Key", "Key to move downward");
			forwardKeyEntry = category.CreateEntry("ForwardKey", KeyCode.W, "Forward Key", "Key to move forward");
			backwardKeyEntry = category.CreateEntry("BackwardKey", KeyCode.S, "Backward Key", "Key to move backward");
			leftKeyEntry = category.CreateEntry("LeftKey", KeyCode.A, "Left Key", "Key to move left");
			rightKeyEntry = category.CreateEntry("RightKey", KeyCode.D, "Right Key", "Key to move right");
			indicatorColorEntry = category.CreateEntry("IndicatorColor", Color.cyan, "Indicator Color", "Color of the flight mode indicator");
			showIndicatorEntry = category.CreateEntry("ShowIndicator", default_value: true, "Show Indicator", "Whether to show the flight mode indicator");
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error initializing preferences: " + ex.Message);
		}
	}

	public void OnGUI()
	{
		if (showFpsCounter)
		{
			if (Time.time - lastFpsUpdate > 0.5f)
			{
				currentFps = 1f / Time.deltaTime;
				lastFpsUpdate = Time.time;
			}
			GUI.Label(new Rect(10f, 10f, 300f, 20f), $"NiggaClient^2 | FPS: {currentFps:F1}", new GUIStyle
			{
				normal = 
				{
					textColor = Color.white
				}
			});
		}
		if (windowVisible)
		{
			windowRect = GUI.Window(0, windowRect, (Action<int>)DrawWindow, "NiggaClient^2");
			if (pickupSettingsVisible)
			{
				pickupSettingsRect = GUI.Window(1, pickupSettingsRect, (Action<int>)DrawPickupSettingsWindow, "Pickup Settings");
			}
			if (avatarSettingsVisible)
			{
				avatarSettingsRect = GUI.Window(2, avatarSettingsRect, (Action<int>)DrawAvatarSettingsWindow, "Avatar Settings");
			}
			if (espSettingsVisible)
			{
				espSettingsRect = GUI.Window(3, espSettingsRect, (Action<int>)DrawESPSettingsWindow, "ESP Settings");
			}
		}
		ESPManager.Instance.OnGUI();
	}

	public void Update()
	{
		bool key = Input.GetKey(KeyCode.RightControl);
		if (key && !wasRightCtrlPressed)
		{
			ToggleWindow();
		}
		wasRightCtrlPressed = key;
		bool key2 = Input.GetKey(KeyCode.Insert);
		if (key2 && !wasInsertPressed)
		{
			ToggleWindow();
		}
		wasInsertPressed = key2;
	}

	private void DrawWindow(int windowID)
	{
		try
		{
			GUILayout.BeginHorizontal(emptyOptions);
			if (GUILayout.Toggle(currentPage == MenuPage.LocalPlayer, "Local Player", GUI.skin.button, emptyOptions))
			{
				currentPage = MenuPage.LocalPlayer;
			}
			if (GUILayout.Toggle(currentPage == MenuPage.Udon, "Udon", GUI.skin.button, emptyOptions))
			{
				currentPage = MenuPage.Udon;
			}
			if (GUILayout.Toggle(currentPage == MenuPage.Misc, "Misc", GUI.skin.button, emptyOptions))
			{
				currentPage = MenuPage.Misc;
			}
			GUILayout.EndHorizontal();
			GUILayout.Space(10f);
			switch (currentPage)
			{
			case MenuPage.LocalPlayer:
				DrawLocalPlayerPage();
				break;
			case MenuPage.Udon:
				DrawUdonPage();
				break;
			case MenuPage.Misc:
				DrawMiscPage();
				break;
			}
			GUI.DragWindow();
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error in DrawWindow: " + ex.Message);
		}
	}

	private void DrawLocalPlayerPage()
	{
		GUILayout.Label("NoClip: " + (noClip.IsFlightEnabled ? "ON" : "OFF"), emptyOptions);
		GUILayout.Space(5f);
		GUILayout.Label("Custom Speed:", emptyOptions);
		if (float.TryParse(GUILayout.TextField(flightSpeedEntry.Value.ToString("F1"), emptyOptions), out var result) && result != flightSpeedEntry.Value)
		{
			flightSpeedEntry.Value = result;
			noClip.UpdateFlightSpeed(result);
		}
		GUILayout.Space(2f);
		GUILayout.Label($"Speed: {flightSpeedEntry.Value:F1}", emptyOptions);
		result = GUILayout.HorizontalSlider(flightSpeedEntry.Value, 5f, 50f, emptyOptions);
		if (result != flightSpeedEntry.Value)
		{
			flightSpeedEntry.Value = result;
			noClip.UpdateFlightSpeed(result);
		}
		GUILayout.Space(5f);
		VRCPlayerApi localPlayer = Networking.LocalPlayer;
		if (localPlayer != null)
		{
			windowRect.height = 315f;
			GUILayout.Label($"Movement Speed: {currentMovementSpeed:F1}", emptyOptions);
			currentMovementSpeed = GUILayout.HorizontalSlider(currentMovementSpeed, 0f, 10f, emptyOptions);
			localPlayer.SetWalkSpeed(currentMovementSpeed);
			localPlayer.SetRunSpeed(currentMovementSpeed * 2f);
			localPlayer.SetStrafeSpeed(currentMovementSpeed);
			GUILayout.Space(2f);
			GUILayout.Label($"Jump Impulse: {currentJumpImpulse:F1}", emptyOptions);
			currentJumpImpulse = GUILayout.HorizontalSlider(currentJumpImpulse, 0f, 10f, emptyOptions);
			localPlayer.SetJumpImpulse(currentJumpImpulse);
			GUILayout.Space(2f);
			GUILayout.Label($"Gravity Strength: {currentGravityStrength:F1}", emptyOptions);
			float num = GUILayout.HorizontalSlider(currentGravityStrength, 0f, 2f, emptyOptions);
			if (num != currentGravityStrength)
			{
				currentGravityStrength = num;
				if (!noClip.IsFlightEnabled)
				{
					localPlayer.SetGravityStrength(currentGravityStrength);
				}
			}
		}
		else
		{
			windowRect.height = 300f;
			GUILayout.Label("No local player found", emptyOptions);
		}
	}

	private void DrawUdonPage()
	{
		GUILayout.Label("Players:", emptyOptions);
		if (Time.time - lastPlayerListUpdate > 0.5f)
		{
			Il2CppSystem.Collections.Generic.List<VRCPlayerApi> allPlayers = VRCPlayerApi.AllPlayers;
			if (allPlayers != null)
			{
				cachedPlayers = new Il2CppReferenceArray<VRCPlayerApi>(allPlayers.Count);
				for (int i = 0; i < allPlayers.Count; i++)
				{
					cachedPlayers[i] = allPlayers[i];
				}
			}
			lastPlayerListUpdate = Time.time;
		}
		if (cachedPlayers != null)
		{
			playerListScrollPosition = GUILayout.BeginScrollView(playerListScrollPosition, emptyOptions);
			StringBuilder stringBuilder = new StringBuilder();
			foreach (VRCPlayerApi cachedPlayer in cachedPlayers)
			{
				if (cachedPlayer != null && cachedPlayer.IsValid())
				{
					stringBuilder.Clear();
					stringBuilder.Append(cachedPlayer.displayName);
					stringBuilder.Append(" [ID: ");
					stringBuilder.Append(VRCPlayerApi.GetPlayerId(cachedPlayer));
					stringBuilder.Append("]");
					if (GUILayout.Button(stringBuilder.ToString(), emptyOptions))
					{
						selectedPlayer = cachedPlayer;
						showPlayerOptions = true;
						pickupManager.SetSelectedPlayer(cachedPlayer);
					}
				}
			}
			GUILayout.EndScrollView();
		}
		else
		{
			GUILayout.Label("No players found", emptyOptions);
		}
		GUILayout.Space(10f);
		if (showPlayerOptions && selectedPlayer != null)
		{
			GUILayout.Label("Selected: " + selectedPlayer.displayName, emptyOptions);
			GUILayout.BeginHorizontal(emptyOptions);
			GUILayout.BeginVertical(emptyOptions);
			if (GUILayout.Button("Teleport to Player", emptyOptions))
			{
				Vector3 position = selectedPlayer.GetPosition();
				Networking.LocalPlayer?.TeleportTo(position, Quaternion.identity);
			}
			if (GUILayout.Button("Copy Player ID", emptyOptions))
			{
				GUIUtility.systemCopyBuffer = VRCPlayerApi.GetPlayerId(selectedPlayer).ToString();
				MelonLogger.Msg($"Copied Player ID: {VRCPlayerApi.GetPlayerId(selectedPlayer)}");
			}
			if (GUILayout.Button(isOrbiterActive ? "Stop Item Orbiter" : "Item Orbiter", emptyOptions))
			{
				MelonLogger.Msg(isOrbiterActive ? "Toggling Item Orbiter Off..." : "Starting Item Orbiter...");
				pickupManager.ActivateItemOrbiter();
				isOrbiterActive = !isOrbiterActive;
			}
			GUILayout.EndVertical();
			GUILayout.BeginVertical(emptyOptions);
			if (GUILayout.Button("Close Options", emptyOptions))
			{
				showPlayerOptions = false;
				selectedPlayer = null;
			}
			if (GUILayout.Button(isNukeActive ? "Stop Item Nuke" : "Item Nuke", emptyOptions))
			{
				MelonLogger.Msg(isNukeActive ? "Toggling Item Nuke Off..." : "Starting Item Nuke...");
				pickupManager.ActivateItemNuke();
				isNukeActive = !isNukeActive;
			}
			if (GUILayout.Button("Udon Nuke", emptyOptions))
			{
				UdonManager.TriggerAllEvents();
			}
			GUILayout.EndVertical();
			GUILayout.EndHorizontal();
		}
	}

	private void DrawMiscPage()
	{
		bool flag = GUILayout.Toggle(showFpsCounter, "Show FPS Counter", emptyOptions);
		if (flag != showFpsCounter)
		{
			showFpsCounter = flag;
		}
		GUILayout.Space(10f);
		if (GUILayout.Button("Anti-Pickups", emptyOptions))
		{
			pickupManager.ToggleAntiPickups();
		}
		GUILayout.Space(5f);
		if (GUILayout.Button(pickupSettingsVisible ? "Close Pickup Settings" : "Pickup Settings", emptyOptions))
		{
			pickupSettingsVisible = !pickupSettingsVisible;
		}
		if (GUILayout.Button(avatarSettingsVisible ? "Close Avatar Settings" : "Avatar Settings", emptyOptions))
		{
			avatarSettingsVisible = !avatarSettingsVisible;
		}
		if (GUILayout.Button(espSettingsVisible ? "Close ESP Settings" : "ESP Settings", emptyOptions))
		{
			espSettingsVisible = !espSettingsVisible;
		}
	}

	private void DrawPickupSettingsWindow(int windowID)
	{
		try
		{
			GUILayout.BeginVertical(emptyOptions);
			GUILayout.Label("Orbit Settings", emptyOptions);
			pickupManager.OrbitRadius = GUILayout.HorizontalSlider(pickupManager.OrbitRadius, 1f, 10f, emptyOptions);
			GUILayout.Label($"Orbit Radius: {pickupManager.OrbitRadius:F1}", emptyOptions);
			pickupManager.OrbitSpeed = GUILayout.HorizontalSlider(pickupManager.OrbitSpeed, 10f, 100f, emptyOptions);
			GUILayout.Label($"Orbit Speed: {pickupManager.OrbitSpeed:F1}", emptyOptions);
			pickupManager.VerticalMultiplier = GUILayout.HorizontalSlider(pickupManager.VerticalMultiplier, 0.5f, 3f, emptyOptions);
			GUILayout.Label($"Vertical Multiplier: {pickupManager.VerticalMultiplier:F1}", emptyOptions);
			GUILayout.Space(10f);
			GUILayout.Label("Nuke Settings", emptyOptions);
			pickupManager.NukeHeight = GUILayout.HorizontalSlider(pickupManager.NukeHeight, 1f, 5f, emptyOptions);
			GUILayout.Label($"Nuke Height: {pickupManager.NukeHeight:F1}", emptyOptions);
			pickupManager.NukeForce = GUILayout.HorizontalSlider(pickupManager.NukeForce, 5f, 20f, emptyOptions);
			GUILayout.Label($"Nuke Force: {pickupManager.NukeForce:F1}", emptyOptions);
			pickupManager.NukeDuration = GUILayout.HorizontalSlider(pickupManager.NukeDuration, 1f, 10f, emptyOptions);
			GUILayout.Label($"Nuke Duration: {pickupManager.NukeDuration:F1}", emptyOptions);
			GUILayout.Space(10f);
			GUILayout.Label("General Settings", emptyOptions);
			pickupManager.SettleTime = GUILayout.HorizontalSlider(pickupManager.SettleTime, 0.1f, 1f, emptyOptions);
			GUILayout.Label($"Settle Time: {pickupManager.SettleTime:F1}", emptyOptions);
			pickupManager.MinPickupSize = GUILayout.HorizontalSlider(pickupManager.MinPickupSize, 0.01f, 0.5f, emptyOptions);
			GUILayout.Label($"Min Pickup Size: {pickupManager.MinPickupSize:F2}", emptyOptions);
			pickupManager.MaxPickupSize = GUILayout.HorizontalSlider(pickupManager.MaxPickupSize, 1f, 5f, emptyOptions);
			GUILayout.Label($"Max Pickup Size: {pickupManager.MaxPickupSize:F1}", emptyOptions);
			GUILayout.EndVertical();
			GUI.DragWindow();
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error in DrawPickupSettingsWindow: " + ex.Message);
		}
	}

	private void DrawAvatarSettingsWindow(int windowID)
	{
		try
		{
			GUILayout.BeginVertical(emptyOptions);
			bool flag = GUILayout.Toggle(antiAvatars, "Anti-Avatars", emptyOptions);
			if (flag != antiAvatars)
			{
				antiAvatars = flag;
				UserManager.Instance.SetAntiAvatars(antiAvatars);
			}
			GUILayout.Space(10f);
			GUILayout.Label($"Avatar Size: {avatarScale:F2}x", emptyOptions);
			float num = GUILayout.HorizontalSlider(avatarScale, 0.1f, 5f, emptyOptions);
			if (num != avatarScale)
			{
				avatarScale = num;
				UserManager.Instance.SetAllAvatarScales(avatarScale);
			}
			GUILayout.Space(10f);
			bool flag2 = GUILayout.Toggle(UserManager.Instance.GetSettings().GetValueOrDefault("NameplateSeeThrough", defaultValue: false), "Nameplates See-Through", emptyOptions);
			if (flag2 != UserManager.Instance.GetSettings().GetValueOrDefault("NameplateSeeThrough", defaultValue: false))
			{
				UserManager.Instance.SetNameplatesSeeThrough(flag2);
			}
			GUILayout.EndVertical();
			GUI.DragWindow();
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error in DrawAvatarSettingsWindow: " + ex.Message);
		}
	}

	private void DrawESPSettingsWindow(int windowID)
	{
		try
		{
			GUILayout.BeginVertical(emptyOptions);
			bool flag = GUILayout.Toggle(showESP, "Enable ESP", emptyOptions);
			if (flag != showESP)
			{
				showESP = flag;
				ESPManager.Instance.UpdateSettings(showESP, showBoxESP, showNameESP, showDistanceESP, espDistance, espColor, espLineWidth);
				UserManager.Instance.SetNameplatesSeeThrough(showESP);
			}
			GUILayout.Space(5f);
			bool flag2 = GUILayout.Toggle(showBoxESP, "Show Box ESP", emptyOptions);
			if (flag2 != showBoxESP)
			{
				showBoxESP = flag2;
				ESPManager.Instance.UpdateSettings(showESP, showBoxESP, showNameESP, showDistanceESP, espDistance, espColor, espLineWidth);
			}
			bool flag3 = GUILayout.Toggle(showNameESP, "Show Name ESP", emptyOptions);
			if (flag3 != showNameESP)
			{
				showNameESP = flag3;
				ESPManager.Instance.UpdateSettings(showESP, showBoxESP, showNameESP, showDistanceESP, espDistance, espColor, espLineWidth);
				UserManager.Instance.SetNameplatesVisible(showNameESP);
			}
			bool flag4 = GUILayout.Toggle(showDistanceESP, "Show Distance ESP", emptyOptions);
			if (flag4 != showDistanceESP)
			{
				showDistanceESP = flag4;
				ESPManager.Instance.UpdateSettings(showESP, showBoxESP, showNameESP, showDistanceESP, espDistance, espColor, espLineWidth);
				UserManager.Instance.SetDistanceVisible(showDistanceESP);
			}
			GUILayout.Space(5f);
			GUILayout.Label($"ESP Distance: {espDistance:F1}", emptyOptions);
			float num = GUILayout.HorizontalSlider(espDistance, 1f, 100f, emptyOptions);
			if (num != espDistance)
			{
				espDistance = num;
				ESPManager.Instance.UpdateSettings(showESP, showBoxESP, showNameESP, showDistanceESP, espDistance, espColor, espLineWidth);
			}
			GUILayout.Label($"ESP Line Width: {espLineWidth:F1}", emptyOptions);
			float num2 = GUILayout.HorizontalSlider(espLineWidth, 1f, 5f, emptyOptions);
			if (num2 != espLineWidth)
			{
				espLineWidth = num2;
				ESPManager.Instance.UpdateSettings(showESP, showBoxESP, showNameESP, showDistanceESP, espDistance, espColor, espLineWidth);
			}
			GUILayout.Space(5f);
			GUILayout.Label("ESP Color:", emptyOptions);
			GUILayout.BeginHorizontal(emptyOptions);
			GUILayout.Label("R:", GUI.skin.label, emptyOptions);
			float num3 = GUILayout.HorizontalSlider(espColor.r, 0f, 1f, emptyOptions);
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal(emptyOptions);
			GUILayout.Label("G:", GUI.skin.label, emptyOptions);
			float num4 = GUILayout.HorizontalSlider(espColor.g, 0f, 1f, emptyOptions);
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal(emptyOptions);
			GUILayout.Label("B:", GUI.skin.label, emptyOptions);
			float num5 = GUILayout.HorizontalSlider(espColor.b, 0f, 1f, emptyOptions);
			GUILayout.EndHorizontal();
			if (num3 != espColor.r || num4 != espColor.g || num5 != espColor.b)
			{
				espColor = new Color(num3, num4, num5);
				ESPManager.Instance.UpdateSettings(showESP, showBoxESP, showNameESP, showDistanceESP, espDistance, espColor, espLineWidth);
			}
			GUILayout.Space(5f);
			Rect rect = GUILayoutUtility.GetRect(50f, 20f, emptyOptions);
			GUI.color = espColor;
			GUI.Box(rect, "", GUI.skin.box);
			GUI.color = Color.white;
			GUILayout.Space(5f);
			GUILayout.BeginHorizontal(emptyOptions);
			if (GUILayout.Button("Red", emptyOptions))
			{
				espColor = Color.red;
				ESPManager.Instance.UpdateSettings(showESP, showBoxESP, showNameESP, showDistanceESP, espDistance, espColor, espLineWidth);
			}
			if (GUILayout.Button("Green", emptyOptions))
			{
				espColor = Color.green;
				ESPManager.Instance.UpdateSettings(showESP, showBoxESP, showNameESP, showDistanceESP, espDistance, espColor, espLineWidth);
			}
			if (GUILayout.Button("Blue", emptyOptions))
			{
				espColor = Color.blue;
				ESPManager.Instance.UpdateSettings(showESP, showBoxESP, showNameESP, showDistanceESP, espDistance, espColor, espLineWidth);
			}
			GUILayout.EndHorizontal();
			GUILayout.BeginHorizontal(emptyOptions);
			if (GUILayout.Button("Yellow", emptyOptions))
			{
				espColor = Color.yellow;
				ESPManager.Instance.UpdateSettings(showESP, showBoxESP, showNameESP, showDistanceESP, espDistance, espColor, espLineWidth);
			}
			if (GUILayout.Button("Cyan", emptyOptions))
			{
				espColor = Color.cyan;
				ESPManager.Instance.UpdateSettings(showESP, showBoxESP, showNameESP, showDistanceESP, espDistance, espColor, espLineWidth);
			}
			if (GUILayout.Button("Magenta", emptyOptions))
			{
				espColor = Color.magenta;
				ESPManager.Instance.UpdateSettings(showESP, showBoxESP, showNameESP, showDistanceESP, espDistance, espColor, espLineWidth);
			}
			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
			GUI.DragWindow();
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error in DrawESPSettingsWindow: " + ex.Message);
		}
	}

	public void ToggleWindow()
	{
		windowVisible = !windowVisible;
		if (!windowVisible)
		{
			pickupSettingsVisible = false;
			avatarSettingsVisible = false;
			espSettingsVisible = false;
		}
	}

	public KeyCode GetFlightToggleKey()
	{
		return flightToggleKeyEntry.Value;
	}

	public KeyCode GetAscendKey()
	{
		return ascendKeyEntry.Value;
	}

	public KeyCode GetDescendKey()
	{
		return descendKeyEntry.Value;
	}

	public KeyCode GetForwardKey()
	{
		return forwardKeyEntry.Value;
	}

	public KeyCode GetBackwardKey()
	{
		return backwardKeyEntry.Value;
	}

	public KeyCode GetLeftKey()
	{
		return leftKeyEntry.Value;
	}

	public KeyCode GetRightKey()
	{
		return rightKeyEntry.Value;
	}

	public Color GetIndicatorColor()
	{
		return indicatorColorEntry.Value;
	}

	public bool GetShowIndicator()
	{
		return showIndicatorEntry.Value;
	}

	public float GetFlightSpeed()
	{
		return flightSpeedEntry.Value;
	}

	private void ToggleAntiAvatars(bool enabled)
	{
		try
		{
			MelonLogger.Msg("Anti-Avatars: " + (enabled ? "Enabled" : "Disabled"));
			UserManager.Instance.SetAntiAvatars(enabled);
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error toggling anti-avatars: " + ex.Message);
		}
	}

	private void SetAvatarScale(float scale)
	{
		try
		{
			UserManager.Instance.SetAllAvatarScales(scale);
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error setting avatar scale: " + ex.Message);
		}
	}
}
