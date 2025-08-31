using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Il2CppSystem.Collections;
using Il2CppSystem.Collections.Generic;
using MelonLoader;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

namespace NoClipMod;

public class UserManager
{
	private class UserData
	{
		public int PlayerId { get; set; }

		public string DisplayName { get; set; }

		public bool IsVR { get; set; }

		public GameObject NameplateBackground { get; set; }

		public Renderer[] AvatarRenderers { get; set; }
	}

	private static UserManager instance;

	private System.Collections.Generic.Dictionary<int, UserData> cachedUsers = new System.Collections.Generic.Dictionary<int, UserData>();

	private System.Collections.Generic.Dictionary<int, GameObject> nameplateLookup = new System.Collections.Generic.Dictionary<int, GameObject>();

	private System.Collections.Generic.Dictionary<int, Transform> avatarLookup = new System.Collections.Generic.Dictionary<int, Transform>();

	private System.Collections.Generic.Dictionary<int, float> playerDistances = new System.Collections.Generic.Dictionary<int, float>();

	private System.Collections.Generic.List<GameObject> applicationCache = new System.Collections.Generic.List<GameObject>();

	private System.Collections.Generic.Dictionary<string, bool> settings = new System.Collections.Generic.Dictionary<string, bool>
	{
		{ "NameplateVisible", true },
		{ "DistanceVisible", true },
		{ "AntiAvatars", false },
		{ "AvatarsEnabled", true },
		{ "NameplateSeeThrough", false },
		{ "HideLocal", false }
	};

	private System.Collections.Generic.Dictionary<int, float> avatarScales = new System.Collections.Generic.Dictionary<int, float>();

	private float defaultAvatarScale = 1f;

	private bool isInitialized;

	private float lastUpdateTime;

	private float updateInterval = 3f;

	private bool updateScheduled;

	private HashSet<int> activePlayerIds = new HashSet<int>();

	private int lastProcessedPlayerCount;

	private bool forceFullUpdate;

	private float applicationCacheRefreshInterval = 60f;

	private float lastApplicationCacheTime;

	private int throughWallRenderQueue = 4000;

	private float zTestAlways = 8f;

	private float zTestNormal = 4f;

	private int srcBlendMode = 5;

	private int dstBlendMode = 10;

	public static UserManager Instance
	{
		get
		{
			if (instance == null)
			{
				instance = new UserManager();
			}
			return instance;
		}
	}

	public UserManager()
	{
		Initialize();
	}

	private void Initialize()
	{
		MelonLogger.Msg("Initializing UserManager...");
		MelonCoroutines.Start(DelayedInitialization());
	}

	private System.Collections.IEnumerator DelayedInitialization()
	{
		yield return new WaitForSeconds(5f);
		CacheApplications();
		UpdateAllUsers();
		isInitialized = true;
		MelonLogger.Msg("UserManager initialized");
		while (true)
		{
			yield return new WaitForSeconds(60f);
			CacheApplications();
			yield return new WaitForSeconds(1f);
			forceFullUpdate = true;
			MelonCoroutines.Start(UpdateAllUsersCoroutine());
		}
	}

	private void CacheApplications()
	{
		try
		{
			if (!(Time.time - lastApplicationCacheTime < applicationCacheRefreshInterval) || applicationCache.Count <= 0)
			{
				applicationCache.Clear();
				System.Collections.Generic.List<GameObject> list = (from go in UnityEngine.Object.FindObjectsOfType<GameObject>()
					where go.name.StartsWith("_Application")
					select go).ToList();
				if (list.Count > 0)
				{
					applicationCache.AddRange(list);
					MelonLogger.Msg($"Cached {list.Count} application objects for UserManager");
				}
				lastApplicationCacheTime = Time.time;
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error caching applications: " + ex.Message);
		}
	}

	public void OnUpdate()
	{
		if (isInitialized && Time.time - lastUpdateTime > updateInterval && !updateScheduled)
		{
			updateScheduled = true;
			MelonCoroutines.Start(UpdateAllUsersCoroutine());
			lastUpdateTime = Time.time;
		}
	}

	public void UpdateAllUsers()
	{
		if (!updateScheduled)
		{
			updateScheduled = true;
			forceFullUpdate = true;
			MelonCoroutines.Start(UpdateAllUsersCoroutine());
		}
	}

	private System.Collections.IEnumerator UpdateAllUsersCoroutine()
	{
		VRCPlayerApi localPlayer = Networking.LocalPlayer;
		if (localPlayer == null)
		{
			updateScheduled = false;
			yield break;
		}
		Vector3 position = localPlayer.GetPosition();
		if (forceFullUpdate)
		{
			activePlayerIds.Clear();
		}
		Il2CppSystem.Collections.Generic.List<VRCPlayerApi> allPlayers = VRCPlayerApi.AllPlayers;
		if (allPlayers == null || allPlayers.Count == 0)
		{
			updateScheduled = false;
			yield break;
		}
		if (!forceFullUpdate && allPlayers.Count == lastProcessedPlayerCount)
		{
			updateScheduled = false;
			yield break;
		}
		lastProcessedPlayerCount = allPlayers.Count;
		System.Collections.Generic.Dictionary<int, float> dictionary = new System.Collections.Generic.Dictionary<int, float>();
		System.Collections.Generic.List<VRCPlayerApi> playersToProcess = new System.Collections.Generic.List<VRCPlayerApi>();
		Il2CppSystem.Collections.Generic.List<VRCPlayerApi>.Enumerator enumerator = allPlayers.GetEnumerator();
		while (enumerator.MoveNext())
		{
			VRCPlayerApi current = enumerator.Current;
			if (current != null && current.IsValid() && (current != localPlayer || !settings["HideLocal"]))
			{
				int playerId = current.playerId;
				activePlayerIds.Add(playerId);
				float value = Vector3.Distance(current.GetPosition(), position);
				dictionary[playerId] = value;
				if (forceFullUpdate || !cachedUsers.ContainsKey(playerId))
				{
					playersToProcess.Add(current);
				}
			}
		}
		playerDistances = dictionary;
		for (int i = 0; i < playersToProcess.Count; i += 5)
		{
			int endIdx = Mathf.Min(i + 5, playersToProcess.Count);
			for (int j = i; j < endIdx; j++)
			{
				VRCPlayerApi vRCPlayerApi = playersToProcess[j];
				if (vRCPlayerApi != null && vRCPlayerApi.IsValid())
				{
					yield return UpdateUserData(vRCPlayerApi);
				}
			}
			yield return null;
		}
		if (forceFullUpdate)
		{
			System.Collections.Generic.List<int> list = new System.Collections.Generic.List<int>();
			foreach (int key in cachedUsers.Keys)
			{
				if (!activePlayerIds.Contains(key))
				{
					list.Add(key);
				}
			}
			foreach (int item in list)
			{
				RemoveUserData(item);
			}
		}
		updateScheduled = false;
		forceFullUpdate = false;
	}

	private System.Collections.IEnumerator UpdateUserData(VRCPlayerApi player)
	{
		if (player != null && player.IsValid())
		{
			int playerId = player.playerId;
			string displayName = player.displayName;
			bool isVR = player.IsUserInVR();
			if (!cachedUsers.ContainsKey(playerId))
			{
				cachedUsers[playerId] = new UserData
				{
					PlayerId = playerId,
					DisplayName = displayName,
					IsVR = isVR
				};
			}
			else
			{
				UserData userData = cachedUsers[playerId];
				userData.DisplayName = displayName;
				userData.IsVR = isVR;
			}
			if (!nameplateLookup.ContainsKey(playerId) || nameplateLookup[playerId] == null)
			{
				yield return FindAndCacheNameplate(player);
			}
			if (!avatarLookup.ContainsKey(playerId) || avatarLookup[playerId] == null)
			{
				yield return FindAndCacheAvatar(player);
			}
			if (nameplateLookup.ContainsKey(playerId) && nameplateLookup[playerId] != null)
			{
				ApplyNameplateSettings(player);
			}
			if (avatarLookup.ContainsKey(playerId) && avatarLookup[playerId] != null)
			{
				ApplyAvatarSettings(player);
			}
		}
	}

	private System.Collections.IEnumerator FindAndCacheNameplate(VRCPlayerApi player)
	{
		int playerId = player.playerId;
		string displayName = player.displayName;
		bool flag = false;
		try
		{
			if (applicationCache.Count == 0)
			{
				CacheApplications();
			}
			foreach (GameObject item in applicationCache)
			{
				if (item == null)
				{
					continue;
				}
				Transform transform = item.transform.Find("NameplateManager");
				if (transform == null)
				{
					continue;
				}
				Transform transform2 = transform.Find("NameplateContainer");
				if (transform2 == null)
				{
					continue;
				}
				Il2CppSystem.Collections.IEnumerator enumerator2 = transform2.GetEnumerator();
				try
				{
					while (enumerator2.MoveNext())
					{
						Transform transform3 = (Transform)enumerator2.Current;
						if (transform3 == null)
						{
							continue;
						}
						try
						{
							Transform transform4 = transform3.Find("Canvas/NameplateGroup/Nameplate/Contents/Main/Text Container/Name");
							if (transform4 == null)
							{
								continue;
							}
							TextMeshProUGUI component = transform4.GetComponent<TextMeshProUGUI>();
							if (!(component != null) || !component.text.Contains(displayName))
							{
								continue;
							}
							nameplateLookup[playerId] = transform4.gameObject;
							Transform transform5 = transform3.Find("Canvas/NameplateGroup/Nameplate/Contents/Main/Background");
							if (transform5 != null && cachedUsers.ContainsKey(playerId))
							{
								cachedUsers[playerId].NameplateBackground = transform5.gameObject;
							}
							flag = true;
							break;
						}
						catch (Exception)
						{
						}
					}
				}
				finally
				{
					if (enumerator2 is IDisposable disposable)
					{
						disposable.Dispose();
					}
				}
				if (flag)
				{
					break;
				}
			}
		}
		catch (Exception ex2)
		{
			MelonLogger.Error("Error finding nameplate for player " + player.displayName + ": " + ex2.Message);
		}
		yield return null;
	}

	private System.Collections.IEnumerator FindAndCacheAvatar(VRCPlayerApi player)
	{
		int playerId = player.playerId;
		try
		{
			GameObject gameObject = GameObject.Find($"VRCPlayer[Remote] {playerId}");
			if (gameObject == null)
			{
				gameObject = UnityEngine.Object.FindObjectsOfType<GameObject>().FirstOrDefault((GameObject go) => go.name.StartsWith("VRCPlayer[Remote]") && go.name.Contains(playerId.ToString()));
			}
			if (gameObject != null)
			{
				Transform transform = gameObject.transform.Find("ForwardDirection");
				if (transform != null)
				{
					Transform transform2 = transform.Find("Avatar");
					if (transform2 != null)
					{
						avatarLookup[playerId] = transform2;
						if (cachedUsers.ContainsKey(playerId))
						{
							cachedUsers[playerId].AvatarRenderers = transform2.GetComponentsInChildren<Renderer>(includeInactive: true);
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error finding avatar for player " + player.displayName + ": " + ex.Message);
		}
		yield return null;
	}

	private void ApplyNameplateSettings(VRCPlayerApi player)
	{
		try
		{
			int playerId = player.playerId;
			if (!nameplateLookup.ContainsKey(playerId) || nameplateLookup[playerId] == null)
			{
				return;
			}
			GameObject gameObject = nameplateLookup[playerId];
			TextMeshProUGUI component = gameObject.GetComponent<TextMeshProUGUI>();
			if (component == null)
			{
				return;
			}
			UserData userData = cachedUsers[playerId];
			if (!settings["NameplateVisible"])
			{
				component.enabled = false;
				return;
			}
			component.enabled = true;
			if (settings["NameplateSeeThrough"])
			{
				component.fontSharedMaterial.renderQueue = throughWallRenderQueue;
				component.fontSharedMaterial.SetFloat("_ZTest", zTestAlways);
				component.fontSharedMaterial.SetInt("_SrcBlend", srcBlendMode);
				component.fontSharedMaterial.SetInt("_DstBlend", dstBlendMode);
				if (userData.NameplateBackground != null)
				{
					Image component2 = userData.NameplateBackground.GetComponent<Image>();
					if (component2 != null)
					{
						Color color = component2.color;
						color.a = 0.3f;
						component2.color = color;
					}
				}
			}
			else
			{
				component.fontSharedMaterial.renderQueue = 3000;
				component.fontSharedMaterial.SetFloat("_ZTest", zTestNormal);
				if (userData.NameplateBackground != null)
				{
					Image component3 = userData.NameplateBackground.GetComponent<Image>();
					if (component3 != null)
					{
						Color color2 = component3.color;
						color2.a = 1f;
						component3.color = color2;
					}
				}
			}
			if (settings["DistanceVisible"] && playerDistances.ContainsKey(playerId))
			{
				float num = playerDistances[playerId];
				string text = player.displayName;
				if (player.IsUserInVR())
				{
					text += " [VR]";
				}
				text += $" [{num:F1}m]";
				if (component.text != text)
				{
					component.text = text;
				}
			}
			else
			{
				string text2 = player.displayName;
				if (player.IsUserInVR())
				{
					text2 += " [VR]";
				}
				if (component.text != text2)
				{
					component.text = text2;
				}
			}
			gameObject.SetActive(value: true);
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error applying nameplate settings: " + ex.Message);
		}
	}

	private void ApplyAvatarSettings(VRCPlayerApi player)
	{
		try
		{
			int playerId = player.playerId;
			if (!avatarLookup.ContainsKey(playerId) || avatarLookup[playerId] == null)
			{
				return;
			}
			Transform transform = avatarLookup[playerId];
			UserData userData = cachedUsers[playerId];
			if (settings["AntiAvatars"])
			{
				transform.gameObject.SetActive(value: false);
				if (userData.AvatarRenderers != null)
				{
					Renderer[] avatarRenderers = userData.AvatarRenderers;
					foreach (Renderer renderer in avatarRenderers)
					{
						if (renderer != null && renderer.enabled)
						{
							renderer.enabled = false;
						}
					}
				}
			}
			else
			{
				transform.gameObject.SetActive(settings["AvatarsEnabled"]);
				if (settings["AvatarsEnabled"] && userData.AvatarRenderers != null)
				{
					Renderer[] avatarRenderers = userData.AvatarRenderers;
					foreach (Renderer renderer2 in avatarRenderers)
					{
						if (renderer2 != null && !renderer2.enabled)
						{
							renderer2.enabled = true;
						}
					}
				}
			}
			if (avatarScales.ContainsKey(playerId))
			{
				float num = avatarScales[playerId];
				transform.localScale = new Vector3(num, num, num);
			}
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error applying avatar settings: " + ex.Message);
		}
	}

	private void RemoveUserData(int userId)
	{
		if (cachedUsers.ContainsKey(userId))
		{
			cachedUsers.Remove(userId);
		}
		if (nameplateLookup.ContainsKey(userId))
		{
			nameplateLookup.Remove(userId);
		}
		if (avatarLookup.ContainsKey(userId))
		{
			avatarLookup.Remove(userId);
		}
		if (playerDistances.ContainsKey(userId))
		{
			playerDistances.Remove(userId);
		}
		if (avatarScales.ContainsKey(userId))
		{
			avatarScales.Remove(userId);
		}
	}

	public System.Collections.Generic.Dictionary<string, bool> GetSettings()
	{
		return settings;
	}

	public void SetNameplatesVisible(bool visible)
	{
		settings["NameplateVisible"] = visible;
		ApplySettingsToAllUsers();
	}

	public void SetDistanceVisible(bool visible)
	{
		settings["DistanceVisible"] = visible;
		ApplySettingsToAllUsers();
	}

	public void SetAntiAvatars(bool enabled)
	{
		settings["AntiAvatars"] = enabled;
		settings["AvatarsEnabled"] = !enabled;
		ApplySettingsToAllUsers();
	}

	public void SetAvatarsEnabled(bool enabled)
	{
		settings["AvatarsEnabled"] = enabled;
		if (enabled)
		{
			settings["AntiAvatars"] = false;
		}
		ApplySettingsToAllUsers();
	}

	public void SetNameplatesSeeThrough(bool enabled)
	{
		settings["NameplateSeeThrough"] = enabled;
		ApplySettingsToAllUsers();
	}

	public void SetHideLocal(bool hide)
	{
		settings["HideLocal"] = hide;
		ApplySettingsToAllUsers();
	}

	public void SetAvatarScale(VRCPlayerApi player, float scale)
	{
		if (player != null && player.IsValid())
		{
			int playerId = player.playerId;
			avatarScales[playerId] = scale;
			if (avatarLookup.ContainsKey(playerId) && avatarLookup[playerId] != null)
			{
				avatarLookup[playerId].localScale = new Vector3(scale, scale, scale);
			}
		}
	}

	public void SetAllAvatarScales(float scale)
	{
		defaultAvatarScale = scale;
		foreach (System.Collections.Generic.KeyValuePair<int, Transform> item in avatarLookup)
		{
			int key = item.Key;
			Transform value = item.Value;
			if (value != null)
			{
				avatarScales[key] = scale;
				value.localScale = new Vector3(scale, scale, scale);
			}
		}
	}

	public System.Collections.Generic.List<VRCPlayerApi> GetAllPlayers()
	{
		System.Collections.Generic.List<VRCPlayerApi> list = new System.Collections.Generic.List<VRCPlayerApi>();
		Il2CppSystem.Collections.Generic.List<VRCPlayerApi> allPlayers = VRCPlayerApi.AllPlayers;
		if (allPlayers != null)
		{
			Il2CppSystem.Collections.Generic.List<VRCPlayerApi>.Enumerator enumerator = allPlayers.GetEnumerator();
			while (enumerator.MoveNext())
			{
				VRCPlayerApi current = enumerator.Current;
				if (current != null && current.IsValid())
				{
					list.Add(current);
				}
			}
		}
		return list;
	}

	public Transform GetPlayerAvatar(int playerId)
	{
		if (avatarLookup.ContainsKey(playerId))
		{
			return avatarLookup[playerId];
		}
		return null;
	}

	public Transform GetPlayerAvatar(VRCPlayerApi player)
	{
		if (player == null || !player.IsValid())
		{
			return null;
		}
		return GetPlayerAvatar(player.playerId);
	}

	public GameObject GetPlayerNameplate(int playerId)
	{
		if (nameplateLookup.ContainsKey(playerId))
		{
			return nameplateLookup[playerId];
		}
		return null;
	}

	public GameObject GetPlayerNameplate(VRCPlayerApi player)
	{
		if (player == null || !player.IsValid())
		{
			return null;
		}
		return GetPlayerNameplate(player.playerId);
	}

	private void ApplySettingsToAllUsers()
	{
		foreach (VRCPlayerApi allPlayer in GetAllPlayers())
		{
			if (allPlayer != null && allPlayer.IsValid())
			{
				ApplyNameplateSettings(allPlayer);
				ApplyAvatarSettings(allPlayer);
			}
		}
	}

	public void Cleanup()
	{
		settings["NameplateVisible"] = true;
		settings["DistanceVisible"] = false;
		settings["AntiAvatars"] = false;
		settings["AvatarsEnabled"] = true;
		settings["NameplateSeeThrough"] = false;
		ApplySettingsToAllUsers();
		cachedUsers.Clear();
		nameplateLookup.Clear();
		avatarLookup.Clear();
		playerDistances.Clear();
		avatarScales.Clear();
		applicationCache.Clear();
	}
}
