using System;
using UnityEngine;
using Il2Cpp;
using Il2CppHurricaneVR.Framework.Core.Player;

namespace TacticalAssaultFPV.Loadout;

public class ArmoryEquipButtonTrigger : MonoBehaviour
{
    public ArmoryEquipButtonTrigger(IntPtr ptr) : base(ptr) { }

    private bool _isPressed;
    private Vector3 _originalPos;
    private float _lastPressTime;
    private Renderer _renderer;
    private Color _normalColor = new Color(0.1f, 0.75f, 0.2f);
    private Color _activeColor = new Color(0.3f, 1.0f, 0.5f);

    public void Awake()
    {
        _originalPos = transform.localPosition;
        _renderer = GetComponent<Renderer>();
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        if (_isPressed) return;
        if (Time.time - _lastPressTime < 1.0f) return;

        PlayerHealth player = other.GetComponentInParent<PlayerHealth>() ?? UnityEngine.Object.FindObjectOfType<PlayerHealth>();
        if (player != null)
        {
            _isPressed = true;
            _lastPressTime = Time.time;
            transform.localPosition = _originalPos - new Vector3(0f, 0.015f, 0f);

            if (_renderer != null && _renderer.material != null)
            {
                _renderer.material.color = _activeColor;
            }

            MelonLoader.MelonLogger.Msg("[Armory] RESPAWN / EQUIP FPV Button pressed! Respawning items on table & equipping vest sockets...");
            FPVLoadoutIntegration.RespawnStationItems();
            FPVLoadoutIntegration.EquipToPlayerSockets(player);
        }
    }

    public void OnTriggerExit(Collider other)
    {
        if (_isPressed)
        {
            _isPressed = false;
            transform.localPosition = _originalPos;
            if (_renderer != null && _renderer.material != null)
            {
                _renderer.material.color = _normalColor;
            }
        }
    }
}
