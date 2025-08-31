using System;
using MelonLoader;
using UnityEngine;
using VRC.SDKBase;

namespace NoClipMod;

public class FlightIndicator
{
	private GameObject indicator;

	private VRCPlayerApi localPlayer;

	private bool isVisible;

	public FlightIndicator(Color color)
	{
		CreateIndicator(color);
	}

	private void CreateIndicator(Color color)
	{
		try
		{
			indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			indicator.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
			indicator.GetComponent<Renderer>().material.color = color;
			indicator.SetActive(value: false);
			UnityEngine.Object.DontDestroyOnLoad(indicator);
		}
		catch (Exception ex)
		{
			MelonLogger.Error("Error creating flight indicator: " + ex.Message);
		}
	}

	public void Update(VRCPlayerApi player, bool show)
	{
		localPlayer = player;
		isVisible = show;
		if (indicator != null)
		{
			indicator.SetActive(isVisible);
			if (isVisible && localPlayer != null && localPlayer.IsValid())
			{
				indicator.transform.position = localPlayer.GetPosition() + Vector3.up * 2f;
			}
		}
	}

	public void SetColor(Color color)
	{
		if (indicator != null)
		{
			indicator.GetComponent<Renderer>().material.color = color;
		}
	}

	public void Cleanup()
	{
		if (indicator != null)
		{
			UnityEngine.Object.Destroy(indicator);
		}
	}
}
