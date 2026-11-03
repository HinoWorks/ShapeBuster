using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// マウス位置をプレイ平面のワールド座標へ変換し、PointerWorld を更新する。
/// </summary>
public class PointerInput : MonoBehaviour
{
    public static PointerInput Inst { get; private set; }

    [SerializeField] Camera worldCamera;
    private float playPlaneZ = 0f;


    void Awake()
    {
        if (Inst == null) { Inst = this; }
        else { Destroy(this); return; }

        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    void OnDestroy()
    {
        if (Inst == this) Inst = null;
    }

    void Update()
    {
        if (worldCamera == null)
            worldCamera = Camera.main;
        if (worldCamera == null) return;

        var mouse = Mouse.current;
        if (mouse == null)
        {
            PointerWorld.Set(PointerWorld.Position, false);
            return;
        }

        var screen = mouse.position.ReadValue();
        var world = ScreenToPlayPlane(screen);
        var inside = IsInsideViewport(screen) && !IsOverBlockingUI();
        PointerWorld.Set(world, inside);
    }

    Vector3 ScreenToPlayPlane(Vector2 screen)
    {
        float depth = Mathf.Abs(worldCamera.transform.position.z - playPlaneZ);
        var world = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
        world.z = playPlaneZ;
        return world;
    }

    bool IsInsideViewport(Vector2 screen)
    {
        var viewport = worldCamera.ScreenToViewportPoint(new Vector3(screen.x, screen.y, 0f));
        Vector2 min = Vector2.zero;
        Vector2 max = Vector2.one;
        if (CameraManager.Inst != null)
            CameraManager.Inst.GetPlayfieldViewport(out min, out max);
        return viewport.x >= min.x && viewport.x <= max.x
            && viewport.y >= min.y && viewport.y <= max.y;
    }

    static bool IsOverBlockingUI()
    {
        if (EventSystem.current == null) return false;
        var pointer = Pointer.current;
        if (pointer == null) return false;
        return EventSystem.current.IsPointerOverGameObject(pointer.device.deviceId);
    }
}
