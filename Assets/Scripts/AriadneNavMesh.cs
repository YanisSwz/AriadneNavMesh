using UnityEngine;

[ExecuteInEditMode]
public class AriadneNavMesh : MonoBehaviour
{
    [SerializeField] GeometryGetter geometryGetter;
    [SerializeField] HeightField heightField;

    private void Update()
    {
        BuildNavMesh();
    }

    public void BuildNavMesh() 
    {
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();

        geometryGetter.GetGeometry();
        heightField.CreateHeightField(geometryGetter.Triangles);

        sw.Stop();
        Debug.Log("\nExecution time:  " + sw.Elapsed.TotalMilliseconds + "ms" +
            "\n" + geometryGetter.VerticesCount + " vertices" +
            "\n" + geometryGetter.Triangles.Count + " triangles"
            );
    }
}
