using UnityEngine;

public class CapturedArea : MonoBehaviour
{
	public PlayerController character;
	public MeshCollider meshCollider;

	private void Awake()
	{
		meshCollider = gameObject.AddComponent<MeshCollider>();
	}
}