using System.Runtime.CompilerServices;
using TMPro;
using U0071;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.VisualScripting.FullSerializer;
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
	public Material BustedBubbleMaterial;

	[Header("Player Lines")]
	public bool DisableIntro;
	public float StartLineTime;
	public float StayLineTime;
	public float RespawnLineTime;
	public float _playerLineTimer;
	public float _usedLineTime;
	public bool _saidStartingLine;
	public bool _initialized;

	// would have been cleaner to do a generic line saying call with timer management
	[Header("Ending")]
	public float EndingLineTime;
	private float _endingLineTimer;

	[Header("Go Crazy")]
	public float GoCrazyFirstStartTime;
	public float GoCrazyRespawnStartTime;
	public float _goCrazyTimer;
	public float _usedGoCrazyTime;
	public float _goCrazyHungerTimer = 0f;
	public float _goCrazyCreditsTimer = 0.5f;
	public float _goCrazyCycleTimer = 2f;
	public float _goCrazyCodeTimer = 1.5f;
	public float _goCrazyInteractionTimer = 1f;
	public float _goCrazyRandomTextTimer = 1f;

	// ECS
	private Entity _player;
	private Entity _gameSingleton;

	// HUD
	private VisualElement _root;
	private VisualElement _fadeScreen;
	private Label _hungerLabel;
	private Label _contaminationLabel;
	private Label _creditsLabel;
	private Label _cycleLabel;
	private Label _codeLabel;

	// codepad
	private Codepad _codepad;

	// miscellaneous
	private int _lastCreditsValue;

	// death random effects
	private int _goCrazyHungerInc = 1;
	private int _goCrazyCreditsInc = 100000;
	private int _goCrazyCycleInc = 200000;
	private int _goCrazyCodeInc = 300000;
	private int _goCrazyInteractionInc = 400000;

	// ending
	private bool _endingPhaseOneProcessed;
	private bool _endingPhaseTwoProcessed;
	private bool _endingPhaseThreeProcessed;

	private void OnEnable()
	{
		_root = GetComponent<UIDocument>().rootVisualElement;
		_hungerLabel = _root.Q<Label>("info_hunger");
		_contaminationLabel = _root.Q<Label>("info_contamination");
		_creditsLabel = _root.Q<Label>("info_credits");
		_cycleLabel = _root.Q<Label>("info_cycle");
		_codeLabel = _root.Q<Label>("info_code");
		_fadeScreen = _root.Q<VisualElement>("fadescreen");
		_codepad = new Codepad(_root.Q<VisualElement>("codepad"));
	}

	private void Start()
	{
		if (DisableIntro)
		{
			_hungerLabel.style.display = DisplayStyle.Flex;
			_contaminationLabel.style.display = DisplayStyle.Flex;
			_creditsLabel.style.display = DisplayStyle.Flex;
			_cycleLabel.style.display = DisplayStyle.Flex;
			_codeLabel.style.display = DisplayStyle.Flex;
			_fadeScreen.style.display = DisplayStyle.None;
		}
		else
		{
			_hungerLabel.style.display = DisplayStyle.None;
			_contaminationLabel.style.display = DisplayStyle.None;
			_creditsLabel.style.display = DisplayStyle.None;
			_cycleLabel.style.display = DisplayStyle.None;
			_codeLabel.style.display = DisplayStyle.None;
			_fadeScreen.style.display = DisplayStyle.Flex;
			Interaction.gameObject.SetActive(false);
			PeekingBubble.transform.parent.gameObject.SetActive(false);
		}
	}

	public void Update()
	{
		EntityManager entityManager = Utilities.GetEntityManager();

		if (_player == Entity.Null)
		{
			EntityQuery query = new EntityQueryBuilder(Allocator.Temp).WithAll<PlayerController>().Build(entityManager);
			if (query.HasSingleton<PlayerController>())
			{
				_player = query.GetSingletonEntity();
			}
		}
		if (_gameSingleton == Entity.Null)
		{
			EntityQuery query = new EntityQueryBuilder(Allocator.Temp).WithAll<CycleComponent>().Build(entityManager);
			if (query.HasSingleton<PlayerController>())
			{
				_gameSingleton = query.GetSingletonEntity();
			}
		}
		if (_player != Entity.Null && _gameSingleton != Entity.Null)
		{
			float3 position = entityManager.GetComponentData<LocalTransform>(_player).Position;
			if (!DisableIntro && _playerLineTimer < StartLineTime + StayLineTime)
			{
				Config config = entityManager.GetComponentData<Config>(_gameSingleton);
				if (!_initialized)
				{
					_initialized = true;
					_usedLineTime = GameSimulationSystem.CachedIteration == 0 ? StartLineTime : RespawnLineTime;
					_usedGoCrazyTime = GameSimulationSystem.CachedIteration == 0 ? GoCrazyFirstStartTime : GoCrazyRespawnStartTime;
					_fadeScreen.style.display = GameSimulationSystem.CachedIteration == 0 ? DisplayStyle.Flex : DisplayStyle.None;
				}
				if (_initialized)
				{
					TickPlayerSpawn(in config, position);
				}
			}
			else
			{
				Ending ending = entityManager.HasComponent<Ending>(_gameSingleton) ? entityManager.GetComponentData<Ending>(_gameSingleton) : new Ending();
				if (ending.PhaseOneTriggered)
				{
					if (!_endingPhaseOneProcessed)
					{
						_endingPhaseOneProcessed = true;
						_hungerLabel.style.display = DisplayStyle.None;
						_contaminationLabel.style.display = DisplayStyle.None;
						_creditsLabel.style.display = DisplayStyle.None;
						_cycleLabel.style.display = DisplayStyle.None;
						_codeLabel.style.display = DisplayStyle.None;

						_endingLineTimer = EndingLineTime;
						Interaction.text = ManagedData.Instance.GetEndingLine(0);
						Interaction.gameObject.SetActive(true);
					}
					else if (ending.PhaseTwoTriggered && !_endingPhaseTwoProcessed)
					{
						_endingPhaseTwoProcessed = true;

						_endingLineTimer = EndingLineTime;
						Interaction.color = Color.black;
						Interaction.text = ManagedData.Instance.GetEndingLine(1);
						Interaction.gameObject.SetActive(true);
					}
					else if (ending.PhaseThreeTriggered && !_endingPhaseThreeProcessed)
					{
						_endingPhaseThreeProcessed = true;

						_endingLineTimer = EndingLineTime;
						Interaction.text = ManagedData.Instance.GetEndingLine(2);
						Interaction.gameObject.SetActive(true);
					}

					if (_endingLineTimer > 0)
					{
						_endingLineTimer -= Time.deltaTime;
						Interaction.transform.SetLocalPositionAndRotation(new float3(position.x, position.y + 5f, position.z + HeightOffset), Interaction.transform.rotation);
					}
					else
					{
						Interaction.gameObject.SetActive(false);
					}
				}
				else if (entityManager.IsComponentEnabled<DeathComponent>(_player))
				{
					GoCrazy(GameSimulationSystem.CachedIteration, position);
				}
				else
				{
					PlayerController playerController = entityManager.GetComponentData<PlayerController>(_player);
					ActionController actionController = entityManager.GetComponentData<ActionController>(_player);
					CreditsComponent credits = entityManager.GetComponentData<CreditsComponent>(_player);
					HungerComponent hunger = entityManager.GetComponentData<HungerComponent>(_player);
					ContaminationLevelComponent contaminationLevel = entityManager.GetComponentData<ContaminationLevelComponent>(_player);
					PeekingInfoComponent peekingInfo = entityManager.GetComponentData<PeekingInfoComponent>(_player);
					CycleComponent cycle = entityManager.GetComponentData<CycleComponent>(_gameSingleton);

					UpdateInteraction(in playerController, position);
					UpdateHUD(in credits, in hunger, in contaminationLevel, in cycle);
					ProcessPopEvents(in credits, position);
					UpdateCodepad(in entityManager, in playerController, in actionController, in cycle);
					UpdatePeekingBubble(in peekingInfo, in cycle);
				}
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void UpdateHUD(in CreditsComponent credits, in HungerComponent hunger, in ContaminationLevelComponent contaminationLevel, in CycleComponent cycle)
	{
		_hungerLabel.text = "Hunger " + (hunger.Value > 1f ? hunger.Value.ToString("0") : hunger.Value.ToString("0.0"));
		_contaminationLabel.text = "Contamination " + (contaminationLevel.Value > 1f || contaminationLevel.Value == 0f ? contaminationLevel.Value.ToString("0") : contaminationLevel.Value.ToString("0.0"));
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
			ActionFlag.Contaminate => "Contaminate",
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
					controller.Stop(false, false);
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

			BubbleRenderer.material = 
				info.IsPeeking ? info.Peeking.Suspicion >= Const.PeekingBustedFeedbackTreshold ? BustedBubbleMaterial : BubbleMaterial : DisabledBubbleMaterial;

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

	private void TickPlayerSpawn(in Config config, float3 position)
	{
		_playerLineTimer += Time.deltaTime;
		if (!_saidStartingLine && _playerLineTimer > _usedLineTime)
		{
			Interaction.text = ManagedData.Instance.GetStartingLine(config.Iteration);
			Interaction.gameObject.SetActive(true);
			_saidStartingLine = true;
			_fadeScreen.style.display = DisplayStyle.None;
		}
		else if (_saidStartingLine && _playerLineTimer > _usedLineTime + StayLineTime)
		{
			Interaction.text = "";
			Interaction.gameObject.SetActive(false);

			_hungerLabel.style.display = DisplayStyle.Flex;
			_contaminationLabel.style.display = DisplayStyle.Flex;
			_creditsLabel.style.display = DisplayStyle.Flex;
			_cycleLabel.style.display = DisplayStyle.Flex;
			_codeLabel.style.display = DisplayStyle.Flex;
		}
		else if (_fadeScreen.style.display == DisplayStyle.Flex)
		{
			_fadeScreen.style.opacity = Utilities.EaseOutCubic(1f - (_playerLineTimer / StartLineTime), 3f);
		}
		Interaction.transform.SetLocalPositionAndRotation(new float3(position.x, position.y + 5f, position.z + HeightOffset), Interaction.transform.rotation);
	}

	private void GoCrazy(int iteration, float3 position)
	{
		_goCrazyTimer += Time.deltaTime;
		if (_goCrazyTimer > _usedGoCrazyTime)
		{
			_contaminationLabel.style.display = DisplayStyle.None;

			TickGoCrazyLabel(iteration, ref _goCrazyHungerInc, _hungerLabel, ref _goCrazyHungerTimer);
			TickGoCrazyLabel(iteration, ref _goCrazyCreditsInc, _creditsLabel, ref _goCrazyCreditsTimer);
			TickGoCrazyLabel(iteration, ref _goCrazyCycleInc, _cycleLabel, ref _goCrazyCycleTimer);
			TickGoCrazyLabel(iteration, ref _goCrazyCodeInc, _codeLabel, ref _goCrazyCodeTimer);

			_goCrazyInteractionTimer -= Time.deltaTime;
			if (_goCrazyInteractionTimer <= 0f)
			{
				_goCrazyInteractionTimer = _goCrazyRandomTextTimer;
				Interaction.text = ManagedData.Instance.GetCrazyDeathLine(iteration, _goCrazyInteractionInc);
				Interaction.transform.SetLocalPositionAndRotation(new float3(position.x, position.y + 5f, position.z + HeightOffset), Interaction.transform.rotation);
				_goCrazyInteractionInc += 33333;
				Interaction.gameObject.SetActive(true);
			}
		}
	}

	private void TickGoCrazyLabel(int iteration, ref int inc, Label label, ref float timer)
	{
		timer -= Time.deltaTime;
		if (timer <= 0f)
		{
			timer = _goCrazyRandomTextTimer;
			label.text = ManagedData.Instance.GetCrazyDeathLine(iteration, inc);
			inc += 33333;
		}
	}
}
