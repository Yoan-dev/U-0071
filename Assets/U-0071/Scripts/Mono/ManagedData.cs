using UnityEngine;

public class ManagedData : MonoBehaviour
{
	public static ManagedData Instance;

	public Color[] SkinColors;
	public Color[] HairColors;

	private void Awake()
	{
		Instance = this;
	}
}
