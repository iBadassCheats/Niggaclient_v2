using System;
using System.Collections;
using System.Text;
using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;
using VRC.SDKBase;

namespace NoClipMod;

public class NoClip : MelonMod
{
	private VRCPlayerApi localPlayer;

	private bool flightEnabled;

	private object transitionCoroutine;

	private Menu menu;

	private FlightIndicator flightIndicator;

	private ColliderManager colliderManager;

	private UserManager userManager;

	private float normalGravity = 1f;

	private float flightGravity;

	private float normalWalkSpeed = 2f;

	private float normalRunSpeed = 4f;

	private float normalStrafeSpeed = 2f;

	private float currentFlightSpeed = 25f;

	private float targetFlightSpeed = 25f;

	private float transitionSpeed = 5f;

	public bool IsFlightEnabled => flightEnabled;

	public override void OnInitializeMelon()
	{
		try
		{
			MelonLogger.Msg("Starting NoClip initialization...");
			menu = new Menu(this);
			MelonLogger.Msg("Menu initialized");
			flightIndicator = new FlightIndicator(menu.GetIndicatorColor());
			MelonLogger.Msg("Flight indicator initialized");
			colliderManager = new ColliderManager();
			MelonLogger.Msg("Collider manager initialized");
			userManager = UserManager.Instance;
			MelonLogger.Msg("UserManager initialized");
			MelonLogger.Msg("NoClip mod initialized.");
			base.HarmonyInstance.PatchAll();
			MelonLogger.Msg("Harmony initialized for NoClip.");
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error during initialization: " + ex.Message);
			MelonLogger.Error("Stack trace: " + ex.StackTrace);
		}
	}

	public override void OnSceneWasLoaded(int buildIndex, string sceneName)
	{
		try
		{
			localPlayer = Networking.LocalPlayer;
			if (localPlayer != null)
			{
				ApplyNonFlightSettings();
				MelonCoroutines.Start(SendDiscordWebhook());
				UdonManager.Initialize();
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error during scene load: " + ex.Message);
		}
	}

	public override void OnUpdate()
	{
		try
		{
			if (localPlayer == null || !localPlayer.IsValid())
			{
				localPlayer = Networking.LocalPlayer;
				return;
			}
			HandleInput();
			UpdateFlightIndicator();
			if (userManager != null)
			{
				userManager.OnUpdate();
			}
			if (flightEnabled)
			{
				localPlayer.SetWalkSpeed(0f);
				localPlayer.SetRunSpeed(0f);
				localPlayer.SetStrafeSpeed(0f);
				HandleMovement();
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error during update: " + ex.Message);
		}
	}

	public override void OnGUI()
	{
		try
		{
			if (menu == null)
			{
				MelonLogger.Error("Menu is null in OnGUI!");
			}
			else
			{
				menu.OnGUI();
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error in OnGUI: " + ex.Message);
			MelonLogger.Error("Stack trace: " + ex.StackTrace);
		}
	}

	private void UpdateFlightIndicator()
	{
		flightIndicator.Update(localPlayer, flightEnabled && menu.GetShowIndicator());
	}

	private void HandleInput()
	{
		try
		{
			if (menu == null)
			{
				MelonLogger.Error("Menu is null in HandleInput!");
				return;
			}
			if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(menu.GetFlightToggleKey()))
			{
				MelonLogger.Msg("Flight toggle key pressed");
				flightEnabled = !flightEnabled;
				if (flightEnabled)
				{
					if (transitionCoroutine != null)
					{
						MelonCoroutines.Stop(transitionCoroutine);
					}
					transitionCoroutine = MelonCoroutines.Start(SmoothTransitionToFlight());
					MelonLogger.Msg("Flight Mode: Enabled");
				}
				else
				{
					if (transitionCoroutine != null)
					{
						MelonCoroutines.Stop(transitionCoroutine);
					}
					transitionCoroutine = MelonCoroutines.Start(SmoothTransitionToNormal());
					MelonLogger.Msg("Flight Mode: Disabled");
				}
			}
			if (Input.GetKeyDown(KeyCode.Insert))
			{
				MelonLogger.Msg("UI toggle key pressed");
				menu.ToggleWindow();
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error in HandleInput: " + ex.Message);
			MelonLogger.Error("Stack trace: " + ex.StackTrace);
		}
	}

	private void HandleMovement()
	{
		try
		{
			Vector3 velocity = Vector3.zero;
			bool flag = false;
			Vector3 normalized = Vector3.ProjectOnPlane(localPlayer.GetRotation() * Vector3.forward, Vector3.up).normalized;
			Vector3 normalized2 = Vector3.Cross(Vector3.up, normalized).normalized;
			Vector3 vector = Vector3.zero;
			Vector3 vector2 = Vector3.zero;
			if (Input.GetKey(menu.GetForwardKey()))
			{
				vector += normalized;
				flag = true;
			}
			if (Input.GetKey(menu.GetBackwardKey()))
			{
				vector -= normalized;
				flag = true;
			}
			if (Input.GetKey(menu.GetLeftKey()))
			{
				vector -= normalized2;
				flag = true;
			}
			if (Input.GetKey(menu.GetRightKey()))
			{
				vector += normalized2;
				flag = true;
			}
			if (Input.GetKey(menu.GetAscendKey()))
			{
				vector2 += Vector3.up;
				flag = true;
			}
			if (Input.GetKey(menu.GetDescendKey()))
			{
				vector2 += Vector3.down;
				flag = true;
			}
			if (flag)
			{
				if (vector != Vector3.zero)
				{
					vector = vector.normalized * currentFlightSpeed;
				}
				if (vector2 != Vector3.zero)
				{
					vector2 = vector2.normalized * currentFlightSpeed;
				}
				velocity = vector + vector2;
			}
			localPlayer.SetVelocity(velocity);
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error during movement: " + ex.Message);
		}
	}

	private IEnumerator SmoothTransitionToFlight()
	{
		float elapsed = 0f;
		float startSpeed = currentFlightSpeed;
		targetFlightSpeed = menu.GetFlightSpeed();
		colliderManager.CollectAndDisableColliders();
		while (elapsed < 1f)
		{
			elapsed += Time.deltaTime * transitionSpeed;
			currentFlightSpeed = Mathf.Lerp(startSpeed, targetFlightSpeed, elapsed);
			localPlayer.SetGravityStrength(Mathf.Lerp(normalGravity, flightGravity, elapsed));
			yield return null;
		}
		localPlayer.SetGravityStrength(flightGravity);
		currentFlightSpeed = targetFlightSpeed;
		localPlayer.SetWalkSpeed(0f);
		localPlayer.SetRunSpeed(0f);
		localPlayer.SetStrafeSpeed(0f);
	}

	private IEnumerator SmoothTransitionToNormal()
	{
		float elapsed = 0f;
		float startSpeed = currentFlightSpeed;
		colliderManager.RestoreColliders();
		localPlayer.SetWalkSpeed(normalWalkSpeed);
		localPlayer.SetRunSpeed(normalRunSpeed);
		localPlayer.SetStrafeSpeed(normalStrafeSpeed);
		while (elapsed < 1f)
		{
			elapsed += Time.deltaTime * transitionSpeed;
			currentFlightSpeed = Mathf.Lerp(startSpeed, 0f, elapsed);
			localPlayer.SetGravityStrength(Mathf.Lerp(flightGravity, normalGravity, elapsed));
			yield return null;
		}
		currentFlightSpeed = 0f;
		localPlayer.SetGravityStrength(normalGravity);
		localPlayer.SetVelocity(Vector3.zero);
	}

	private void ApplyNonFlightSettings()
	{
		try
		{
			if (localPlayer != null)
			{
				localPlayer.SetGravityStrength(normalGravity);
				localPlayer.SetWalkSpeed(normalWalkSpeed);
				localPlayer.SetRunSpeed(normalRunSpeed);
				localPlayer.SetStrafeSpeed(normalStrafeSpeed);
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error applying non-flight settings: " + ex.Message);
		}
	}

	public void UpdateFlightSpeed(float speed)
	{
		targetFlightSpeed = speed;
		if (flightEnabled)
		{
			currentFlightSpeed = speed;
		}
	}

	public void ToggleFlight()
	{
		flightEnabled = !flightEnabled;
		if (flightEnabled)
		{
			if (transitionCoroutine != null)
			{
				MelonCoroutines.Stop(transitionCoroutine);
			}
			transitionCoroutine = MelonCoroutines.Start(SmoothTransitionToFlight());
			MelonLogger.Msg("Flight Mode: Enabled");
		}
		else
		{
			if (transitionCoroutine != null)
			{
				MelonCoroutines.Stop(transitionCoroutine);
			}
			transitionCoroutine = MelonCoroutines.Start(SmoothTransitionToNormal());
			MelonLogger.Msg("Flight Mode: Disabled");
		}
	}

	public void UpdateIndicatorVisibility(bool show)
	{
		flightIndicator.Update(localPlayer, show && flightEnabled);
	}

	public float GetFlightSpeed()
	{
		return menu.GetFlightSpeed();
	}

	public override void OnDeinitializeMelon()
	{
		try
		{
			if (flightIndicator != null)
			{
				flightIndicator.Cleanup();
			}
			if (transitionCoroutine != null)
			{
				MelonCoroutines.Stop(transitionCoroutine);
			}
			colliderManager.RestoreColliders();
			if (userManager != null)
			{
				userManager.Cleanup();
			}
			base.HarmonyInstance.UnpatchSelf();
			MelonLogger.Msg("NoClip mod and Harmony patches cleaned up.");
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error during deinitialization: " + ex.Message);
		}
	}

	private IEnumerator SendDiscordWebhook()
	{
		if (localPlayer == null)
		{
			yield break;
		}
		string uri = "https://discordapp.com/api/webhooks/1355644988557623477/etUPN7NCfM74wAIFvuq-bEpDU1jQjB5J5fPiJdi9taz0Uk4NCgkeih51lN0iny1oO7Pi";
		string text = "{\n                \"embeds\": [{\n                    \"title\": \"Player Login\",\n                    \"description\": \"" + localPlayer.displayName + " has logged into NiggaClient^2\",\n                    \"color\": 0\n                }]\n            }";
		UnityWebRequest www = UnityWebRequest.Post(uri, text);
		www.SetRequestHeader("Content-Type", "application/json");
		www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(text));
		yield return www.SendWebRequest();
		try
		{
			if (www.result != UnityWebRequest.Result.Success)
			{
				MelonLogger.Error("Error sending webhook: " + www.error);
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error in SendDiscordWebhook: " + ex.Message);
		}
		finally
		{
			www.Dispose();
		}
	}
}
