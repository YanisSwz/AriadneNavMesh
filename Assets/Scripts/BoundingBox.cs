using UnityEngine;

[ExecuteInEditMode]
public class BoundingBox : MonoBehaviour
{
    [SerializeField] Transform point1;
    [SerializeField] Transform point2;
    [SerializeField] Transform point3;

    private Vector3 minAABB = Vector3.zero;
    private Vector3 maxAABB = Vector3.zero;
    private void GetBoundingBox() 
    {
        minAABB.x = Mathf.Min(point1.position.x, Mathf.Min(point2.position.x, point3.position.x));
        minAABB.y = Mathf.Min(point1.position.y, Mathf.Min(point2.position.y, point3.position.y));
        minAABB.z = Mathf.Min(point1.position.z, Mathf.Min(point2.position.z, point3.position.z));

        maxAABB.x = Mathf.Max(point1.position.x, Mathf.Max(point2.position.x, point3.position.x));
        maxAABB.y = Mathf.Max(point1.position.y, Mathf.Max(point2.position.y, point3.position.y));
        maxAABB.z = Mathf.Max(point1.position.z, Mathf.Max(point2.position.z, point3.position.z));
    }

    private void Update()
    {
        if(point1 && point2 && point3)
            GetBoundingBox();
    }

    private void OnDrawGizmos()
    {
        Gizmos.DrawWireCube((minAABB + maxAABB)/2f, maxAABB - minAABB);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(point1.position, point2.position);
        Gizmos.DrawLine(point2.position, point3.position);
        Gizmos.DrawLine(point3.position, point1.position);
        Gizmos.color = Color.white;
    }
}
