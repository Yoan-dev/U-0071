using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using static U0071.DebugSystem;

public class DebugManager : MonoBehaviour
{
	public static DebugManager Instance;
	
	public TMP_Text ElementPrefab;

	private Dictionary<Entity, TMP_Text> _roomElements;

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
				TMP_Text element = Instantiate(ElementPrefab, new Vector3(info.Position.x, 0.3f, info.Position.y), ElementPrefab.transform.rotation, transform);
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
}
