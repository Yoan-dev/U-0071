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

			string textOne = GetInteractionText(in playerController.PrimaryInfo);
			string textTwo = GetInteractionText(in playerController.SecondaryInfo);

			Interaction.gameObject.SetActive(textOne != "" || textTwo != "");

			if (Interaction.gameObject.activeSelf)
			{
				Interaction.text = textOne + (textOne != "" ? "\n" : "") + textTwo;
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
	private string GetInteractionText(in ActionInfo interactionInfo)
	{
		return interactionInfo.Type != 0 ? interactionInfo.Key.ToString() + ": " + GetActionTypeName(interactionInfo.Type) + " (" + interactionInfo.Name + ")" : "";
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private string GetActionTypeName(ActionType type)
	{
		return type switch
		{
			ActionType.Grind => "Grind",
			ActionType.Pick => "Pick",
			ActionType.Drop => "Drop",
			_ => "none",
		};
	}
}
