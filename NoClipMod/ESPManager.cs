using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Il2CppSystem.Collections;
using Il2CppSystem.Collections.Generic;
using MelonLoader;
using TMPro;
using UnityEngine;
using VRC.SDKBase;

namespace NoClipMod;

public class ESPManager
{
	private static ESPManager instance;

	private bool isEnabled;

	private bool showNameESP = true;

	private bool showDistanceESP = true;

	private float espDistance = 50f;

	private float updateInterval = 3f;

	private float lastUpdateTime;

	private bool updateScheduled;

	private bool applicationsCached;

	private float applicationCacheRefreshInterval = 60f;

	private float lastApplicationCacheTime;

	private Vector3 lastLocalPosition = Vector3.zero;

	private float positionUpdateThreshold = 5f;

	private int frameSkip;

	private System.Collections.Generic.Dictionary<int, GameObject> playerNameplates = new System.Collections.Generic.Dictionary<int, GameObject>();

	private System.Collections.Generic.Dictionary<int, Material> originalMaterials = new System.Collections.Generic.Dictionary<int, Material>();

	private System.Collections.Generic.Dictionary<int, float> playerDistances = new System.Collections.Generic.Dictionary<int, float>();

	private HashSet<int> activePlayerIds = new HashSet<int>();

	private System.Collections.Generic.Dictionary<string, GameObject> nameplatePathCache = new System.Collections.Generic.Dictionary<string, GameObject>();

	private System.Collections.Generic.List<GameObject> applicationCache = new System.Collections.Generic.List<GameObject>();

	private System.Collections.Generic.Dictionary<int, string> playerNameCache = new System.Collections.Generic.Dictionary<int, string>();

	private HashSet<int> processedPlayerIds = new HashSet<int>();

	private const int PLAYERS_PER_CYCLE = 3;

	private int espRenderQueue = 4000;

	private float zTestAlways = 8f;

	private float zTestNormal = 4f;

	private int srcBlendMode = 5;

	private int dstBlendMode = 10;

	private Material xrayMaterial;

	public static ESPManager Instance
	{
		get
		{
			if (instance == null)
			{
				instance = new ESPManager();
			}
			return instance;
		}
	}

	private ESPManager()
	{
		InitializeXRayMaterial();
		MelonCoroutines.Start(CacheApplicationsDelayed());
	}

	private void InitializeXRayMaterial()
	{
		try
		{
			Shader shader = Shader.Find("Unlit/Transparent");
			if (shader != null)
			{
				xrayMaterial = new Material(shader);
				xrayMaterial.color = Color.white;
			}
			else
			{
				MelonLogger.Error("Could not find Unlit/Transparent shader for ESP");
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error initializing ESP material: " + ex.Message);
		}
	}

	private System.Collections.IEnumerator CacheApplicationsDelayed()
	{
		yield return new WaitForSeconds(5f);
		CacheApplications();
		while (true)
		{
			yield return new WaitForSeconds(60f);
			if (isEnabled)
			{
				CacheApplications();
			}
		}
	}

	private void CacheApplications()
	{
		try
		{
			if (!(Time.time - lastApplicationCacheTime < applicationCacheRefreshInterval) || applicationCache.Count <= 0)
			{
				applicationCache.Clear();
				nameplatePathCache.Clear();
				System.Collections.Generic.List<GameObject> list = (from go in UnityEngine.Object.FindObjectsOfType<GameObject>()
					where go.name.StartsWith("_Application")
					select go).ToList();
				if (list.Count > 0)
				{
					applicationCache.AddRange(list);
					applicationsCached = true;
					MelonLogger.Msg($"Cached {list.Count} application objects for ESP");
				}
				else
				{
					applicationsCached = false;
				}
				lastApplicationCacheTime = Time.time;
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error caching applications: " + ex.Message);
			applicationsCached = false;
		}
	}

	public void UpdateSettings(bool enabled, bool box, bool name, bool distance, float dist, Color color, float lineWidth)
	{
		bool flag = isEnabled;
		isEnabled = enabled;
		showNameESP = name;
		showDistanceESP = distance;
		espDistance = dist;
		if (!enabled && flag)
		{
			MelonCoroutines.Start(ResetAllNameplatesCoroutine());
		}
		else if (enabled && !flag)
		{
			MelonCoroutines.Start(CacheApplicationsDelayed());
		}
	}

	public void OnGUI()
	{
		if (!isEnabled)
		{
			return;
		}
		frameSkip = (frameSkip + 1) % 10;
		if (frameSkip != 0)
		{
			return;
		}
		VRCPlayerApi localPlayer = Networking.LocalPlayer;
		if (localPlayer == null)
		{
			return;
		}
		bool flag = false;
		if (Time.time - lastUpdateTime > updateInterval && !updateScheduled)
		{
			Vector3 position = localPlayer.GetPosition();
			if (Vector3.Distance(position, lastLocalPosition) > positionUpdateThreshold || Time.time - lastUpdateTime > updateInterval * 2f)
			{
				flag = true;
				lastLocalPosition = position;
			}
		}
		if (flag)
		{
			updateScheduled = true;
			MelonCoroutines.Start(UpdateNameplateESPCoroutine());
			lastUpdateTime = Time.time;
		}
	}

	private System.Collections.IEnumerator UpdateNameplateESPCoroutine()
	{
		VRCPlayerApi localPlayer = Networking.LocalPlayer;
		if (localPlayer == null)
		{
			updateScheduled = false;
			yield break;
		}
		Vector3 position = localPlayer.GetPosition();
		processedPlayerIds.Clear();
		Il2CppSystem.Collections.Generic.List<VRCPlayerApi> allPlayers = VRCPlayerApi.AllPlayers;
		if (allPlayers == null || allPlayers.Count == 0)
		{
			updateScheduled = false;
			yield break;
		}
		int playerProcessCounter = 0;
		int playerProcessLimit = Mathf.Max(3, allPlayers.Count / 5);
		System.Collections.Generic.Dictionary<int, float> dictionary = new System.Collections.Generic.Dictionary<int, float>();
		System.Collections.Generic.List<VRCPlayerApi> playersInRange = new System.Collections.Generic.List<VRCPlayerApi>();
		System.Collections.Generic.List<int> list = new System.Collections.Generic.List<int>();
		Il2CppSystem.Collections.Generic.List<VRCPlayerApi>.Enumerator enumerator = allPlayers.GetEnumerator();
		while (enumerator.MoveNext())
		{
			VRCPlayerApi current = enumerator.Current;
			if (current != null && current.IsValid() && current != localPlayer)
			{
				int playerId = current.playerId;
				activePlayerIds.Add(playerId);
				float num = (dictionary[playerId] = Vector3.Distance(current.GetPosition(), position));
				playerNameCache[playerId] = current.displayName;
				if (num <= espDistance)
				{
					playersInRange.Add(current);
				}
				else if (playerNameplates.ContainsKey(playerId))
				{
					list.Add(playerId);
				}
			}
		}
		playerDistances = dictionary;
		foreach (int item in list)
		{
			processedPlayerIds.Add(item);
			yield return ResetNameplate(item);
			playerProcessCounter++;
			if (playerProcessCounter >= playerProcessLimit)
			{
				playerProcessCounter = 0;
				yield return null;
			}
		}
		playersInRange.Sort(delegate(VRCPlayerApi a, VRCPlayerApi b)
		{
			float num3 = playerDistances[a.playerId];
			float value = playerDistances[b.playerId];
			return num3.CompareTo(value);
		});
		for (int i = 0; i < playersInRange.Count; i++)
		{
			VRCPlayerApi vRCPlayerApi = playersInRange[i];
			if (vRCPlayerApi != null && vRCPlayerApi.IsValid())
			{
				int playerId2 = vRCPlayerApi.playerId;
				processedPlayerIds.Add(playerId2);
				yield return FindAndUpdateNameplate(vRCPlayerApi, playerDistances[playerId2]);
				playerProcessCounter++;
				if (playerProcessCounter >= playerProcessLimit)
				{
					playerProcessCounter = 0;
					yield return null;
				}
			}
		}
		if (Time.frameCount % 300 == 0)
		{
			System.Collections.Generic.List<int> list2 = new System.Collections.Generic.List<int>();
			foreach (int key in playerNameplates.Keys)
			{
				if (!activePlayerIds.Contains(key) || !processedPlayerIds.Contains(key))
				{
					list2.Add(key);
				}
			}
			foreach (int item2 in list2)
			{
				yield return ResetNameplate(item2);
			}
		}
		updateScheduled = false;
	}

	private System.Collections.IEnumerator FindAndUpdateNameplate(VRCPlayerApi player, float distance)
	{
		if (player == null || !player.IsValid())
		{
			yield break;
		}
		int playerId = player.playerId;
		_ = player.displayName;
		GameObject nameplate = null;
		if (playerNameplates.ContainsKey(playerId) && playerNameplates[playerId] != null)
		{
			nameplate = playerNameplates[playerId];
			if (nameplate == null || nameplate.Equals(null))
			{
				playerNameplates.Remove(playerId);
				nameplate = null;
			}
		}
		if (nameplate == null)
		{
			nameplate = FindPlayerNameplate(player);
			if (playerId % 3 == 0)
			{
				yield return null;
			}
		}
		if (nameplate != null)
		{
			ModifyNameplateForESP(nameplate, player, distance);
		}
	}

	private GameObject FindPlayerNameplate(VRCPlayerApi player)
	{
		try
		{
			int playerId = player.playerId;
			string displayName = player.displayName;
			string key = $"player_{playerId}_{displayName}";
			if (nameplatePathCache.ContainsKey(key) && nameplatePathCache[key] != null)
			{
				GameObject gameObject = nameplatePathCache[key];
				if (!gameObject.Equals(null))
				{
					playerNameplates[playerId] = gameObject;
					return gameObject;
				}
				nameplatePathCache.Remove(key);
			}
			if (!applicationsCached || applicationCache.Count == 0)
			{
				CacheApplications();
				return null;
			}
			UserManager userManager = UserManager.Instance;
			if (userManager != null)
			{
				GameObject playerNameplate = userManager.GetPlayerNameplate(player);
				if (playerNameplate != null)
				{
					playerNameplates[playerId] = playerNameplate;
					nameplatePathCache[key] = playerNameplate;
					return playerNameplate;
				}
			}
			int num = playerId % applicationCache.Count;
			if (num < 0 || num >= applicationCache.Count)
			{
				num = 0;
			}
			GameObject gameObject2 = applicationCache[num];
			if (gameObject2 == null || gameObject2.Equals(null))
			{
				return null;
			}
			Transform transform = gameObject2.transform.Find("NameplateManager");
			if (transform == null)
			{
				return null;
			}
			Transform transform2 = transform.Find("NameplateContainer");
			if (transform2 == null)
			{
				return null;
			}
			Il2CppSystem.Collections.IEnumerator enumerator = transform2.GetEnumerator();
			try
			{
				while (enumerator.MoveNext())
				{
					Transform transform3 = (Transform)enumerator.Current;
					if (transform3 == null)
					{
						continue;
					}
					try
					{
						Transform transform4 = transform3.Find("Canvas/NameplateGroup/Nameplate/Contents/Main/Text Container/Name");
						if (!(transform4 == null))
						{
							TextMeshProUGUI component = transform4.GetComponent<TextMeshProUGUI>();
							if (component != null && (component.text == displayName || component.text.StartsWith(displayName)))
							{
								GameObject gameObject3 = transform4.gameObject;
								playerNameplates[playerId] = gameObject3;
								nameplatePathCache[key] = gameObject3;
								return gameObject3;
							}
						}
					}
					catch (Exception)
					{
					}
				}
			}
			finally
			{
				if (enumerator is IDisposable disposable)
				{
					disposable.Dispose();
				}
			}
		}
		catch (Exception ex2)
		{
			MelonLogger.Error("Error finding player nameplate: " + ex2.Message);
		}
		return null;
	}

	private void ModifyNameplateForESP(GameObject nameplate, VRCPlayerApi player, float distance)
	{
		try
		{
			if (nameplate == null || nameplate.Equals(null))
			{
				return;
			}
			int playerId = player.playerId;
			TextMeshProUGUI component = nameplate.GetComponent<TextMeshProUGUI>();
			if (component == null)
			{
				return;
			}
			if (!originalMaterials.ContainsKey(playerId))
			{
				originalMaterials[playerId] = component.materialForRendering;
			}
			component.fontSharedMaterial.renderQueue = espRenderQueue;
			component.fontSharedMaterial.SetFloat("_ZTest", zTestAlways);
			component.fontSharedMaterial.SetInt("_SrcBlend", srcBlendMode);
			component.fontSharedMaterial.SetInt("_DstBlend", dstBlendMode);
			if (!playerDistances.TryGetValue(playerId, out var value) || Mathf.Abs(distance - value) > 0.5f)
			{
				string empty = string.Empty;
				if (!showDistanceESP)
				{
					empty = ((!player.IsUserInVR()) ? player.displayName : (player.displayName + " [VR]"));
				}
				else
				{
					string text = player.displayName;
					if (player.IsUserInVR())
					{
						text += " [VR]";
					}
					empty = $"{text} [{distance:F1}m]";
				}
				if (component.text != empty)
				{
					component.text = empty;
				}
			}
			if (component.color != Color.yellow)
			{
				component.color = Color.yellow;
				component.fontSize *= 1.2f;
			}
			if (!nameplate.activeSelf)
			{
				nameplate.SetActive(value: true);
			}
		}
		catch (Exception)
		{
		}
	}

	private System.Collections.IEnumerator ResetNameplateCoroutine(int playerId)
	{
		yield return ResetNameplate(playerId);
	}

	private System.Collections.IEnumerator ResetNameplate(int playerId)
	{
		try
		{
			if (!playerNameplates.ContainsKey(playerId))
			{
				yield break;
			}
			GameObject gameObject = playerNameplates[playerId];
			if (gameObject == null || gameObject.Equals(null))
			{
				playerNameplates.Remove(playerId);
				originalMaterials.Remove(playerId);
				yield break;
			}
			TextMeshProUGUI component = gameObject.GetComponent<TextMeshProUGUI>();
			if (component == null)
			{
				yield break;
			}
			component.fontSharedMaterial.renderQueue = 3000;
			component.fontSharedMaterial.SetFloat("_ZTest", zTestNormal);
			component.fontSize /= 1.2f;
			component.color = Color.white;
			playerNameplates.Remove(playerId);
			originalMaterials.Remove(playerId);
			string value = null;
			if (playerNameCache.TryGetValue(playerId, out value))
			{
				string key = $"player_{playerId}_{value}";
				if (nameplatePathCache.ContainsKey(key))
				{
					nameplatePathCache.Remove(key);
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private System.Collections.IEnumerator ResetAllNameplatesCoroutine()
	{
		System.Collections.Generic.List<int> list = new System.Collections.Generic.List<int>(playerNameplates.Keys);
		foreach (int item in list)
		{
			yield return ResetNameplate(item);
			yield return null;
		}
		playerNameplates.Clear();
		originalMaterials.Clear();
		playerDistances.Clear();
		activePlayerIds.Clear();
		nameplatePathCache.Clear();
	}

	public void Cleanup()
	{
		MelonCoroutines.Start(ResetAllNameplatesCoroutine());
		if (xrayMaterial != null)
		{
			UnityEngine.Object.Destroy(xrayMaterial);
		}
	}
}
