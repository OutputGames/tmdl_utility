namespace tmdl_utility;


public static class Utils
{
    public static ModelUtility.Vec3[] ToVec3Array(this ModelUtility.Vec4[] a, int start=0, int count=-1)
    {
        if (count == -1)
        {
            count = a.Length;
        }

        ModelUtility.Vec3[] v = new ModelUtility.Vec3[a.Length];

        for (var i = 0; i < a.Length; i++)
        {
            v[i] = a[i].ToVec3();
        }

        return v;
    }

    public static int[] ToArray(this ModelUtility.Vec4 v)
    {
        int[] i = new int[4];

        for (int j = 0; j < 4; j++)
        {
            i[j] = (int)v[j];
        }

        return i;
    }

    public static float[] ToFltArray(this ModelUtility.Vec4 v)
    {
        float[] i = new float[4];

        for (int j = 0; j < 4; j++)
        {
            i[j] = v[j];
        }

        return i;
    }

    public static ModelUtility.Vec2[] ToVec2Array(this ModelUtility.Vec4[] a, int start = 0, int count = -1)
    {
        if (count == -1)
        {
            count = a.Length;
        }

        ModelUtility.Vec2[] v = new ModelUtility.Vec2[a.Length];

        for (var i = 0; i < a.Length; i++)
        {
            v[i] = a[i].ToVec2();
        }

        return v;
    }
}