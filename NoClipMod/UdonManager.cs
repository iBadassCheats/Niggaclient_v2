using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using VRC.Udon;
using VRC.Udon.Common.Enums;
using VRC.Udon.Common.Interfaces;

namespace NoClipMod;

public class UdonManager
{
	private static List<UdonBehaviour> cachedUdonBehaviours = new List<UdonBehaviour>();

	private static bool isInitialized = false;

	private static bool isNukeRunning = false;

	private static int batchSize = 10;

	private static float progressPercentage = 0f;

	public static void Initialize()
	{
		MelonLogger.Msg("Available NetworkEventTarget values:");
		foreach (object value in Enum.GetValues(typeof(NetworkEventTarget)))
		{
			MelonLogger.Msg($"  - {value}");
		}
		MelonCoroutines.Start(CacheUdonBehavioursCoroutine());
	}

	private static IEnumerator CacheUdonBehavioursCoroutine()
	{
		yield return new WaitForSeconds(5f);
		CacheUdonBehaviours();
		isInitialized = true;
	}

	public static void CacheUdonBehaviours()
	{
		try
		{
			cachedUdonBehaviours.Clear();
			foreach (UdonBehaviour item in UnityEngine.Object.FindObjectsOfType<UdonBehaviour>())
			{
				if (!(item == null) && (item.gameObject.hideFlags & HideFlags.HideAndDontSave) == 0)
				{
					cachedUdonBehaviours.Add(item);
				}
			}
			MelonLogger.Msg($"Cached {cachedUdonBehaviours.Count} UdonBehaviours for future use");
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error caching UdonBehaviours: " + ex.Message);
		}
	}

	public static void TriggerAllEvents()
	{
		if (isNukeRunning)
		{
			MelonLogger.Warning($"Udon Nuke is already running! Current progress: {progressPercentage:F1}%. Please wait for it to finish.");
			return;
		}
		try
		{
			isNukeRunning = true;
			progressPercentage = 0f;
			if (!isInitialized || cachedUdonBehaviours.Count == 0)
			{
				MelonLogger.Msg("UdonBehaviour cache not initialized or empty. Refreshing cache...");
				CacheUdonBehaviours();
				isInitialized = true;
			}
			int count = cachedUdonBehaviours.Count;
			MelonLogger.Msg($"Starting Udon Nuke on {count} UdonBehaviours");
			MelonCoroutines.Start(ProcessAllUdonBehavioursCoroutine());
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error starting Udon Nuke: " + ex.Message);
			isNukeRunning = false;
		}
	}

	private static IEnumerator ProcessAllUdonBehavioursCoroutine()
	{
		int triggeredEvents = 0;
		int processedBehaviours = 0;
		int totalBehaviours = cachedUdonBehaviours.Count;
		for (int i = 0; i < totalBehaviours; i += batchSize)
		{
			int num = Math.Min(batchSize, totalBehaviours - i);
			for (int j = 0; j < num; j++)
			{
				int num2 = i + j;
				if (num2 >= cachedUdonBehaviours.Count)
				{
					break;
				}
				UdonBehaviour udonBehaviour = cachedUdonBehaviours[num2];
				if (!(udonBehaviour == null))
				{
					GameObject gameObject = udonBehaviour.gameObject;
					MelonLogger.Msg("Processing UdonBehaviour on: " + gameObject.name);
					int num3 = ProcessUdonBehaviour(udonBehaviour);
					triggeredEvents += num3;
					processedBehaviours++;
					progressPercentage = (float)processedBehaviours / (float)totalBehaviours * 100f;
				}
			}
			yield return null;
			if (i % (batchSize * 5) == 0)
			{
				MelonLogger.Msg($"Udon Nuke progress: {progressPercentage:F1}% ({processedBehaviours}/{totalBehaviours})");
			}
		}
		MelonLogger.Msg($"Udon Nuke completed. Processed {processedBehaviours} behaviours and triggered {triggeredEvents} events in total.");
		isNukeRunning = false;
	}

	private static int ProcessUdonBehaviour(UdonBehaviour udon)
	{
		if (udon == null)
		{
			return 0;
		}
		GameObject gameObject = udon.gameObject;
		int num = 0;
		try
		{
			MethodInfo[] methods = udon.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public);
			int num2 = 0;
			MethodInfo[] array = methods;
			foreach (MethodInfo methodInfo in array)
			{
				try
				{
					if (methodInfo.Name.StartsWith("_") && methodInfo.GetParameters().Length == 0)
					{
						num2++;
						string name = methodInfo.Name;
						MelonLogger.Msg("Found standard Udon event: " + name + " on " + gameObject.name);
						TriggerEvent(udon, gameObject, name);
						num++;
					}
				}
				catch (Exception ex)
				{
					MelonLogger.Error("Error triggering standard event " + methodInfo.Name + " on " + gameObject.name + ": " + ex.Message);
				}
			}
			int num3 = 0;
			array = methods;
			foreach (MethodInfo methodInfo2 in array)
			{
				try
				{
					if (!methodInfo2.Name.StartsWith("_") && methodInfo2.GetParameters().Length == 0 && !ShouldSkipMethod(methodInfo2.Name))
					{
						num3++;
						string name2 = methodInfo2.Name;
						MelonLogger.Msg("Found other potential event: " + name2 + " on " + gameObject.name);
						TriggerEvent(udon, gameObject, name2);
						num++;
					}
				}
				catch (Exception ex2)
				{
					MelonLogger.Error("Error triggering other method " + methodInfo2.Name + " on " + gameObject.name + ": " + ex2.Message);
				}
			}
			MelonLogger.Msg($"Processed {num2} standard events and {num3} other methods for {gameObject.name}");
		}
		catch (Exception ex3)
		{
			MelonLogger.Error("Error processing UdonBehaviour " + gameObject.name + ": " + ex3.Message);
		}
		return num;
	}

	private static void TriggerEvent(UdonBehaviour udon, GameObject obj, string eventName)
	{
		try
		{
			udon.SendCustomEvent(eventName);
			MelonLogger.Msg("Triggered event: " + eventName + " on " + obj.name);
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error with SendCustomEvent for " + eventName + " on " + obj.name + ": " + ex.Message);
		}
		try
		{
			udon.RunProgram(eventName);
			MelonLogger.Msg("Ran program: " + eventName + " on " + obj.name);
		}
		catch (Exception ex2)
		{
			MelonLogger.Error("Error with RunProgram for " + eventName + " on " + obj.name + ": " + ex2.Message);
		}
		try
		{
			udon.SendCustomNetworkEvent(NetworkEventTarget.All, eventName);
			MelonLogger.Msg("Triggered network event: " + eventName + " on " + obj.name + " with target All");
		}
		catch (Exception ex3)
		{
			MelonLogger.Error("Error triggering network event " + eventName + " on " + obj.name + " with target All: " + ex3.Message);
		}
		try
		{
			udon.SendCustomNetworkEvent(NetworkEventTarget.Owner, eventName);
			MelonLogger.Msg("Triggered network event: " + eventName + " on " + obj.name + " with target Owner");
		}
		catch (Exception ex4)
		{
			MelonLogger.Error("Error triggering network event " + eventName + " on " + obj.name + " with target Owner: " + ex4.Message);
		}
		try
		{
			udon.SendCustomEventDelayedSeconds(eventName, 0.1f, EventTiming.Update);
			MelonLogger.Msg("Triggered delayed event: " + eventName + " on " + obj.name + " (0.1s)");
		}
		catch (Exception ex5)
		{
			MelonLogger.Error("Error triggering delayed event " + eventName + " on " + obj.name + ": " + ex5.Message);
		}
		try
		{
			udon.SendCustomEventDelayedFrames(eventName, 1, EventTiming.Update);
			MelonLogger.Msg("Triggered frame-delayed event: " + eventName + " on " + obj.name + " (1 frame)");
		}
		catch (Exception ex6)
		{
			MelonLogger.Error("Error triggering frame-delayed event " + eventName + " on " + obj.name + ": " + ex6.Message);
		}
	}

	private static bool ShouldSkipMethod(string methodName)
	{
		string[] array = new string[15]
		{
			"ToString", "Equals", "GetHashCode", "GetType", "get_", "set_", "add_", "remove_", "op_", "Start",
			"Update", "Awake", "OnEnable", "OnDisable", "OnDestroy"
		};
		foreach (string value in array)
		{
			if (methodName.StartsWith(value))
			{
				return true;
			}
		}
		return false;
	}
}
