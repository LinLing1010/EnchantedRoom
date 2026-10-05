using System;
using System.IO;
using UnityEngine;

public static class WallMaskBaker
{
    const int Resolution = 128;
    //static string CacheDir => Path.Combine(Application.dataPath, "WallMaskCache");
    static string CacheDir => Path.Combine(Application.persistentDataPath, "WallMaskCache");

    public static Texture2D GetOrBake(WindowsInput windowsInput, Renderer wallRenderer)
    {
        int hash = ComputeHash(windowsInput, wallRenderer);
        string path = Path.Combine(CacheDir, $"wallmask_{hash}.png");

        if (File.Exists(path))
            return LoadFromDisk(path);

        Texture2D tex = Bake(windowsInput, wallRenderer);
        SaveToDisk(tex, path);
        return tex;
    }

    static Texture2D Bake(WindowsInput windowsInput, Renderer wallRenderer)
    {
        Transform t      = wallRenderer.transform;
        Vector3 center   = t.position;
        Vector3 right    = t.right;
        Vector3 up       = t.up;
        Vector3 size     = t.localScale;

        int count = Mathf.Min(windowsInput.windows.Count, windowsInput.maxWindows);

        Vector3[] positions    = new Vector3[count];
        Vector3[] extents      = new Vector3[count];
        Vector4[] orientations = new Vector4[count];

        for (int i = 0; i < count; i++)
        {
            WindowsInput.WindowData w = windowsInput.windows[i];

            Vector3 safeExtent = new Vector3(
                Mathf.Max(Mathf.Abs(w.extent.x), windowsInput.minExtent),
                Mathf.Max(Mathf.Abs(w.extent.y), windowsInput.minExtent),
                Mathf.Max(Mathf.Abs(w.extent.z), windowsInput.minExtent)
            );

            Quaternion q = new Quaternion(
                w.orientation.x, w.orientation.y,
                w.orientation.z, w.orientation.w
            ).normalized;

            positions[i]    = w.position;
            extents[i]      = safeExtent;
            orientations[i] = new Vector4(q.x, q.y, q.z, q.w);
        }

        Texture2D tex = new Texture2D(Resolution, Resolution, TextureFormat.R8, false);
        Color[] pixels = new Color[Resolution * Resolution];

        for (int py = 0; py < Resolution; py++)
        {
            for (int px = 0; px < Resolution; px++)
            {
                float u = (px + 0.5f) / Resolution;
                float v = (py + 0.5f) / Resolution;

                // uv(0.5, 0.5) = 墙中心，right/up 包含旋转，size 包含尺寸
                Vector3 worldPos = center
                    + right * (u - 0.5f) * size.x
                    + up    * (v - 0.5f) * size.y;

                bool isHole = IsInsideAnyWindow(worldPos, positions, extents, orientations, count);
                pixels[py * Resolution + px] = new Color(isHole ? 0f : 1f, 0, 0);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    static bool IsInsideAnyWindow(
        Vector3 worldPos,
        Vector3[] positions, Vector3[] extents, Vector4[] orientations,
        int count)
    {
        for (int i = 0; i < count; i++)
        {
            Vector3 center = positions[i];
            Vector3 ext    = extents[i];
            Vector4 q      = orientations[i];

            Vector3 delta = worldPos - center;

            Vector3 u = -new Vector3(q.x, q.y, q.z);
            float   s = q.w;
            Vector3 localDelta =
                2f * Vector3.Dot(u, delta) * u
                + (s * s - Vector3.Dot(u, u)) * delta
                + 2f * s * Vector3.Cross(u, delta);

            if (Mathf.Abs(localDelta.x) <= ext.x &&
                Mathf.Abs(localDelta.y) <= ext.y &&
                Mathf.Abs(localDelta.z) <= ext.z)
                return true;
        }
        return false;
    }

    static int ComputeHash(WindowsInput windowsInput, Renderer wallRenderer)
    {
        var h = new HashCode();
        foreach (var w in windowsInput.windows)
        {
            h.Add(w.position);
            h.Add(w.extent);
            h.Add(w.orientation);
        }
        h.Add(wallRenderer.transform.position);
        h.Add(wallRenderer.transform.rotation);
        h.Add(wallRenderer.transform.localScale);
        return h.ToHashCode();
    }

    static void SaveToDisk(Texture2D tex, string path)
    {
        Directory.CreateDirectory(CacheDir);
        //File.WriteAllBytes(path, tex.EncodeToPNG());
        File.WriteAllBytes(path, tex.GetRawTextureData());
    }

    static Texture2D LoadFromDisk(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(Resolution, Resolution, TextureFormat.R8, false);
        //tex.LoadImage(bytes);
        tex.LoadRawTextureData(bytes);
        tex.Apply();
        return tex;
    }
}