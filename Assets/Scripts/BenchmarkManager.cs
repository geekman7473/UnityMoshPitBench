using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Runs a timed benchmark, recording per-frame FPS and ball count.
/// When time is up the scene is paused and a results screen is drawn
/// showing Average FPS, 1% Low FPS, ball count, and an FPS-over-time graph.
/// An "Exit" button quits the application.
/// </summary>
public class BenchmarkManager : MonoBehaviour
{
    // ── configuration ──────────────────────────────────────────────
    [Header("Benchmark Settings")]
    public float benchmarkDuration = 45f;

    // ── internal state ─────────────────────────────────────────────
    private readonly List<float> _fpsHistory = new List<float>();
    private float _elapsed;
    private bool _benchmarkDone;
    private Texture2D _graphTexture;

    /// <summary>True once the benchmark has finished and the results screen is showing.</summary>
    public bool IsBenchmarkDone => _benchmarkDone;

    // Cached results
    private float _avgFps;
    private float _onePercentLow;
    private float _minFps;
    private float _maxFps;
    private int _totalBalls;

    // HUD
    private float _hudFps;
    private float _hudUpdateTimer;
    private const float HudUpdateInterval = 0.25f;

    // Styles (created once)
    private GUIStyle _boxStyle;
    private GUIStyle _titleStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _valueLabelStyle;
    private GUIStyle _buttonStyle;
    private GUIStyle _smallLabelStyle;
    private GUIStyle _hudStyle;
    private bool _stylesInitialized;

    // ── MonoBehaviour ──────────────────────────────────────────────
    private void Update()
    {
        if (_benchmarkDone) return;

        _elapsed += Time.unscaledDeltaTime;

        float fps = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        _fpsHistory.Add(fps);

        // Update HUD readout periodically
        _hudUpdateTimer -= Time.unscaledDeltaTime;
        if (_hudUpdateTimer <= 0f)
        {
            _hudFps = fps;
            _hudUpdateTimer = HudUpdateInterval;
        }

        if (_elapsed >= benchmarkDuration)
        {
            FinishBenchmark();
        }
    }

    // ── Benchmark logic ────────────────────────────────────────────
    private void FinishBenchmark()
    {
        _benchmarkDone = true;
        Time.timeScale = 0f;

        _avgFps = _fpsHistory.Average();
        _minFps = _fpsHistory.Min();
        _maxFps = _fpsHistory.Max();

        List<float> sorted = _fpsHistory.OrderBy(f => f).ToList();
        int count1Pct = Mathf.Max(1, Mathf.FloorToInt(sorted.Count * 0.01f));
        _onePercentLow = sorted.Take(count1Pct).Average();

        // Count pool balls in scene
        _totalBalls = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length;

        _graphTexture = BuildGraphTexture(800, 300);
    }

    // ── Graph texture generation ───────────────────────────────────
    private Texture2D BuildGraphTexture(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Color bgColor = new Color(0.10f, 0.10f, 0.14f, 1f);
        Color gridColor = new Color(0.22f, 0.22f, 0.28f, 1f);
        Color lineColor = new Color(0.30f, 0.85f, 0.55f, 1f);
        Color avgColor = new Color(1f, 0.85f, 0.25f, 0.7f);
        Color lowColor = new Color(1f, 0.30f, 0.30f, 0.7f);

        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = bgColor;
        tex.SetPixels(pixels);

        int marginLeft = 50;
        int marginBottom = 30;
        int marginTop = 10;
        int marginRight = 10;
        int graphW = width - marginLeft - marginRight;
        int graphH = height - marginBottom - marginTop;

        float fpsMin = 0;
        float fpsMax = Mathf.Ceil(_maxFps / 10f) * 10f;
        if (fpsMax < 10) fpsMax = 10;

        // Horizontal grid lines
        float gridStep = Mathf.Max(10, Mathf.Round(fpsMax / 6f / 10f) * 10f);
        for (float g = 0; g <= fpsMax; g += gridStep)
        {
            int y = marginBottom + Mathf.RoundToInt((g - fpsMin) / (fpsMax - fpsMin) * graphH);
            if (y < 0 || y >= height) continue;
            for (int x = marginLeft; x < marginLeft + graphW; x++)
                tex.SetPixel(x, y, gridColor);
        }

        // Avg line
        int avgY = marginBottom + Mathf.Clamp(Mathf.RoundToInt((_avgFps - fpsMin) / (fpsMax - fpsMin) * graphH), 0, graphH);
        for (int x = marginLeft; x < marginLeft + graphW; x++)
        {
            tex.SetPixel(x, avgY, avgColor);
            if (avgY + 1 < height) tex.SetPixel(x, avgY + 1, avgColor);
        }

        // 1% Low line
        int lowY = marginBottom + Mathf.Clamp(Mathf.RoundToInt((_onePercentLow - fpsMin) / (fpsMax - fpsMin) * graphH), 0, graphH);
        for (int x = marginLeft; x < marginLeft + graphW; x++)
        {
            tex.SetPixel(x, lowY, lowColor);
            if (lowY + 1 < height) tex.SetPixel(x, lowY + 1, lowColor);
        }

        // FPS data line
        int sampleCount = _fpsHistory.Count;
        float samplesPerPixel = (float)sampleCount / graphW;

        int prevPx = -1, prevPy = -1;
        for (int px = 0; px < graphW; px++)
        {
            int sampleIdx = Mathf.Clamp(Mathf.RoundToInt(px * samplesPerPixel), 0, sampleCount - 1);
            float val = _fpsHistory[sampleIdx];

            int py = marginBottom + Mathf.Clamp(Mathf.RoundToInt((val - fpsMin) / (fpsMax - fpsMin) * graphH), 0, graphH - 1);

            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int sx = marginLeft + px + dx;
                    int sy = py + dy;
                    if (sx >= 0 && sx < width && sy >= 0 && sy < height)
                        tex.SetPixel(sx, sy, lineColor);
                }

            if (prevPx >= 0 && Mathf.Abs(py - prevPy) > 1)
            {
                int yStart = Mathf.Min(py, prevPy);
                int yEnd = Mathf.Max(py, prevPy);
                int xPos = marginLeft + px;
                for (int fy = yStart; fy <= yEnd; fy++)
                    if (xPos >= 0 && xPos < width && fy >= 0 && fy < height)
                        tex.SetPixel(xPos, fy, lineColor);
            }

            prevPx = px;
            prevPy = py;
        }

        // Axis lines
        for (int y = marginBottom; y < marginBottom + graphH; y++)
            tex.SetPixel(marginLeft, y, gridColor);
        for (int x = marginLeft; x < marginLeft + graphW; x++)
            tex.SetPixel(x, marginBottom, gridColor);

        tex.Apply();
        return tex;
    }

    // ── GUI ────────────────────────────────────────────────────────
    private void InitStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        _boxStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = MakeTex(2, 2, new Color(0.08f, 0.08f, 0.12f, 0.96f)) }
        };

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 32,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            normal = { textColor = new Color(0.75f, 0.75f, 0.80f) }
        };

        _valueLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = Color.white }
        };

        _smallLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            normal = { textColor = new Color(0.55f, 0.55f, 0.60f) }
        };

        _buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            fixedHeight = 55,
            normal = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.75f, 0.18f, 0.18f, 1f)) },
            hover = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.90f, 0.25f, 0.25f, 1f)) },
            active = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.60f, 0.12f, 0.12f, 1f)) }
        };

        _hudStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 1f, 1f, 0.85f) }
        };
    }

    private void OnGUI()
    {
        InitStyles();

        if (!_benchmarkDone)
        {
            // Live HUD: FPS, timer, ball count
            float timeLeft = benchmarkDuration - _elapsed;
            int ballCount = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length;
            string hudText = $"FPS: {_hudFps:F0}  |  Time: {timeLeft:F1}s  |  Balls: {ballCount}";
            GUI.Label(new Rect(10, 10, 500, 30), hudText, _hudStyle);
            return;
        }

        // ── Results screen ──

        float panelW = 880;
        float panelH = 660;
        float x = (Screen.width - panelW) / 2f;
        float y = (Screen.height - panelH) / 2f;

        // Dim background
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
            MakeTex(1, 1, new Color(0, 0, 0, 0.65f)));

        // Panel
        GUI.Box(new Rect(x, y, panelW, panelH), GUIContent.none, _boxStyle);

        float cx = x + 40;
        float cy = y + 15;
        float contentW = panelW - 80;

        // Title
        GUI.Label(new Rect(cx, cy, contentW, 45), "MoshPit Benchmark Results", _titleStyle);
        cy += 52;

        // Stats rows
        float rowH = 34;
        DrawStatRow(cx, cy, contentW, rowH, "Average FPS", $"{_avgFps:F1}");
        cy += rowH;
        DrawStatRow(cx, cy, contentW, rowH, "1% Low FPS", $"{_onePercentLow:F1}");
        cy += rowH;
        DrawStatRow(cx, cy, contentW, rowH, "Min FPS", $"{_minFps:F1}");
        cy += rowH;
        DrawStatRow(cx, cy, contentW, rowH, "Max FPS", $"{_maxFps:F1}");
        cy += rowH;
        DrawStatRow(cx, cy, contentW, rowH, "Total Frames", $"{_fpsHistory.Count}");
        cy += rowH;
        DrawStatRow(cx, cy, contentW, rowH, "Total Balls", $"{_totalBalls}");
        cy += rowH;
        DrawStatRow(cx, cy, contentW, rowH, "Duration", $"{benchmarkDuration:F0}s");
        cy += rowH + 8;

        // Graph
        if (_graphTexture != null)
        {
            float graphW = contentW;
            float graphH = graphW * (_graphTexture.height / (float)_graphTexture.width);
            GUI.DrawTexture(new Rect(cx, cy, graphW, graphH), _graphTexture);

            float legendY = cy + graphH + 4;
            GUI.Label(new Rect(cx, legendY, 200, 20), "— FPS    ", _smallLabelStyle);

            GUIStyle avgLegend = new GUIStyle(_smallLabelStyle) { normal = { textColor = new Color(1f, 0.85f, 0.25f) } };
            GUI.Label(new Rect(cx + 80, legendY, 200, 20), "— Avg", avgLegend);

            GUIStyle lowLegend = new GUIStyle(_smallLabelStyle) { normal = { textColor = new Color(1f, 0.30f, 0.30f) } };
            GUI.Label(new Rect(cx + 160, legendY, 200, 20), "— 1% Low", lowLegend);

            GUI.Label(new Rect(cx, legendY + 16, 80, 20), "0 s", _smallLabelStyle);
            GUIStyle rightAligned = new GUIStyle(_smallLabelStyle) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(cx + graphW - 80, legendY + 16, 80, 20), $"{benchmarkDuration:F0} s", rightAligned);

            cy += graphH + 50;
        }

        // Exit button
        float btnW = 220;
        if (GUI.Button(new Rect(x + (panelW - btnW) / 2f, cy, btnW, 55), "Exit", _buttonStyle))
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    private void DrawStatRow(float x, float y, float w, float h, string label, string value)
    {
        GUI.Label(new Rect(x, y, w * 0.6f, h), label, _labelStyle);
        GUI.Label(new Rect(x + w * 0.6f, y, w * 0.4f, h), value, _valueLabelStyle);
    }

    // ── Helpers ────────────────────────────────────────────────────
    private static readonly Dictionary<Color, Texture2D> _texCache = new Dictionary<Color, Texture2D>();

    private static Texture2D MakeTex(int w, int h, Color col)
    {
        if (_texCache.TryGetValue(col, out Texture2D cached) && cached != null) return cached;
        Color[] pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        Texture2D result = new Texture2D(w, h);
        result.SetPixels(pix);
        result.Apply();
        _texCache[col] = result;
        return result;
    }
}
