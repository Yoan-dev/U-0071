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
	public bool DebugDestroyFlowField;

	[Header("Miscellaneous")]	
	public TMP_Text RoomElementPrefab;
	public TMP_Text CellElementPrefab;

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
		element.text = info.Name + "\n" + info.ElementCount;
	}

	public void UpdateFlowfieldElements(in NativeArray<FlowfieldInfo> flowfieldInfos)
	{
		_flowfieldInfos = new List<FlowfieldInfo>(flowfieldInfos);
	}

	public void DrawFlowfield()
	{
		foreach (var info in _flowfieldInfos)
		{
			Vector3 from = new Vector3(info.Position.x, 0.5f, info.Position.y);
			Gizmos.color = Color.red;
			Gizmos.DrawLine(from, from + new Vector3(info.Value.x, 0.5f, info.Value.y) / 2f);
		}
	}

	public void ClearFlowfieldElements()
	{
		_flowfieldInfos.Clear();
	}
}
