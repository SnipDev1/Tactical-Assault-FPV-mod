using System;
using UnityEngine;
using TacticalAssaultFPV.Config;

namespace TacticalAssaultFPV.Drone;

public class DroneCameraRig : MonoBehaviour
{
    public DroneCameraRig(IntPtr ptr) : base(ptr) { }

    private GameObject _camObj;
    private Camera _cam;
    private RenderTexture _renderTexture;

    public RenderTexture StreamTexture => _renderTexture;
    public float CurrentTiltAngle { get; private set; } = ModConfig.CameraTiltAngleDeg;
    public bool IsStreaming => _cam != null && _cam.enabled;

    public void Awake()
    {
        _camObj = new GameObject("FPV_Camera");
        _camObj.transform.SetParent(transform, false);
        _camObj.transform.localPosition = new Vector3(0f, 0.05f, 0.12f);
        _camObj.transform.localRotation = Quaternion.Euler(-CurrentTiltAngle, 0f, 0f);

        _cam = _camObj.AddComponent<Camera>();
        _cam.fieldOfView = 115f;
        _cam.nearClipPlane = 0.05f;
        _cam.farClipPlane = 1000f;
        _cam.depth = -10;
        _cam.aspect = 1.6f;
        _cam.clearFlags = CameraClearFlags.Skybox;
        _cam.backgroundColor = new Color(0.12f, 0.14f, 0.18f);

        int width = ModConfig.RenderTextureResolution;
        int height = Mathf.RoundToInt(width * (10f / 16f));
        _renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 2,
            filterMode = FilterMode.Bilinear,
            name = "FPV_Drone_Feed"
        };
        _renderTexture.Create();

        _cam.targetTexture = _renderTexture;
        _cam.enabled = false; // Only active when tablet connects to this drone!
    }

    public void SetStreaming(bool active)
    {
        if (_cam != null)
        {
            _cam.enabled = active;
        }
    }

    public void AdjustTilt(float deltaDegrees)
    {
        CurrentTiltAngle = Mathf.Clamp(CurrentTiltAngle + deltaDegrees, -15f, 75f);
        if (_camObj != null)
        {
            _camObj.transform.localRotation = Quaternion.Euler(-CurrentTiltAngle, 0f, 0f);
        }
    }

    public void OnDestroy()
    {
        if (_renderTexture != null)
        {
            if (RenderTexture.active == _renderTexture)
            {
                RenderTexture.active = null;
            }
            if (_cam != null)
            {
                _cam.targetTexture = null;
            }
            _renderTexture.Release();
            Destroy(_renderTexture);
            _renderTexture = null;
        }
    }
}
