using System.Runtime.CompilerServices;
using TMPro;
using U0071;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class UIManager : MonoBehaviour
{
	public TMP_Text Interaction;
	public float interactionHeightOffset;

	public void Update()
	{
		EntityManager entityManager = Utilities.GetEntityManager();

		UpdateInteraction(in entityManager);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void UpdateInteraction(in EntityManager entityManager)
	{
		EntityQuery query = new EntityQueryBuilder(Allocator.Temp).WithAll<PlayerController, LocalTransform>().Build(entityManager);
		if (query.HasSingleton<PlayerController>())
		{
			Entity player = query.GetSingletonEntity();
			PlayerController playerController = entityManager.GetComponentData<PlayerController>(player);
			float3 position = entityManager.GetComponentData<LocalTransform>(player).Position;

			string textOne = GetInteractionText(in playerController.PrimaryAction);
			string textTwo = GetInteractionText(in playerController.SecondaryAction);

			Interaction.gameObject.SetActive(playerController.ActionTimer > 0f || textOne != "" || textTwo != "");

			if (Interaction.gameObject.activeSelf)
			{
				Interaction.text = playerController.ActionTimer > 0f ? playerController.ActionTimer.ToString("0.00") : textOne + (textOne != "" ? "\n" : "") + textTwo;
				Interaction.transform.SetLocalPositionAndRotation(position + new float3(0f, 0f, interactionHeightOffset), Interaction.transform.rotation);
			}
		}
		else
		{
			Interaction.gameObject.SetActive(false);
		}
		query.Dispose();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private string GetInteractionText(in ActionInfo info)
	{
		return info.Data.Target != Entity.Null ? 
			info.Key.ToString() + ": " + 
			GetActionTypeName(info.Type, info.Cost) + 
			(info.SecondaryName.Length > 0 ? " " + info.SecondaryName : "") +
			(info.Cost > 0f ? " (-" + info.Cost + "c)" : "") +
			(info.Name.Length > 0 ? " (" + info.Name + ")" : "") : "";
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private string GetActionTypeName(ActionType type, int cost)
	{
		return type switch
		{
			ActionType.Collect => cost > 0f ? "Buy" : "Take",
			ActionType.Eat => "Eat",
			ActionType.Trash => "Destroy",
			ActionType.Pick => "Pick",
			ActionType.Drop => "Drop",
			_ => "none",
		};
	}
}
