using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(BoxCollider))]
public class HeightField : MonoBehaviour
{
    public int cellCount = 10;
    public int cellCountX = 10;
    public int cellCountZ = 10;
    public float cellSize = 1;
    public float cellHeight = 2;
    public Vector3 minBounds = Vector3.zero;
    public Vector3 maxBounds = Vector3.zero;
    private BoxCollider boxCollider = new BoxCollider();

    public void Start()
    {
        boxCollider = GetComponent<BoxCollider>();
    }

    public void Update()
    {
        minBounds = boxCollider.bounds.min;
        maxBounds = boxCollider.bounds.max;
        cellCountX = (int)(boxCollider.size.x / cellSize);
        cellCountZ = (int)(boxCollider.size.z / cellSize);
        cellCount = cellCountX * cellCountZ;
    }

    public void OnDrawGizmos()
    {
        Vector3 center = Vector3.zero;
        for(int i = 0; i < cellCount; ++i) 
        {
            center = new Vector3(cellSize / 2f + cellSize * (i / cellCountZ), cellHeight/2f, cellSize / 2f + cellSize * (i % cellCountZ)) + boxCollider.transform.position - new Vector3(cellCountX * cellSize/2f, boxCollider.size.y/2f, cellCountZ * cellSize / 2f);
            Gizmos.DrawWireCube(center, new Vector3(cellSize, cellHeight, cellSize));
        }
    }
}
