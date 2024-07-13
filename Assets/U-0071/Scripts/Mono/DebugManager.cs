using System.Collections.Generic;
using TMPro;
using U0071;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using static U0071.DebugSystem;

public class DebugManager : MonoBehaviour
{
	public static DebugManager Instance;

	[Header("Cycle")]
	public TMP_Text CycleElement;

	[Header("Flowfield")]
	public bool ShowToDestroy;
	public bool ShowToProcess;
	public bool ShowToFood;
	public bool ShowToWorkd;
	public bool ShowWander;
	public bool ShowRelax;
	public bool ShowToRed;
	public bool ShowToBlue;
	public bool ShowToYellow;

	[Header("Miscellaneous")]	
	public TMP_Text RoomElementPrefab;

	private Dictionary<Entity, TMP_Text> _roomElements;
	private List<FlowfieldInfo> _flowfieldInfos;

	public void Awake()
	{
		Instance = this;
	}

	private void OnDrawGizmos()
	{
		if (_flowfieldInfos != null && _flowfieldInfos.Count > 0)
		{
			DrawFlowfield();
		}
	}

	public void UpdateRoomElements(in NativeList<RoomInfo> roomInfos)
	{
		if (_roomElements == null)
		{
			_roomElements = new Dictionary<Entity, TMP_Text>();
			foreach (var info in roomInfos)
			{
				TMP_Text element = Instantiate(RoomElementPrefab, new Vector3(info.Position.x, 0.5f, info.Position.y), RoomElementPrefab.transform.rotation, transform);
				SetRoomInfo(element, in info);
				_roomElements.Add(info.Entity, element);
			}
		}
		else
		{
			// we assume there is no room addition/deletion during playtime
			foreach (var info in roomInfos)
			{
				SetRoomInfo(_roomElements[info.Entity], in info);
			}
		}
	}

	public void ClearRoomElements()
	{
		foreach (var pair in _roomElements)
		{
			Destroy(pair.Value);
		}
		_roomElements = null;
	}

	private void SetRoomInfo(TMP_Text element, in RoomInfo info)
	{
		element.text = info.Name + "\nCapacity: " + info.Capacity + "\nPopulation: " + info.Population + "\nElement count: " + info.ElementCount;
	}

	public void UpdateCycleElement(in CycleComponent cycle)
	{
		if (!CycleElement.gameObject.transform.parent.gameObject.activeSelf)
		{
			CycleElement.gameObject.transform.parent.gameObject.SetActive(true);
		}
		CycleElement.text =
			cycle.CycleTimer.ToString("0.00") + " - Cycle " + cycle.CycleCounter + "\n" +
			cycle.LevelOneCode.ToString("0000") + " - LVL1\n" +
			cycle.LevelTwoCode.ToString("0000") + " - LVL2\n" +
			cycle.LevelThreeCode.ToString("0000") + " - LVL3\n" +
			cycle.RedCode.ToString("0000") + " - RED\n" +
			cycle.BlueCode.ToString("0000") + " - BLUE\n" +
			cycle.YellowCode.ToString("0000") + " - YELLOW\n";
	}

	public void ClearCycleElement()
	{
		CycleElement.text = "";
		CycleElement.gameObject.transform.parent.gameObject.SetActive(false);
	}

	public void UpdateFlowfieldElements(in NativeArray<FlowfieldInfo> flowfieldInfos)
	{
		_flowfieldInfos = new List<FlowfieldInfo>(flowfieldInfos);
	}

	private void DrawFlowfield()
	{
		foreach (var info in _flowfieldInfos)
		{
			Vector3 from = new Vector3(info.Position.x, 0.5f, info.Position.y);
			if (ShowToDestroy || ShowToRed) TryDraw(Color.red, from, info.ToDestroyAndRed);
			if (ShowToFood) TryDraw(Color.green, from, info.Food);
			if (ShowToWorkd || ShowToBlue) TryDraw(Color.blue, from, info.WorkdAndBlue);
			if (ShowToProcess || ShowToYellow) TryDraw(Color.yellow, from, info.ProcessAndYellow);
			if (ShowWander) TryDraw(Color.cyan, from, info.Wander);
			if (ShowRelax) TryDraw(Color.magenta, from, info.Relax);
		}
	}

	private void TryDraw(Color color, Vector3 from, float2 direction)
	{
		if (!direction.Equals(float2.zero))
		{
			Gizmos.color = color;
			Gizmos.DrawLine(from, from + new Vector3(direction.x, 0f, direction.y) / 2f);
		}
	}

	public void ClearFlowfieldElements()
	{
		_flowfieldInfos.Clear();
	}
}
