using System.Collections;
using UnityEngine;

public class HighwayBuilder : MonoBehaviour
{
    // HTML版 js/modes/rhythm.js のレーン色に準拠
    static readonly Color[] GroupColors = {
        new Color(0.0f,  1.0f,  1.0f),  // A (0,1)  – cyan
        new Color(0.2f,  0.4f,  1.0f),  // S (2,3)  – blue
        new Color(0.6f,  0.2f,  1.0f),  // D (4,5)  – purple
        new Color(1.0f,  0.3f,  0.6f),  // J (6,7)  – pink
        new Color(1.0f,  0.55f, 0.1f),  // K (8,9)  – orange
        new Color(0.2f,  1.0f,  0.4f),  // L (10,11)– green
    };

    static Shader _unlitShader;
    static Shader UnlitShader =>
        _unlitShader != null ? _unlitShader
        : (_unlitShader = Shader.Find("Unlit/Color")
                       ?? Shader.Find("Universal Render Pipeline/Unlit")
                       ?? Shader.Find("Standard"));

    static Material MakeTransparentMat(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                  ?? Shader.Find("Sprites/Default")
                  ?? Shader.Find("Standard");
        var mat = new Material(shader);
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        ApplyColor(mat, color);
        return mat;
    }

    Light[] _hitLights;
    Material[] _laneMats; // レーングロー用、インスタンス保持
    readonly bool[] _glowActive = new bool[6]; // キー押下中フラグ（Flash後の intensity 復元用）

    public void Build()
    {
        BuildFloor();
        BuildLaneStrips();
        BuildDividers();
        BuildHitLine();
        BuildBorders();
        BuildHitLights();
    }

    // ---------------------------------------------------------------
    void BuildFloor()
    {
        var go = Cube("Floor");
        float len = GameConstants.NOTE_Z_DEAD - GameConstants.NOTE_Z_SPAWN;
        float w   = (GameConstants.NUM_LANES + 1) * GameConstants.LANE_SPACING;
        go.transform.localScale = new Vector3(w, 0.01f, len);
        go.transform.position   = new Vector3(0f, -0.01f, MidZ());
        go.GetComponent<Renderer>().sharedMaterial = MakeTransparentMat(new Color(0.04f, 0.04f, 0.09f, 0.55f));
    }

    void BuildLaneStrips()
    {
        _laneMats = new Material[GameConstants.NUM_LANES];
        float len = GameConstants.NOTE_Z_DEAD - GameConstants.NOTE_Z_SPAWN;
        for (int lane = 0; lane < GameConstants.NUM_LANES; lane++)
        {
            var go = Cube($"Lane_{lane}");
            go.transform.localScale = new Vector3(GameConstants.LANE_SPACING * 0.96f, 0.005f, len);
            go.transform.position   = new Vector3(LaneX(lane), 0f, MidZ());
            Color c = GroupColors[lane / 2];
            var mat = MakeTransparentMat(DimColor(c));
            go.GetComponent<Renderer>().sharedMaterial = mat;
            _laneMats[lane] = mat;
        }
    }

    void BuildDividers()
    {
        float len = GameConstants.NOTE_Z_DEAD - GameConstants.NOTE_Z_SPAWN;
        for (int i = 0; i <= GameConstants.NUM_LANES; i++)
        {
            var go = Cube($"Div_{i}");
            float x = (i - 6f) * GameConstants.LANE_SPACING;
            go.transform.localScale = new Vector3(0.025f, 0.008f, len);
            go.transform.position   = new Vector3(x, 0.003f, MidZ());
            bool major = (i % 2 == 0);
            go.GetComponent<Renderer>().sharedMaterial = MakeTransparentMat(
                major ? new Color(0.5f, 0.75f, 1f, 0.5f) : new Color(0.2f, 0.3f, 0.5f, 0.4f));
        }
    }

    void BuildHitLine()
    {
        var go = Cube("HitLine");
        float w = (GameConstants.NUM_LANES + 1) * GameConstants.LANE_SPACING;
        go.transform.localScale = new Vector3(w, 0.06f, 0.12f);
        go.transform.position   = new Vector3(0f, 0.01f, GameConstants.NOTE_Z_HIT);
        SetColor(go, Color.red);
        go.AddComponent<HitLinePulse>();
    }

    void BuildBorders()
    {
        float len   = GameConstants.NOTE_Z_DEAD - GameConstants.NOTE_Z_SPAWN;
        float halfW = 6f * GameConstants.LANE_SPACING;
        foreach (float side in new[] { -(halfW + 0.15f), halfW + 0.15f })
        {
            var go = Cube("Border");
            go.transform.localScale = new Vector3(0.12f, 0.25f, len);
            go.transform.position   = new Vector3(side, 0.12f, MidZ());
            go.GetComponent<Renderer>().sharedMaterial = MakeTransparentMat(new Color(0.4f, 0.6f, 1f, 0.5f));
        }
    }

    void BuildHitLights()
    {
        _hitLights = new Light[6];
        for (int g = 0; g < 6; g++)
        {
            float laneAvg = (GameConstants.KEY_LANES[g, 0] + GameConstants.KEY_LANES[g, 1]) * 0.5f;
            float x = (laneAvg - 5.5f) * GameConstants.LANE_SPACING;
            var go = new GameObject($"HitLight_{g}");
            go.transform.SetParent(transform);
            go.transform.position = new Vector3(x, 0.6f, GameConstants.NOTE_Z_HIT);
            var lt = go.AddComponent<Light>();
            lt.type      = LightType.Point;
            lt.color     = GroupColors[g];
            lt.range     = 3.5f;
            lt.intensity = 0f;
            _hitLights[g] = lt;
        }
    }

    // ---------------------------------------------------------------
    // キー押下時に2レーンを明るく、離したら暗く戻す
    public void SetGroupGlow(int group, bool active)
    {
        if (_laneMats == null || (uint)group >= 6u) return;
        int laneA = GameConstants.KEY_LANES[group, 0];
        int laneB = GameConstants.KEY_LANES[group, 1];
        Color c = GroupColors[group];
        Color col = active ? BrightColor(c) : DimColor(c);
        ApplyColor(_laneMats[laneA], col);
        ApplyColor(_laneMats[laneB], col);

        // ヒットライトも連動
        if (_hitLights != null && (uint)group < (uint)_hitLights.Length)
        {
            _glowActive[group] = active;
            _hitLights[group].intensity = active ? 1.5f : 0f;
        }
    }

    public void FlashLight(int group)
    {
        if (_hitLights == null || (uint)group >= (uint)_hitLights.Length) return;
        StartCoroutine(Flash(group));
    }

    IEnumerator Flash(int group)
    {
        _hitLights[group].intensity = 4f;
        yield return new WaitForSeconds(0.06f);
        _hitLights[group].intensity = _glowActive[group] ? 1.5f : 0f;
    }

    // ---------------------------------------------------------------
    static Color DimColor(Color c)    => new Color(c.r * 0.18f, c.g * 0.18f, c.b * 0.18f, 0.45f);
    static Color BrightColor(Color c) => new Color(c.r * 0.70f, c.g * 0.70f, c.b * 0.70f, 0.70f);

    // Three.jsと座標系の左右を合わせるため符号を反転
    static float LaneX(int lane) => (5.5f - lane) * GameConstants.LANE_SPACING;
    static float MidZ() => (GameConstants.NOTE_Z_SPAWN + GameConstants.NOTE_Z_DEAD) * 0.5f;

    static GameObject Cube(string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        return go;
    }

    static void SetColor(GameObject go, Color color)
    {
        var mat = new Material(UnlitShader);
        ApplyColor(mat, color);
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    internal static void ApplyColor(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))     mat.color = color;
    }
}
