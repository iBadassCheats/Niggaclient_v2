using System;
using System.Collections;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace NoClipMod;

public class ColliderManager
{
	private Collider[] allColliders;

	private bool[] originalColliderStates;

	private bool collidersCached;

	private object currentColliderCoroutine;

	public void CollectAndDisableColliders()
	{
		try
		{
			if (collidersCached)
			{
				if (currentColliderCoroutine != null)
				{
					MelonCoroutines.Stop(currentColliderCoroutine);
				}
				currentColliderCoroutine = MelonCoroutines.Start(DisableCollidersGradually());
				return;
			}
			allColliders = UnityEngine.Object.FindObjectsOfType<Collider>();
			if (allColliders == null)
			{
				MelonLogger.Warning("No colliders found in scene");
				return;
			}
			List<Collider> list = new List<Collider>();
			List<bool> list2 = new List<bool>();
			MelonLogger.Msg($"Found {allColliders.Length} colliders in scene");
			for (int i = 0; i < allColliders.Length; i++)
			{
				try
				{
					Collider collider = allColliders[i];
					if (collider == null)
					{
						continue;
					}
					GameObject gameObject = collider.gameObject;
					if (gameObject == null)
					{
						continue;
					}
					if ((gameObject.hideFlags & HideFlags.HideAndDontSave) != HideFlags.None)
					{
						MelonLogger.Msg("Skipping DontDestroy/HideAndDontSave collider on " + gameObject.name);
						continue;
					}
					bool flag = false;
					Transform transform = gameObject.transform;
					while (transform != null)
					{
						int layer = transform.gameObject.layer;
						if (layer == LayerMask.NameToLayer("PlayerLocal") || layer == LayerMask.NameToLayer("Player") || layer == LayerMask.NameToLayer("Pickup"))
						{
							flag = true;
							MelonLogger.Msg("Skipping whitelisted layer collider on " + gameObject.name);
							break;
						}
						transform = transform.parent;
					}
					if (!flag)
					{
						list.Add(collider);
						list2.Add(collider.enabled);
					}
				}
				catch (Exception ex)
				{
					MelonLogger.Error($"Error processing collider {i}: {ex.Message}");
				}
			}
			allColliders = list.ToArray();
			originalColliderStates = list2.ToArray();
			collidersCached = true;
			MelonLogger.Msg($"Starting to disable {allColliders.Length} colliders");
			if (currentColliderCoroutine != null)
			{
				MelonCoroutines.Stop(currentColliderCoroutine);
			}
			currentColliderCoroutine = MelonCoroutines.Start(DisableCollidersGradually());
		}
		catch (Exception ex2)
		{
			MelonLogger.Error("Error collecting colliders: " + ex2.Message);
		}
	}

	private IEnumerator DisableCollidersGradually()
	{
		if (allColliders == null)
		{
			yield break;
		}
		int processedCount = 0;
		while (processedCount < allColliders.Length)
		{
			int num = Mathf.Min(processedCount + 5, allColliders.Length);
			for (int i = processedCount; i < num; i++)
			{
				if (allColliders[i] != null)
				{
					allColliders[i].enabled = false;
				}
			}
			processedCount = num;
			yield return new WaitForSeconds(0.02f);
		}
		currentColliderCoroutine = null;
	}

	public void RestoreColliders()
	{
		try
		{
			if (allColliders == null || originalColliderStates == null)
			{
				MelonLogger.Warning("No colliders to restore");
				return;
			}
			if (currentColliderCoroutine != null)
			{
				MelonCoroutines.Stop(currentColliderCoroutine);
			}
			currentColliderCoroutine = MelonCoroutines.Start(RestoreCollidersGradually());
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error restoring colliders: " + ex.Message);
		}
	}

	private IEnumerator RestoreCollidersGradually()
	{
		if (allColliders == null || originalColliderStates == null)
		{
			yield break;
		}
		int processedCount = 0;
		while (processedCount < allColliders.Length)
		{
			int num = Mathf.Min(processedCount + 5, allColliders.Length);
			for (int i = processedCount; i < num; i++)
			{
				if (allColliders[i] != null)
				{
					allColliders[i].enabled = originalColliderStates[i];
				}
			}
			processedCount = num;
			yield return new WaitForSeconds(0.02f);
		}
		currentColliderCoroutine = null;
		allColliders = null;
		originalColliderStates = null;
		collidersCached = false;
	}
}
