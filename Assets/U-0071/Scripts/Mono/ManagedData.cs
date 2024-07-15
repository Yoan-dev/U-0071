using UnityEngine;
using Random = Unity.Mathematics.Random;

public class ManagedData : MonoBehaviour
{
	public static ManagedData Instance;

	public Color[] SkinColors;
	public Color[] HairColors;
	public string[] StartingLines;
	public string[] RespawnLines;
	public string[] GoCrazyDeathLines;
	public string[] EndingLines;

	private void Awake()
	{
		Instance = this;
	}

	public string GetStartingLine(int iteration)
	{
		return iteration < StartingLines.Length ? StartingLines[iteration] : RespawnLines[(iteration - StartingLines.Length) % RespawnLines.Length];
	}

	public string GetCrazyDeathLine(int iteration, int inc)
	{
		return GoCrazyDeathLines[new Random((uint)(iteration + inc)).NextInt(0, GoCrazyDeathLines.Length)];
	}

	public string GetEndingLine(int phase)
	{
		return EndingLines[phase];
	}
}
