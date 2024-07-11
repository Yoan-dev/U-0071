using System.Runtime.CompilerServices;
using TMPro;
using U0071;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.UIElements;

public class UIManager : MonoBehaviour
{
	public TMP_Text Interaction;
	public TMP_Text PopTextPrefab;
	public float HeightOffset = 1.2f;

	// core
	private EntityQuery _query;

	// HUD
	private VisualElement _root;
	private Label _hungerLabel;
	private Label _creditsLabel;
	private Label _cycleLabel;
	private Label _codeLabel;

	// miscellaneous
	private int _lastCreditsValue;
	
	private void OnEnable()
	{
		_root = GetComponent<UIDocument>().rootVisualElement;
		_hungerLabel = _root.Q<Label>("info_hunger");
		_creditsLabel = _root.Q<Label>("info_credits");
		_cycleLabel = _root.Q<Label>("info_cycle");
		_codeLabel = _root.Q<Label>("info_code");
	}

	public void Start()
	{
		_query = new EntityQueryBuilder(Allocator.Temp).WithAll<PlayerController, LocalTransform>().Build(Utilities.GetEntityManager());
	}

	public void OnDestroy()
	{
		_query.Dispose(); // needed ?
	}

	public void Update()
	{
		if (_query.HasSingleton<PlayerController>())
		{
			EntityManager entityManager = Utilities.GetEntityManager();
			Entity player = _query.GetSingletonEntity();
			PlayerController playerController = entityManager.GetComponentData<PlayerController>(player);
			CreditsComponent credits = entityManager.GetComponentData<CreditsComponent>(player);
			HungerComponent hunger = entityManager.GetComponentData<HungerComponent>(player);
			float3 position = entityManager.GetComponentData<LocalTransform>(player).Position;

			UpdateHUD(in credits, in hunger);
			UpdateInteraction(in entityManager, in playerController, position);
			ProcessPopEvents(in credits, position);
		}
		else
		{
			Interaction.gameObject.SetActive(false);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void UpdateHUD(in CreditsComponent credits, in HungerComponent hunger)
	{
		_hungerLabel.text = "Hunger " + (hunger.Value > 1f ? hunger.Value.ToString("0") : hunger.Value.ToString("0.0"));
		_creditsLabel.text = "Credits " + credits.Value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void UpdateInteraction(in EntityManager entityManager, in PlayerController playerController, float3 position)
	{
		string textOne = GetInteractionText(in playerController.PrimaryAction);
		string textTwo = GetInteractionText(in playerController.SecondaryAction);

		bool shouldDisplayTimer = playerController.ActionTimer > 0f && playerController.ActionTimer < int.MaxValue;

		Interaction.gameObject.SetActive(shouldDisplayTimer || textOne != "" || textTwo != "");

		if (Interaction.gameObject.activeSelf)
		{
			Interaction.text = shouldDisplayTimer ? playerController.ActionTimer.ToString("0.00") : textOne + (textOne != "" ? "\n" : "") + textTwo;
			Interaction.transform.SetLocalPositionAndRotation(position + new float3(0f, 0f, HeightOffset), Interaction.transform.rotation);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void ProcessPopEvents(in CreditsComponent credits, float3 position)
	{
		int newCredits = credits.Value;
		int difference = newCredits - _lastCreditsValue;
		if (difference != 0)
		{
			TMP_Text pop = Instantiate(PopTextPrefab, position + new float3(0f, 0f, HeightOffset), Interaction.transform.rotation, transform);
			pop.text = (difference > 0 ? "+" : "") + difference + "c";
		}
		_lastCreditsValue = newCredits;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private string GetInteractionText(in ActionInfo info)
	{
		return info.Data.Target != Entity.Null ? 
			info.Key.ToString() + ": " + 
			(info.ActionName.Length > 0 ? info.ActionName : GetActionName(info.Type, info.Cost)) + 
			(info.TargetName.Length > 0 ? " " + info.TargetName : "") +
			(info.Cost > 0f ? " (-" + info.Cost + "c)" : "") +
			(info.DeviceName.Length > 0 ? " (" + info.DeviceName + ")" : "") : "";
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private string GetActionName(ActionFlag type, int cost)
	{
		return type switch
		{
			ActionFlag.Collect => cost > 0f ? "Buy" : "Take",
			ActionFlag.Eat => "Eat",
			ActionFlag.Destroy => "Destroy",
			ActionFlag.Pick => "Pick",
			ActionFlag.Store => "Store",
			ActionFlag.Drop => "Drop",
			ActionFlag.Search => "Loot",
			ActionFlag.Push => "Push",
			_ => "none",
		};
	}
}
