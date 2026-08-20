using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class EchoCoinVisual : MonoBehaviour
{
    private const string MaterialPath = "Materials/EchoCollectible";
    private static readonly int ContractMarkerId =
        Shader.PropertyToID("_ContractMarker");
    private static Mesh _sharedMesh;
    private static Material _sharedMaterial;
    private static bool _ownsMaterial;

    private MeshRenderer _renderer;

    public void Initialize()
    {
        MeshFilter filter = GetComponent<MeshFilter>();
        if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
        _renderer = GetComponent<MeshRenderer>();
        if (_renderer == null) _renderer = gameObject.AddComponent<MeshRenderer>();
        filter.sharedMesh = GetOrCreateMesh();
        _renderer.sharedMaterial = GetOrCreateMaterial();
        _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
    }

    public void SetContractMarker(bool marker)
    {
        if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
        if (_renderer == null) return;
        var properties = new MaterialPropertyBlock();
        _renderer.GetPropertyBlock(properties);
        properties.SetFloat(ContractMarkerId, marker ? 1f : 0f);
        _renderer.SetPropertyBlock(properties);
    }

    private static Mesh GetOrCreateMesh()
    {
        if (_sharedMesh != null) return _sharedMesh;
        var vertices = new List<Vector3>(256);
        var normals = new List<Vector3>(256);
        var colors = new List<Color>(256);
        var triangles = new List<int>(768);
        AppendTorus(vertices, normals, colors, triangles,
            0.43f, 0.085f, 24, 6, new Color(1f, 0f, 0f, 1f));
        AppendCore(vertices, normals, colors, triangles,
            0.31f, 0.12f, 20, new Color(0f, 1f, 0f, 1f));
        _sharedMesh = new Mesh { name = "EchoCollectible_Combined" };
        _sharedMesh.SetVertices(vertices);
        _sharedMesh.SetNormals(normals);
        _sharedMesh.SetColors(colors);
        _sharedMesh.SetTriangles(triangles, 0);
        _sharedMesh.RecalculateBounds();
        _sharedMesh.UploadMeshData(true);
        return _sharedMesh;
    }

    private static Material GetOrCreateMaterial()
    {
        if (_sharedMaterial != null) return _sharedMaterial;
        _sharedMaterial = Resources.Load<Material>(MaterialPath);
        if (_sharedMaterial != null) return _sharedMaterial;
        Shader shader = Shader.Find("EchoRun/Collectible");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;
        _sharedMaterial = new Material(shader)
        {
            name = "EchoCollectible_RuntimeFallback"
        };
        _ownsMaterial = true;
        return _sharedMaterial;
    }

    private static void AppendTorus(List<Vector3> vertices,
        List<Vector3> normals, List<Color> colors, List<int> triangles,
        float majorRadius, float minorRadius, int majorSegments,
        int minorSegments, Color color)
    {
        int start = vertices.Count;
        for (int major = 0; major < majorSegments; major++)
        {
            float majorAngle = major * Mathf.PI * 2f / majorSegments;
            Vector3 radial = new Vector3(Mathf.Cos(majorAngle),
                Mathf.Sin(majorAngle), 0f);
            for (int minor = 0; minor < minorSegments; minor++)
            {
                float minorAngle = minor * Mathf.PI * 2f / minorSegments;
                Vector3 normal = radial * Mathf.Cos(minorAngle)
                                 + Vector3.forward * Mathf.Sin(minorAngle);
                vertices.Add(radial * (majorRadius
                    + minorRadius * Mathf.Cos(minorAngle))
                    + Vector3.forward * (minorRadius * Mathf.Sin(minorAngle)));
                normals.Add(normal.normalized);
                colors.Add(color);
            }
        }
        for (int major = 0; major < majorSegments; major++)
        {
            int nextMajor = (major + 1) % majorSegments;
            for (int minor = 0; minor < minorSegments; minor++)
            {
                int nextMinor = (minor + 1) % minorSegments;
                int a = start + major * minorSegments + minor;
                int b = start + nextMajor * minorSegments + minor;
                int c = start + nextMajor * minorSegments + nextMinor;
                int d = start + major * minorSegments + nextMinor;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(a); triangles.Add(c); triangles.Add(d);
            }
        }
    }

    private static void AppendCore(List<Vector3> vertices,
        List<Vector3> normals, List<Color> colors, List<int> triangles,
        float radius, float halfDepth, int segments, Color color)
    {
        int frontTip = vertices.Count;
        vertices.Add(new Vector3(0f, 0f, -halfDepth));
        normals.Add(Vector3.back);
        colors.Add(color);
        int backTip = vertices.Count;
        vertices.Add(new Vector3(0f, 0f, halfDepth));
        normals.Add(Vector3.forward);
        colors.Add(color);
        int ringStart = vertices.Count;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            vertices.Add(radial * radius);
            normals.Add(radial);
            colors.Add(color);
        }
        for (int i = 0; i < segments; i++)
        {
            int current = ringStart + i;
            int next = ringStart + (i + 1) % segments;
            triangles.Add(frontTip); triangles.Add(next); triangles.Add(current);
            triangles.Add(backTip); triangles.Add(current); triangles.Add(next);
        }
    }

    private void OnDestroy()
    {
        if (_ownsMaterial && _sharedMaterial != null)
        {
            if (Application.isPlaying) Destroy(_sharedMaterial);
            else DestroyImmediate(_sharedMaterial);
            _sharedMaterial = null;
            _ownsMaterial = false;
        }
    }
}
