using System;
using System.Collections;
using System.Collections.Generic;
using MelonLoader;
using UnhollowerBaseLib;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

namespace NoClipMod;

public class PickupManager
{
	private object activeOrbiterCoroutine;

	private object activeNukeCoroutine;

	private bool isOrbiterActive;

	private bool isNukeActive;

	private bool isAntiPickupsActive;

	private VRCPlayerApi selectedPlayer;

	private List<GameObject> disabledPickups = new List<GameObject>();

	private const float DEG_TO_RAD = (float)Math.PI / 180f;

	public float OrbitRadius { get; set; } = 2f;

	public float OrbitSpeed { get; set; } = 50f;

	public float VerticalMultiplier { get; set; } = 1.5f;

	public float NukeHeight { get; set; } = 2f;

	public float NukeForce { get; set; } = 10f;

	public float NukeDuration { get; set; } = 3f;

	public float SettleTime { get; set; } = 0.5f;

	public float MinPickupSize { get; set; } = 0.1f;

	public float MaxPickupSize { get; set; } = 2f;

	public void SetSelectedPlayer(VRCPlayerApi player)
	{
		selectedPlayer = player;
	}

	public void ActivateItemOrbiter()
	{
		try
		{
			if (isOrbiterActive)
			{
				StopOrbiter();
				return;
			}
			List<VRCPickup> list = FindAllPickups();
			if (list.Count == 0)
			{
				MelonLogger.Warning("No pickups found to orbit");
				return;
			}
			foreach (VRCPickup item in list)
			{
				if (!(item == null))
				{
					Networking.SetOwner(Networking.LocalPlayer, item.gameObject);
				}
			}
			isOrbiterActive = true;
			activeOrbiterCoroutine = MelonCoroutines.Start(OrbitItems(list));
			MelonLogger.Msg("Item Orbiter activated");
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error in ActivateItemOrbiter: " + ex.Message);
		}
	}

	public void ActivateItemNuke()
	{
		try
		{
			if (isNukeActive)
			{
				StopNuke();
				return;
			}
			List<VRCPickup> list = FindAllPickups();
			if (list.Count == 0)
			{
				MelonLogger.Warning("No pickups found to nuke");
				return;
			}
			foreach (VRCPickup item in list)
			{
				if (!(item == null))
				{
					Networking.SetOwner(Networking.LocalPlayer, item.gameObject);
				}
			}
			isNukeActive = true;
			activeNukeCoroutine = MelonCoroutines.Start(NukeItems(list));
			MelonLogger.Msg("Item Nuke activated");
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error in ActivateItemNuke: " + ex.Message);
		}
	}

	public void ToggleAntiPickups()
	{
		try
		{
			if (isAntiPickupsActive)
			{
				RestorePickups();
				isAntiPickupsActive = false;
				MelonLogger.Msg("Anti-Pickups disabled");
				return;
			}
			foreach (VRCPickup item in FindAllPickups())
			{
				if (item == null)
				{
					continue;
				}
				try
				{
					item.enabled = false;
					MeshRenderer component = item.GetComponent<MeshRenderer>();
					if (component != null)
					{
						component.enabled = false;
					}
					item.gameObject.SetActive(value: false);
					disabledPickups.Add(item.gameObject);
				}
				catch (Exception ex)
				{
					MelonLogger.Error("Error disabling pickup: " + ex.Message);
				}
			}
			isAntiPickupsActive = true;
			MelonLogger.Msg($"Anti-Pickups enabled: {disabledPickups.Count} pickups disabled");
		}
		catch (Exception ex2)
		{
			MelonLogger.Error("Error in ToggleAntiPickups: " + ex2.Message);
		}
	}

	private void RestorePickups()
	{
		foreach (GameObject disabledPickup in disabledPickups)
		{
			if (disabledPickup == null)
			{
				continue;
			}
			try
			{
				VRCPickup component = disabledPickup.GetComponent<VRCPickup>();
				if (component != null)
				{
					component.enabled = true;
				}
				MeshRenderer component2 = disabledPickup.GetComponent<MeshRenderer>();
				if (component2 != null)
				{
					component2.enabled = true;
				}
				disabledPickup.SetActive(value: true);
			}
			catch (Exception ex)
			{
				MelonLogger.Error("Error restoring pickup: " + ex.Message);
			}
		}
		disabledPickups.Clear();
	}

	private void StopOrbiter()
	{
		if (activeOrbiterCoroutine != null)
		{
			MelonCoroutines.Stop(activeOrbiterCoroutine);
			activeOrbiterCoroutine = null;
		}
		isOrbiterActive = false;
		MelonLogger.Msg("Item Orbiter stopped");
	}

	private void StopNuke()
	{
		if (activeNukeCoroutine != null)
		{
			MelonCoroutines.Stop(activeNukeCoroutine);
			activeNukeCoroutine = null;
		}
		isNukeActive = false;
		MelonLogger.Msg("Item Nuke stopped");
	}

	private IEnumerator OrbitItems(List<VRCPickup> pickups)
	{
		if (selectedPlayer == null || !selectedPlayer.IsValid())
		{
			StopOrbiter();
			yield break;
		}
		Dictionary<VRCPickup, Rigidbody> rigidbodies = new Dictionary<VRCPickup, Rigidbody>();
		foreach (VRCPickup pickup in pickups)
		{
			if (!(pickup == null))
			{
				Rigidbody component = pickup.GetComponent<Rigidbody>();
				if (component != null)
				{
					rigidbodies[pickup] = component;
				}
			}
		}
		foreach (VRCPickup pickup2 in pickups)
		{
			if (pickup2 == null)
			{
				continue;
			}
			try
			{
				Vector3 position = selectedPlayer.GetPosition();
				float num = selectedPlayer.GetBonePosition(HumanBodyBones.Head).y - position.y;
				Vector3 vector = new Vector3(OrbitRadius, num * VerticalMultiplier, 0f);
				pickup2.transform.position = position + vector;
				pickup2.transform.rotation = Quaternion.identity;
				if (rigidbodies.TryGetValue(pickup2, out var value))
				{
					value.velocity = Vector3.zero;
					value.angularVelocity = Vector3.zero;
				}
			}
			catch (Exception ex)
			{
				MelonLogger.Error("Error setting up pickup for orbit: " + ex.Message);
			}
		}
		yield return new WaitForSeconds(SettleTime);
		float angle = 0f;
		int frameCount = 0;
		float[] angleOffsets = new float[pickups.Count];
		for (int i = 0; i < pickups.Count; i++)
		{
			angleOffsets[i] = 360f * (float)i / (float)pickups.Count;
		}
		while (isOrbiterActive)
		{
			if (selectedPlayer == null || !selectedPlayer.IsValid())
			{
				StopOrbiter();
				yield break;
			}
			try
			{
				if (frameCount % 2 == 0)
				{
					Vector3 position2 = selectedPlayer.GetPosition();
					float num2 = selectedPlayer.GetBonePosition(HumanBodyBones.Head).y - position2.y;
					Vector3 vector2 = position2 + Vector3.up * (num2 * VerticalMultiplier);
					for (int j = 0; j < pickups.Count; j++)
					{
						VRCPickup vRCPickup = pickups[j];
						if (!(vRCPickup == null))
						{
							try
							{
								float f = (angle + angleOffsets[j]) * ((float)Math.PI / 180f);
								Vector3 vector3 = new Vector3(Mathf.Cos(f) * OrbitRadius, 0f, Mathf.Sin(f) * OrbitRadius);
								vRCPickup.transform.position = vector2 + vector3;
							}
							catch (Exception ex2)
							{
								MelonLogger.Error("Error orbiting pickup: " + ex2.Message);
							}
						}
					}
					angle += OrbitSpeed * Time.deltaTime * 2f;
					if (angle >= 360f)
					{
						angle -= 360f;
					}
				}
				frameCount++;
			}
			catch (Exception ex3)
			{
				MelonLogger.Error("Error in OrbitItems: " + ex3.Message);
			}
			yield return null;
		}
		foreach (Rigidbody value2 in rigidbodies.Values)
		{
			if (value2 != null)
			{
				value2.velocity = Vector3.zero;
				value2.angularVelocity = Vector3.zero;
			}
		}
	}

	private IEnumerator NukeItems(List<VRCPickup> pickups)
	{
		if (selectedPlayer == null || !selectedPlayer.IsValid())
		{
			StopNuke();
			yield break;
		}
		foreach (VRCPickup pickup in pickups)
		{
			if (pickup == null)
			{
				continue;
			}
			try
			{
				Vector3 position = selectedPlayer.GetPosition();
				Vector3 vector = new Vector3(0f, NukeHeight, 0f);
				pickup.transform.position = position + vector;
				pickup.transform.rotation = Quaternion.identity;
				Rigidbody component = pickup.GetComponent<Rigidbody>();
				if (component != null)
				{
					component.velocity = Vector3.zero;
					component.angularVelocity = Vector3.zero;
				}
			}
			catch (Exception ex)
			{
				MelonLogger.Error("Error setting up pickup for nuke: " + ex.Message);
			}
		}
		yield return new WaitForSeconds(SettleTime);
		float elapsed = 0f;
		while (elapsed < NukeDuration && isNukeActive)
		{
			if (selectedPlayer == null || !selectedPlayer.IsValid())
			{
				StopNuke();
				yield break;
			}
			try
			{
				Vector3 vector2 = selectedPlayer.GetPosition() + Vector3.up * NukeHeight;
				foreach (VRCPickup pickup2 in pickups)
				{
					if (pickup2 == null)
					{
						continue;
					}
					try
					{
						Vector3 normalized = (pickup2.transform.position - vector2).normalized;
						Rigidbody component2 = pickup2.GetComponent<Rigidbody>();
						if (component2 != null)
						{
							component2.AddForce(normalized * NukeForce, ForceMode.Impulse);
						}
					}
					catch (Exception ex2)
					{
						MelonLogger.Error("Error nuking pickup: " + ex2.Message);
					}
				}
				elapsed += Time.deltaTime;
			}
			catch (Exception ex3)
			{
				MelonLogger.Error("Error in NukeItems: " + ex3.Message);
			}
			yield return null;
		}
		RestorePickupPhysics(pickups);
	}

	private void RestorePickupPhysics(List<VRCPickup> pickups)
	{
		foreach (VRCPickup pickup in pickups)
		{
			if (pickup == null)
			{
				continue;
			}
			try
			{
				Rigidbody component = pickup.GetComponent<Rigidbody>();
				if (component != null)
				{
					component.velocity = Vector3.zero;
					component.angularVelocity = Vector3.zero;
				}
			}
			catch (Exception ex)
			{
				MelonLogger.Error("Error restoring pickup physics: " + ex.Message);
			}
		}
	}

	private List<VRCPickup> FindAllPickups()
	{
		List<VRCPickup> list = new List<VRCPickup>();
		try
		{
			Il2CppArrayBase<VRCPickup> il2CppArrayBase = UnityEngine.Object.FindObjectsOfType<VRCPickup>();
			if (il2CppArrayBase != null && il2CppArrayBase.Length > 0)
			{
				foreach (VRCPickup item in il2CppArrayBase)
				{
					if (!(item == null))
					{
						list.Add(item);
					}
				}
				MelonLogger.Msg($"Found {il2CppArrayBase.Length} VRCPickup items");
			}
			Il2CppArrayBase<Rigidbody> il2CppArrayBase2 = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
			int num = 0;
			foreach (Rigidbody item2 in il2CppArrayBase2)
			{
				if (item2 == null || item2.GetComponent<VRCPickup>() != null || item2.gameObject.layer == LayerMask.NameToLayer("Player") || item2.gameObject.layer == LayerMask.NameToLayer("PlayerLocal"))
				{
					continue;
				}
				Collider component = item2.GetComponent<Collider>();
				if (!(component != null))
				{
					continue;
				}
				Bounds bounds = component.bounds;
				float num2 = (bounds.size.x + bounds.size.y + bounds.size.z) / 3f;
				if (!(num2 < MinPickupSize) && !(num2 > MaxPickupSize))
				{
					VRCPickup vRCPickup = item2.gameObject.AddComponent<VRCPickup>();
					if (vRCPickup != null)
					{
						list.Add(vRCPickup);
						num++;
					}
				}
			}
			MelonLogger.Msg($"Found {num} additional Rigidbody objects as pickups");
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error in FindAllPickups: " + ex.Message);
		}
		return list;
	}
}
