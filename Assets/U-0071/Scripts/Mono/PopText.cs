using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
	public float LifeTime;
	public float Speed;

	private void Update()
	{
		transform.Translate(0f, Time.deltaTime * Speed, 0f);
		LifeTime -= Time.deltaTime;
		if (LifeTime <= 0f)
		{
			Destroy(gameObject);
		}
	}
}