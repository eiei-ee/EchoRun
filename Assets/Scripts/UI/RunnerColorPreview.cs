using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Render the existing runner only; do not clone, replace or re-rig the character.
[RequireComponent(typeof(RawImage))]
public sealed class RunnerColorPreview : MonoBehaviour
{
    private const int PreviewLayer = 30;
    private readonly Dictionary<GameObject, int> _layers = new Dictionary<GameObject, int>();
    private Transform _model;
    private Camera _camera;
    private Light _light;
    private RenderTexture _texture;

    void LateUpdate()
    {
        if (_model == null)
        {
            GameObject player = GameObject.Find("player");
            _model = player != null ? player.transform.Find("CharacterModel") : null;
            if (_model == null) return;
            foreach (Transform child in _model.GetComponentsInChildren<Transform>(true))
            {
                _layers[child.gameObject] = child.gameObject.layer;
                child.gameObject.layer = PreviewLayer;
            }
            _texture = new RenderTexture(512, 640, 24);
            GetComponent<RawImage>().texture = _texture;
            _camera = new GameObject("RunnerPreviewCamera").AddComponent<Camera>();
            _camera.enabled = false;
            _camera.cullingMask = 1 << PreviewLayer;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = EchoRunUITheme.Backdrop;
            _camera.targetTexture = _texture;
            _camera.fieldOfView = 32f;
            _camera.nearClipPlane = 0.05f;
            _camera.farClipPlane = 30f;
            _light = new GameObject("RunnerPreviewLight").AddComponent<Light>();
            _light.type = LightType.Directional;
            _light.cullingMask = 1 << PreviewLayer;
            _light.intensity = 1.6f;
            _light.color = new Color(0.85f, 0.92f, 1f);
            _light.transform.rotation = Quaternion.Euler(30f, 160f, 0f);
        }
        Renderer[] renderers = _model.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        Vector3 direction = (_model.forward + _model.right * 0.2f).normalized;
        _camera.transform.position = bounds.center + direction * Mathf.Max(2f, bounds.size.y * 2.1f);
        _camera.transform.LookAt(bounds.center);
        _camera.Render();
    }

    void OnDisable()
    {
        foreach (var entry in _layers)
            if (entry.Key != null) entry.Key.layer = entry.Value;
        _layers.Clear();
        _model = null;
        if (_camera != null) Destroy(_camera.gameObject);
        if (_light != null) Destroy(_light.gameObject);
        GetComponent<RawImage>().texture = null;
        if (_texture != null) { _texture.Release(); Destroy(_texture); }
    }
}
