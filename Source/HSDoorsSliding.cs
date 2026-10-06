using UnityEngine;

public static class HSDoorsSliding
{
    public static Vector3 LeafOffset(HSDoorsConfigData d, HSDoorsCell c)
    {
        if (d == null || c == null) return Vector3.zero;
        float open = Mathf.Clamp01(d.Open);
        float travel = Mathf.Max(0.25f, d.Travel);
        float sign = LeafSign(d, c);
        float dist = travel * open;
        if (d.Mode == "tele")
        {
            if (c.Leaf == 1) dist = travel * open;
            else dist = travel * 0.55f * open;
        }
        if (d.Axis == 0) return new Vector3(sign * dist, 0f, 0f);
        return new Vector3(0f, 0f, sign * dist);
    }

    static float LeafSign(HSDoorsConfigData d, HSDoorsCell c)
    {
        if (d.Mode == "left") return -1f * d.AxisSign;
        if (d.Mode == "right") return 1f * d.AxisSign;
        return c.Leaf == 1 ? -1f : 1f;
    }

    public static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    public static Bounds LeafWorldBounds(HSDoorsConfigData d, int leaf)
    {
        var off1 = LeafOffset(d, new HSDoorsCell { Leaf = 1, Dx = 0, Dy = 0, Dz = 0 });
        var off2 = LeafOffset(d, new HSDoorsCell { Leaf = 2, Dx = 0, Dy = 0, Dz = 0 });
        var off = leaf == 1 ? off1 : off2;
        float minX = d.OriginX + off.x;
        float minY = d.OriginY;
        float minZ = d.OriginZ + off.z;
        float maxX = d.OriginX + d.SizeX + off.x;
        float maxY = d.OriginY + d.SizeY;
        float maxZ = d.OriginZ + d.SizeZ + off.z;
        if (d.Mode == "bipart" || d.Mode == "tele")
        {
            if (d.Axis == 0)
            {
                float mid = d.OriginX + d.SizeX * 0.5f + off.x;
                if (leaf == 1) maxX = mid;
                else minX = mid;
            }
            else
            {
                float mid = d.OriginZ + d.SizeZ * 0.5f + off.z;
                if (leaf == 1) maxZ = mid;
                else minZ = mid;
            }
        }
        var c = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);
        var s = new Vector3(Mathf.Max(0.2f, maxX - minX), Mathf.Max(0.2f, maxY - minY), Mathf.Max(0.2f, maxZ - minZ));
        return new Bounds(c, s);
    }

    public static Vector3 SlideAxis(HSDoorsConfigData d)
    {
        if (d.Axis == 0) return new Vector3(d.AxisSign, 0f, 0f);
        return new Vector3(0f, 0f, d.AxisSign);
    }
}
