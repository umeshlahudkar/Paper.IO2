using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PlayerController : MonoBehaviour
{
	[Header("Reference")]
	[SerializeField] private Rigidbody rb;
	[SerializeField] private Transform thisTransform;
	[SerializeField] private TrailRenderer trail;
	[SerializeField] private Color color;
	[SerializeField] private Material material;

	[Header("Movement")]
	[SerializeField] private float moveSpeed = 5f;
	[SerializeField] private float rotationSpeed = 125f;

	[Header("Captured Area")]
	[SerializeField] private int initialVertices = 45;
	[SerializeField] private float initialAreaRadius = 3f;
	[SerializeField] private float minVertexPointDistance = 0.1f;
	private CapturedArea capturedArea;
	private GameObject capturedAreaOutline;
	public List<Vector3> capturedAreaVertices = new List<Vector3>();
	private List<Vector3> newCapturedAreaVertices = new List<Vector3>();
	private MeshRenderer capturedAreaMeshRend;
	private MeshFilter capturedAreaFilter;
	private MeshRenderer capturedAreaOutlineMeshRend;
	private MeshFilter capturedAreaOutlineFilter;
	
	private GameObject trailCollidersHolder;
	private List<SphereCollider> trailColliders = new List<SphereCollider>();
	
	private Vector3 currentDirection;
	private Quaternion targetRotation;

	private void Awake()
	{
		trail.material.color = new Color(color.r, color.g, color.b);
		GetComponent<MeshRenderer>().material.color = new Color(color.r, color.g, color.b);
	}

    private void Start()
    {
		InitializePlayer();
    }

    private void Update()
	{
	    currentDirection = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")).normalized;

		Vector3 transPos = thisTransform.position;
		bool isOutside = !IsPointInPolygon(new Vector2(transPos.x, transPos.z), Vertices2D(capturedAreaVertices));
		int count = newCapturedAreaVertices.Count;

		if (isOutside)
		{
			if (count == 0 || !newCapturedAreaVertices.Contains(transPos) && (newCapturedAreaVertices[count - 1] - transPos).magnitude >= minVertexPointDistance)
			{
				count++;
				newCapturedAreaVertices.Add(transPos);

				int trailCollsCount = trailColliders.Count;
				float trailWidth = trail.startWidth;
				SphereCollider lastColl = trailCollsCount > 0 ? trailColliders[trailCollsCount - 1] : null;
				if (!lastColl || (transPos - lastColl.center).magnitude > trailWidth)
				{
					SphereCollider trailCollider = trailCollidersHolder.AddComponent<SphereCollider>();
					trailCollider.center = transPos;
					trailCollider.radius = trailWidth / 2f;
					trailCollider.isTrigger = true;
					trailCollider.enabled = false;
					trailColliders.Add(trailCollider);

					if (trailCollsCount > 1)
					{
						trailColliders[trailCollsCount - 2].enabled = true;
					}
				}
			}

			if (!trail.emitting)
			{
				trail.Clear();
				trail.emitting = true;
			}
		}
		else if (count > 0)
		{
			DeformCharacterArea();
			newCapturedAreaVertices.Clear();

			if (trail.emitting)
			{
				trail.Clear();
				trail.emitting = false;
			}
			foreach (var trailColl in trailColliders)
			{
				Destroy(trailColl);
			}
			trailColliders.Clear();
		}
	}

	private void FixedUpdate()
	{
		rb.MovePosition(rb.position + transform.forward * moveSpeed * Time.fixedDeltaTime);

		if (currentDirection != Vector3.zero)
		{
			targetRotation = Quaternion.LookRotation(currentDirection);
			rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
		}
	}

	private void InitializePlayer()
	{
		capturedArea = new GameObject().AddComponent<CapturedArea>();
		capturedArea.name = "CapturedArea";
		capturedArea.character = this;
		Transform areaTrans = capturedArea.transform;
		capturedAreaFilter = capturedArea.gameObject.AddComponent<MeshFilter>();
		capturedAreaMeshRend = capturedArea.gameObject.AddComponent<MeshRenderer>();
		capturedAreaMeshRend.material = material;
		capturedAreaMeshRend.material.color = color;

		capturedAreaOutline = new GameObject();
		capturedAreaOutline.name = "CapturedAreaOutline";
		Transform areaOutlineTrans = capturedAreaOutline.transform;
		areaOutlineTrans.position += new Vector3(0, -0.495f, -0.1f);
		areaOutlineTrans.SetParent(areaTrans);
		capturedAreaOutlineFilter = capturedAreaOutline.AddComponent<MeshFilter>();
		capturedAreaOutlineMeshRend = capturedAreaOutline.AddComponent<MeshRenderer>();
		capturedAreaOutlineMeshRend.material = material;
		capturedAreaOutlineMeshRend.material.color = new Color(color.r * .7f, color.g * .7f, color.b * .7f);

		float step = 360f / initialVertices;
		for (int i = 0; i < initialVertices; i++)
		{
			capturedAreaVertices.Add(transform.position + Quaternion.Euler(new Vector3(0, step * i, 0)) * Vector3.forward * initialAreaRadius);
		}

		UpdateCapturedArea();

		trailCollidersHolder = new GameObject();
		trailCollidersHolder.transform.SetParent(areaTrans);
		trailCollidersHolder.name = "TrailCollidersHolder";
		trailCollidersHolder.layer = 8;
	}

	private void UpdateCapturedArea()
	{
		if (capturedAreaFilter)
		{
			Mesh areaMesh = GenerateMesh(capturedAreaVertices);
			capturedAreaFilter.mesh = areaMesh;
			capturedAreaOutlineFilter.mesh = areaMesh;
			capturedArea.meshCollider.sharedMesh = areaMesh;
		}
	}

	private Mesh GenerateMesh(List<Vector3> vertices)
	{
		Triangulator tr = new Triangulator(Vertices2D(vertices));
		int[] indices = tr.Triangulate();

		Mesh msh = new Mesh();
		msh.vertices = vertices.ToArray();
		msh.triangles = indices;
		msh.RecalculateNormals();
		msh.RecalculateBounds();
		msh.name ="Mesh";

		return msh;
	}

	private Vector2[] Vertices2D(List<Vector3> vertices)
	{
		List<Vector2> areaVertices2D = new List<Vector2>();
		foreach (Vector3 vertex in vertices)
		{
			areaVertices2D.Add(new Vector2(vertex.x, vertex.z));
		}

		return areaVertices2D.ToArray();
	}

	private int GetClosestAreaVertice(Vector3 fromPos)
	{
		int closest = -1;
		float closestDist = Mathf.Infinity;
		for (int i = 0; i < capturedAreaVertices.Count; i++)
		{
			float dist = (capturedAreaVertices[i] - fromPos).magnitude;
			if (dist < closestDist)
			{
				closest = i;
				closestDist = dist;
			}
		}

		return closest;
	}

	private bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
	{
		int polygonLength = polygon.Length, i = 0;
		bool inside = false;
		float pointX = point.x, pointY = point.y;
		float startX, startY, endX, endY;
		Vector2 endPoint = polygon[polygonLength - 1];
		endX = endPoint.x;
		endY = endPoint.y;
		while (i < polygonLength)
		{
			startX = endX; startY = endY;
			endPoint = polygon[i++];
			endX = endPoint.x; endY = endPoint.y;
			inside ^= (endY > pointY ^ startY > pointY) && ((pointX - endX) < (pointY - endY) * (startX - endX) / (startY - endY));
		}
		return inside;
	}

	private void DeformCharacterArea()
	{
		int newAreaVerticesCount = newCapturedAreaVertices.Count;
		if (newAreaVerticesCount > 0)
		{
			List<Vector3> areaVertices = capturedAreaVertices;
			int startPoint = GetClosestAreaVertice(newCapturedAreaVertices[0]);
			int endPoint = GetClosestAreaVertice(newCapturedAreaVertices[newAreaVerticesCount - 1]);

			// CLOCKWISE AREA
			// Select redundant vertices
			List<Vector3> redundantVertices = new List<Vector3>();
			for (int i = startPoint; i != endPoint; i++)
			{
				if (i == areaVertices.Count)
				{
					if (endPoint == 0)
					{
						break;
					}

					i = 0;
				}
				redundantVertices.Add(areaVertices[i]);
			}
			redundantVertices.Add(areaVertices[endPoint]);

			// Add new vertices to clockwise temp area
			List<Vector3> tempAreaClockwise = new List<Vector3>(areaVertices);
			for (int i = 0; i < newAreaVerticesCount; i++)
			{
				tempAreaClockwise.Insert(i + startPoint, newCapturedAreaVertices[i]);
			}

			// Remove the redundat vertices & calculate clockwise area's size
			tempAreaClockwise = tempAreaClockwise.Except(redundantVertices).ToList();
			float clockwiseArea = Mathf.Abs(tempAreaClockwise.Take(tempAreaClockwise.Count - 1).Select((p, i) => (tempAreaClockwise[i + 1].x - p.x) * (tempAreaClockwise[i + 1].z + p.z)).Sum() / 2f);

			// COUNTERCLOCKWISE AREA
			// Select redundant vertices
			redundantVertices.Clear();
			for (int i = startPoint; i != endPoint; i--)
			{
				if (i == -1)
				{
					if (endPoint == areaVertices.Count - 1)
					{
						break;
					}

					i = areaVertices.Count - 1;
				}
				redundantVertices.Add(areaVertices[i]);
			}
			redundantVertices.Add(areaVertices[endPoint]);

			// Add new vertices to clockwise temp area
			List<Vector3> tempAreaCounterclockwise = new List<Vector3>(areaVertices);
			for (int i = 0; i < newAreaVerticesCount; i++)
			{
				tempAreaCounterclockwise.Insert(startPoint, newCapturedAreaVertices[i]);
			}

			// Remove the redundant vertices & calculate counterclockwise area's size
			tempAreaCounterclockwise = tempAreaCounterclockwise.Except(redundantVertices).ToList();
			float counterclockwiseArea = Mathf.Abs(tempAreaCounterclockwise.Take(tempAreaCounterclockwise.Count - 1).Select((p, i) => (tempAreaCounterclockwise[i + 1].x - p.x) * (tempAreaCounterclockwise[i + 1].z + p.z)).Sum() / 2f);

			// Find the area with greatest size
			capturedAreaVertices = clockwiseArea > counterclockwiseArea ? tempAreaClockwise : tempAreaCounterclockwise;
		}

		UpdateCapturedArea();
	}
}
