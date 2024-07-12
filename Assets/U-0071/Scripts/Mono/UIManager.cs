using System.Runtime.CompilerServices;
using TMPro;
using U0071;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UIElements;

public class UIManager : MonoBehaviour
{
	[Header("Core")]
	public TMP_Text Interaction;
	public TMP_Text PopTextPrefab;
	public float HeightOffset = 1.4f;

	[Header("PeekingBubble")]
	public TMP_Text PeekingBubble;
	public MeshRenderer BubbleRenderer;
	public Material BubbleMaterial;
	public Material DisabledBubbleMaterial;

	// ECS
	private Entity _player;
	private Entity _gameSingleton;

	// HUD
	private VisualElement _root;
	private Label _hungerLabel;
	private Label _creditsLabel;
	private Label _cycleLabel;
	private Label _codeLabel;

	// codepad
	private Codepad _codepad;

	// miscellaneous
	private int _lastCreditsValue;
	private float _peekingLastTimer;

	private void OnEnable()
	{
		_root = GetComponent<UIDocument>().rootVisualElement;
		_hungerLabel = _root.Q<Label>("info_hunger");
		_creditsLabel = _root.Q<Label>("info_credits");
		_cycleLabel = _root.Q<Label>("info_cycle");
		_codeLabel = _root.Q<Label>("info_code");
		_codepad = new Codepad(_root.Q<VisualElement>("codepad"));
	}

	public void Update()
	{
		EntityManager entityManager = Utilities.GetEntityManager();

		if (_player == Entity.Null)
		{
			EntityQuery query = new EntityQueryBuilder(Allocator.Temp).WithAll<PlayerController>().Build(Utilities.GetEntityManager());
			if (query.HasSingleton<PlayerController>())
			{
				_player = query.GetSingletonEntity();
			}
		}
		if (_gameSingleton == Entity.Null)
		{
			EntityQuery query = new EntityQueryBuilder(Allocator.Temp).WithAll<CycleComponent>().Build(Utilities.GetEntityManager());
			if (query.HasSingleton<PlayerController>())
			{
				_gameSingleton = query.GetSingletonEntity();
			}
		}
		if (_player != Entity.Null && _gameSingleton != Entity.Null)
		{
			_root.style.display = DisplayStyle.Flex;

			PlayerController playerController = entityManager.GetComponentData<PlayerController>(_player);
			ActionController actionController = entityManager.GetComponentData<ActionController>(_player);
			CreditsComponent credits = entityManager.GetComponentData<CreditsComponent>(_player);
			HungerComponent hunger = entityManager.GetComponentData<HungerComponent>(_player);
			PeekingInfoComponent peekingInfo = entityManager.GetComponentData<PeekingInfoComponent>(_player);
			float3 position = entityManager.GetComponentData<LocalTransform>(_player).Position;
			CycleComponent cycle = entityManager.GetComponentData<CycleComponent>(_gameSingleton);

			UpdateHUD(in credits, in hunger, in cycle);
			UpdateInteraction(in playerController, position);
			ProcessPopEvents(in credits, position);
			UpdateCodepad(in entityManager, in playerController, in actionController, in cycle);

			UpdatePeekingBubble(in peekingInfo, in cycle);
		}
		else
		{
			_root.style.display = DisplayStyle.None;
			Interaction.gameObject.SetActive(false);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void UpdateHUD(in CreditsComponent credits, in HungerComponent hunger, in CycleComponent cycle)
	{
		_hungerLabel.text = "Hunger " + (hunger.Value > 1f ? hunger.Value.ToString("0") : hunger.Value.ToString("0.0"));
		_creditsLabel.text = "Credits " + credits.Value;

		int minutes = (int)(cycle.CycleTimer / 60f);
		int seconds = (int)(cycle.CycleTimer - minutes * 60);
		_cycleLabel.text = minutes.ToString("00") + ":" + seconds.ToString("00") + " - Cycle " + cycle.CycleCounter.ToString();
		_codeLabel.text = cycle.LevelOneCode.ToString("0000") + " - LVL1";
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void UpdateInteraction(in PlayerController playerController, float3 position)
	{
		string textOne = GetInteractionText(in playerController.PrimaryAction);
		string textTwo = GetInteractionText(in playerController.SecondaryAction);

		bool shouldDisplayTimer = playerController.ActionTimer > 0f && playerController.ActionTimer < 9999f;

		Interaction.gameObject.SetActive(shouldDisplayTimer || textOne != "" || textTwo != "");

		if (Interaction.gameObject.activeSelf)
		{
			Interaction.text = shouldDisplayTimer ? playerController.ActionTimer.ToString("0.00") : textOne + (textOne != "" ? "\n" : "") + textTwo;
			Interaction.transform.SetLocalPositionAndRotation(new float3(position.x, position.y + 5f, position.z + HeightOffset), Interaction.transform.rotation);
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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void UpdateCodepad(in EntityManager entityManager, in PlayerController playerController, in ActionController actionController, in CycleComponent cycle)
	{
		// done in managed class for ease
		
		// TODO: only from front (cache in player controller)

		if (actionController.IsResolving && playerController.CachedDoorAuthorization != 0 && actionController.Action.HasActionFlag(ActionFlag.Open))
		{
			// is interacting with a door
			if (_codepad.IsShown())
			{
				if (!playerController.MoveInput.Equals(float2.zero) || Input.GetKeyDown(KeyCode.Escape))
				{
					// cancel
					ActionController controller = entityManager.GetComponentData<ActionController>(_player);
					controller.Stop(false);
					entityManager.SetComponentData(_player, controller);
					_codepad.ExitScreen();
					return;
				}

				int numeric = -1;
				bool validate = false;
				bool cancel = false;
				if (Input.GetKeyDown(KeyCode.KeypadEnter)) validate = true;
				else if (Input.GetKeyDown(KeyCode.KeypadPeriod)) cancel = true;
				else if (Input.GetKeyDown(KeyCode.Keypad0)) numeric = 0;
				else if (Input.GetKeyDown(KeyCode.Keypad1)) numeric = 1;
				else if (Input.GetKeyDown(KeyCode.Keypad2)) numeric = 2;
				else if (Input.GetKeyDown(KeyCode.Keypad3)) numeric = 3;
				else if (Input.GetKeyDown(KeyCode.Keypad4)) numeric = 4;
				else if (Input.GetKeyDown(KeyCode.Keypad5)) numeric = 5;
				else if (Input.GetKeyDown(KeyCode.Keypad6)) numeric = 6;
				else if (Input.GetKeyDown(KeyCode.Keypad7)) numeric = 7;
				else if (Input.GetKeyDown(KeyCode.Keypad8)) numeric = 8;
				else if (Input.GetKeyDown(KeyCode.Keypad9)) numeric = 9;

				// update
				_codepad.UpdateCodepad(Time.deltaTime, in cycle, numeric, validate, cancel);
			}
			else
			{
				// start
				_codepad.ShowCodepad(playerController.CachedDoorAuthorization, in cycle, OnDoorInteractionSucceed);
			}
		}
		else if (_codepad.IsShown())
		{
			// not interacting with door anymore
			_codepad.ExitScreen();
		}
	}

	private void OnDoorInteractionSucceed()
	{
		// resolve action
		EntityManager entityManager = Utilities.GetEntityManager();
		ActionController controller = entityManager.GetComponentData<ActionController>(_player);
		controller.Action.Time = 0f;
		entityManager.SetComponentData(_player, controller);
	}

	public void	UpdatePeekingBubble(in PeekingInfoComponent info, in CycleComponent cycle)
	{
		Transform bubbleAnchor = PeekingBubble.transform.parent;
		
		if (info.DoorEntity != Entity.Null)
		{
			bubbleAnchor.gameObject.SetActive(true);
			bubbleAnchor.SetPositionAndRotation(new float3(info.Position.x, 4f, info.Position.y), bubbleAnchor.rotation);

			float smoothedRatio = Utilities.SmoothStep(1f - info.DistanceRatio, Const.PeekingBubbleScaleSmoothStart, Const.PeekingBubbleScaleSmoothEnd);
			float scale = math.clamp(Const.PeekingBubbleMinScale + smoothedRatio * (1f - Const.PeekingBubbleMinScale), Const.PeekingBubbleMinScale, 1f);
			bubbleAnchor.localScale = new Vector3(scale, scale, scale);

			BubbleRenderer.material = info.IsPeeking ? BubbleMaterial : DisabledBubbleMaterial;

			char[] digits = cycle.GetCode(info.Authorization).ToString("0000").ToCharArray();
			
			PeekingBubble.text = "" +
				(info.Peeking.FirstDiscovered ? digits[0] : info.Peeking.DigitIndex > 0 ? "?" : "_") +
				(info.Peeking.SecondDiscovered ? digits[1] : info.Peeking.DigitIndex > 1 ? "?" : "_") +
				(info.Peeking.ThirdDiscovered ? digits[2] : info.Peeking.DigitIndex > 2 ? "?" : "_") +
				(info.Peeking.FourthDiscovered ? digits[3] : info.Peeking.DigitIndex > 3 ? "?" : "_");
		}
		else
		{
			bubbleAnchor.gameObject.SetActive(false);
		}
	}
}
