using UnityEngine;

public static class HSDoorsRevolving
{
    public static float WingStep(HSDoorsConfigData d)
    {
        int w = d != null && d.Wings >= 2 ? d.Wings : 4;
        return 360f / w;
    }

    public static float SnapAngle(HSDoorsConfigData d, float angle)
    {
        float step = WingStep(d);
        float a = angle % 360f;
        if (a < 0f) a += 360f;
        return Mathf.Round(a / step) * step;
    }

    public static bool NearSnap(HSDoorsConfigData d, float angle, float deg)
    {
        float step = WingStep(d);
        float a = angle % step;
        if (a < 0f) a += step;
        return a < deg || a > step - deg;
    }

    public static float DistToWing(HSDoorsConfigData d, Vector3 worldFeet)
    {
        if (d == null) return 99f;
        var hub = d.HubWorld;
        var rel = new Vector3(worldFeet.x - hub.x, 0f, worldFeet.z - hub.z);
        float r = rel.magnitude;
        if (r < 0.15f || r > d.Radius + 0.4f) return 99f;
        float ang = Mathf.Atan2(rel.z, rel.x) * Mathf.Rad2Deg;
        float step = WingStep(d);
        float a = (ang - d.Angle) % step;
        if (a < 0f) a += step;
        float nearest = Mathf.Min(a, step - a);
        return Mathf.Abs(Mathf.Sin(nearest * Mathf.Deg2Rad) * r);
    }

    public static Vector3 RotateAroundHub(HSDoorsConfigData d, Vector3 world, float dAngle)
    {
        var hub = d.HubWorld;
        var rel = world - new Vector3(hub.x, world.y, hub.z);
        var q = Quaternion.AngleAxis(dAngle, Vector3.up);
        var n = q * rel;
        return new Vector3(hub.x + n.x, world.y, hub.z + n.z);
    }

    public static bool InsideDrum(HSDoorsConfigData d, Vector3 feet)
    {
        if (d == null) return false;
        if (feet.y < d.HubY - 0.2f || feet.y > d.HubY + d.Height + 0.4f) return false;
        var hub = d.HubWorld;
        float dx = feet.x - hub.x;
        float dz = feet.z - hub.z;
        return dx * dx + dz * dz <= (d.Radius + 0.15f) * (d.Radius + 0.15f);
    }
}
