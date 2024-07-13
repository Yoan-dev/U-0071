using UnityEngine;
using Random = Unity.Mathematics.Random;

public class ManagedData : MonoBehaviour
{
	public static ManagedData Instance;

	public Color[] SkinColors;
	public Color[] HairColors;
	public string[] StartingLines;
	public string[] RespawnLines;

	private void Awake()
	{
		Instance = this;
	}

	public string GetStartingLine(int iteration)
	{
		return iteration < StartingLines.Length ? StartingLines[iteration] : RespawnLines[(iteration - StartingLines.Length) % RespawnLines.Length];
	}
}
