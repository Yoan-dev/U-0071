using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using static U0071.DebugSystem;
using static UnityEditor.Rendering.FilterWindow;

public class DebugManager : MonoBehaviour
{
	public static DebugManager Instance;

	[Header("Runtime")]
	public bool DebugFoodLevelZeroFlowField;
	public bool DebugDestroyLevelZeroFlowField;

	[Header("Miscellaneous")]	
	public TMP_Text RoomElementPrefab;
	public TMP_Text CellElementPrefab;

	private Dictionary<Entity, TMP_Text> _roomElements;
	private List<TMP_Text> _cellElements;

	public void Awake()
	{
		Instance = this;
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

	public void ClearFlowfieldElements()
	{
		foreach (var pair in _roomElements)
		{
			Destroy(pair.Value);
		}
		_roomElements = null;
	}

	private void SetRoomInfo(TMP_Text element, in RoomInfo info)
	{
		element.text = info.Name + "\n" + info.ElementCount;
	}

	public void UpdateFlowfieldElements(in NativeArray<FlowfieldInfo> flowfieldInfos)
	{
		if (_cellElements == null)
		{
			_cellElements = new List<TMP_Text>();
			for (int i = 0; i < flowfieldInfos.Length; i++)
			{
				FlowfieldInfo info = flowfieldInfos[i];
				TMP_Text element = Instantiate(CellElementPrefab, new Vector3(info.Position.x, 0.5f, info.Position.y), CellElementPrefab.transform.rotation, transform);
				SetFlowfieldInfo(element, info);
				_cellElements.Add(element);
			}
		}
		else
		{
			// we assume there is no cell addition/deletion during playtime
			for (int i = 0; i < flowfieldInfos.Length; i++)
			{
				SetFlowfieldInfo(_cellElements[i], flowfieldInfos[i]);
			}
		}
	}

	public void ClearRoomElements()
	{
		foreach (var element in _cellElements)
		{
			Destroy(element.gameObject);
		}
		_cellElements = null;
	}

	private void SetFlowfieldInfo(TMP_Text element, in FlowfieldInfo info)
	{
		element.text = GetFlowfieldValue(info.Value.x) + "," + GetFlowfieldValue(info.Value.y);
	}

	private string GetFlowfieldValue(float value)
	{
		return value >= int.MaxValue ? "-" : value.ToString();
	}
}
