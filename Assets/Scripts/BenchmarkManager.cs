using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Multi-phase benchmark manager. Phases transition automatically.
/// Phase 1: Mosh Pit (pool balls in funnel)
/// Phase 2: Space Rocks (n-body asteroid sim)
/// Phase 3: Corn Maze (1000 AI agents pathfinding)
/// After all phases, shows a combined results screen with per-phase stats
/// and an overall geometric mean score.
/// </summary>
public class BenchmarkManager : MonoBehaviour
{
    public enum Phase { Phase1_Running, Phase2_Running, Phase3_Running, FinalResults }

    // ── configuration ──────────────────────────────────────────────
    [Header("Benchmark Settings")]
    public float phase1Duration = 45f;
    public float phase2Duration = 45f;
    public float phase3Duration = 45f;

    public Action onTransitionToPhase2;
    public Action onTransitionToPhase3;

    // ── state ──────────────────────────────────────────────────────
    public Phase CurrentPhase { get; private set; } = Phase.Phase1_Running;

    // Per-phase stats
    private struct PhaseStats
    {
        public string name;
        public float avg, min, max, low;
        public int totalFrames, totalBodies;
        public float duration;
        public Texture2D graph;
        public bool valid; // was this phase actually run (not skipped with 0 frames)
    }

    private PhaseStats _s1, _s2, _s3;

    // Phase frame histories
    private readonly List<float> _p1Fps = new List<float>();
    private readonly List<float> _p2Fps = new List<float>();
    private readonly List<float> _p3Fps = new List<float>();
    private float _p1Elapsed, _p2Elapsed, _p3Elapsed;

    // HUD
    private float _hudFps;
    private int _hudBodies;
    private float _hudUpdateTimer;
    private const float HudUpdateInterval = 0.25f;

    // Styles
    private GUIStyle _boxStyle, _titleStyle, _labelStyle, _valueLabelStyle;
    private GUIStyle _buttonStyle, _smallLabelStyle, _hudStyle;
    private GUIStyle _headerStyle, _scoreStyle;
    private bool _stylesInitialized;

    // Scroll position for final results
    private Vector2 _scrollPos;

    // ── MonoBehaviour ──────────────────────────────────────────────
    private void Update()
    {
        // Escape skips current phase
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (CurrentPhase == Phase.Phase1_Running)
            { FinishPhase1(); TransitionToPhase2(); return; }
            else if (CurrentPhase == Phase.Phase2_Running)
            { FinishPhase2(); TransitionToPhase3(); return; }
            else if (CurrentPhase == Phase.Phase3_Running)
            { FinishPhase3(); return; }
        }

        switch (CurrentPhase)
        {
            case Phase.Phase1_Running:
                _p1Elapsed += Time.unscaledDeltaTime;
                RecordFps(_p1Fps);
                if (_p1Elapsed >= phase1Duration)
                { FinishPhase1(); TransitionToPhase2(); }
                break;

            case Phase.Phase2_Running:
                _p2Elapsed += Time.unscaledDeltaTime;
                RecordFps(_p2Fps);
                if (_p2Elapsed >= phase2Duration)
                { FinishPhase2(); TransitionToPhase3(); }
                break;

            case Phase.Phase3_Running:
                _p3Elapsed += Time.unscaledDeltaTime;
                RecordFps(_p3Fps);
                if (_p3Elapsed >= phase3Duration)
                    FinishPhase3();
                break;
        }
    }

    private void RecordFps(List<float> history)
    {
        float fps = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        history.Add(fps);
        UpdateHud(fps);
    }

    private void UpdateHud(float fps)
    {
        _hudUpdateTimer -= Time.unscaledDeltaTime;
        if (_hudUpdateTimer <= 0f)
        {
            _hudFps = fps;
            _hudBodies = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length
                       + FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None).Length;
            _hudUpdateTimer = HudUpdateInterval;
        }
    }

    // ── Phase transitions ──────────────────────────────────────────
    private void FinishPhase1()
    {
        _s1 = BuildStats("Mosh Pit", _p1Fps, _p1Elapsed, phase1Duration,
            FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length);
    }

    private void TransitionToPhase2()
    {
        onTransitionToPhase2?.Invoke();
        CurrentPhase = Phase.Phase2_Running;
        _hudUpdateTimer = 0f;
    }

    private void FinishPhase2()
    {
        _s2 = BuildStats("Space Rocks", _p2Fps, _p2Elapsed, phase2Duration,
            FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length);
    }

    private void TransitionToPhase3()
    {
        onTransitionToPhase3?.Invoke();
        CurrentPhase = Phase.Phase3_Running;
        _hudUpdateTimer = 0f;
    }

    private void FinishPhase3()
    {
        _s3 = BuildStats("Corn Maze", _p3Fps, _p3Elapsed, phase3Duration,
            FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None).Length);
        CurrentPhase = Phase.FinalResults;
        Time.timeScale = 0f;
    }

    private PhaseStats BuildStats(string name, List<float> fps, float elapsed, float configuredDuration, int bodies)
    {
        PhaseStats s = new PhaseStats();
        s.name = name;
        s.duration = Mathf.Min(elapsed, configuredDuration);
        s.totalBodies = bodies;

        if (fps.Count > 0)
        {
            s.valid = true;
            s.avg = fps.Average();
            s.min = fps.Min();
            s.max = fps.Max();
            var sorted = fps.OrderBy(f => f).ToList();
            int count1Pct = Mathf.Max(1, Mathf.FloorToInt(sorted.Count * 0.01f));
            s.low = sorted.Take(count1Pct).Average();
            s.totalFrames = fps.Count;
            s.graph = BuildGraphTexture(fps, s.avg, s.low, s.max, s.duration, 800, 200);
        }
        return s;
    }

    // ── Graph texture generation ───────────────────────────────────
    private Texture2D BuildGraphTexture(List<float> fpsData, float avgFps, float lowFps, float maxFps,
        float duration, int width, int height)
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

        int marginLeft = 50, marginBottom = 20, marginTop = 5, marginRight = 10;
        int graphW = width - marginLeft - marginRight;
        int graphH = height - marginBottom - marginTop;

        float fpsMin = 0;
        float fpsMax = Mathf.Ceil(maxFps / 10f) * 10f;
        if (fpsMax < 10) fpsMax = 10;

        float gridStep = Mathf.Max(10, Mathf.Round(fpsMax / 4f / 10f) * 10f);
        for (float g = 0; g <= fpsMax; g += gridStep)
        {
            int y = marginBottom + Mathf.RoundToInt((g - fpsMin) / (fpsMax - fpsMin) * graphH);
            if (y < 0 || y >= height) continue;
            for (int x = marginLeft; x < marginLeft + graphW; x++)
                tex.SetPixel(x, y, gridColor);
        }

        int avgY = marginBottom + Mathf.Clamp(Mathf.RoundToInt((avgFps - fpsMin) / (fpsMax - fpsMin) * graphH), 0, graphH);
        for (int x = marginLeft; x < marginLeft + graphW; x++)
            tex.SetPixel(x, avgY, avgColor);

        int lowY = marginBottom + Mathf.Clamp(Mathf.RoundToInt((lowFps - fpsMin) / (fpsMax - fpsMin) * graphH), 0, graphH);
        for (int x = marginLeft; x < marginLeft + graphW; x++)
            tex.SetPixel(x, lowY, lowColor);

        int sampleCount = fpsData.Count;
        float samplesPerPixel = (float)sampleCount / graphW;
        int prevPx = -1, prevPy = -1;
        for (int px = 0; px < graphW; px++)
        {
            int sampleIdx = Mathf.Clamp(Mathf.RoundToInt(px * samplesPerPixel), 0, sampleCount - 1);
            float val = fpsData[sampleIdx];
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
        { normal = { background = MakeTex(2, 2, new Color(0.08f, 0.08f, 0.12f, 0.96f)) } };

        _titleStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
          normal = { textColor = Color.white } };

        _headerStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
          normal = { textColor = new Color(0.55f, 0.85f, 1f) } };

        _labelStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 16, normal = { textColor = new Color(0.75f, 0.75f, 0.80f) } };

        _valueLabelStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight,
          normal = { textColor = Color.white } };

        _smallLabelStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 12, normal = { textColor = new Color(0.55f, 0.55f, 0.60f) } };

        _scoreStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 36, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
          normal = { textColor = new Color(0.3f, 1f, 0.5f) } };

        _buttonStyle = new GUIStyle(GUI.skin.button)
        { fontSize = 24, fontStyle = FontStyle.Bold, fixedHeight = 55,
          normal = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.75f, 0.18f, 0.18f, 1f)) },
          hover  = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.90f, 0.25f, 0.25f, 1f)) },
          active = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.60f, 0.12f, 0.12f, 1f)) } };

        _hudStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 18, fontStyle = FontStyle.Bold,
          normal = { textColor = new Color(1f, 1f, 1f, 0.85f) } };
    }

    private void OnGUI()
    {
        InitStyles();

        if (CurrentPhase != Phase.FinalResults)
        {
            string phaseName = CurrentPhase switch
            {
                Phase.Phase1_Running => "Phase 1: Mosh Pit",
                Phase.Phase2_Running => "Phase 2: Space Rocks",
                Phase.Phase3_Running => "Phase 3: Corn Maze",
                _ => ""
            };
            float elapsed = CurrentPhase switch
            {
                Phase.Phase1_Running => _p1Elapsed,
                Phase.Phase2_Running => _p2Elapsed,
                Phase.Phase3_Running => _p3Elapsed,
                _ => 0f
            };
            float dur = CurrentPhase switch
            {
                Phase.Phase1_Running => phase1Duration,
                Phase.Phase2_Running => phase2Duration,
                Phase.Phase3_Running => phase3Duration,
                _ => 0f
            };
            string hudText = $"{phaseName}  |  FPS: {_hudFps:F0}  |  Time: {dur - elapsed:F1}s  |  Bodies: {_hudBodies}";
            GUI.Label(new Rect(10, 10, 700, 30), hudText, _hudStyle);
            return;
        }

        // ── Final combined results screen ──
        DrawFinalResults();
    }

    private void DrawFinalResults()
    {
        float panelW = 920;
        float panelH = Screen.height * 0.92f;
        float x = (Screen.width - panelW) / 2f;
        float y = (Screen.height - panelH) / 2f;

        // Dim
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
            MakeTex(1, 1, new Color(0, 0, 0, 0.75f)));

        GUI.Box(new Rect(x, y, panelW, panelH), GUIContent.none, _boxStyle);

        // Scrollable content
        float contentW = panelW - 60;
        float contentHeight = EstimateContentHeight();
        Rect viewRect = new Rect(x + 10, y + 10, panelW - 20, panelH - 20);
        Rect contentRect = new Rect(0, 0, contentW, contentHeight);

        _scrollPos = GUI.BeginScrollView(viewRect, _scrollPos, contentRect);

        float cx = 10;
        float cy = 5;

        // Title
        GUI.Label(new Rect(cx, cy, contentW, 40), "MoshPit Benchmark — Final Results", _titleStyle);
        cy += 48;

        // Geometric mean score
        float geomean = ComputeGeomean();
        GUI.Label(new Rect(cx, cy, contentW, 50), $"Overall Score: {geomean:F1} FPS", _scoreStyle);
        cy += 55;

        // Separator
        GUI.DrawTexture(new Rect(cx, cy, contentW, 1), MakeTex(1, 1, new Color(0.3f, 0.3f, 0.35f)));
        cy += 10;

        // Phase sections
        cy = DrawPhaseSection(cx, cy, contentW, "Phase 1: Mosh Pit", _s1);
        cy = DrawPhaseSection(cx, cy, contentW, "Phase 2: Space Rocks", _s2);
        cy = DrawPhaseSection(cx, cy, contentW, "Phase 3: Corn Maze", _s3);

        // Exit button
        cy += 10;
        float btnW = 220;
        if (GUI.Button(new Rect((contentW - btnW) / 2f, cy, btnW, 55), "Exit", _buttonStyle))
            QuitApp();
        cy += 70;

        GUI.EndScrollView();
    }

    private float DrawPhaseSection(float cx, float cy, float contentW, string title, PhaseStats s)
    {
        GUI.Label(new Rect(cx, cy, contentW, 28), title, _headerStyle);
        cy += 30;

        if (!s.valid)
        {
            GUI.Label(new Rect(cx + 20, cy, contentW, 22), "(Skipped)", _labelStyle);
            cy += 30;
            return cy;
        }

        float rowH = 24;
        float col1 = contentW * 0.55f;
        float col2 = contentW * 0.4f;
        float indent = cx + 20;

        DrawStatRowSmall(indent, cy, col1, col2, rowH, "Average FPS", $"{s.avg:F1}"); cy += rowH;
        DrawStatRowSmall(indent, cy, col1, col2, rowH, "1% Low FPS", $"{s.low:F1}"); cy += rowH;
        DrawStatRowSmall(indent, cy, col1, col2, rowH, "Min / Max FPS", $"{s.min:F1} / {s.max:F1}"); cy += rowH;
        DrawStatRowSmall(indent, cy, col1, col2, rowH, "Total Frames", $"{s.totalFrames}"); cy += rowH;
        DrawStatRowSmall(indent, cy, col1, col2, rowH, "Bodies", $"{s.totalBodies}"); cy += rowH;
        DrawStatRowSmall(indent, cy, col1, col2, rowH, "Duration", $"{s.duration:F1}s"); cy += rowH + 4;

        // Graph
        if (s.graph != null)
        {
            float graphW = contentW - 40;
            float graphH = graphW * (s.graph.height / (float)s.graph.width);
            GUI.DrawTexture(new Rect(indent, cy, graphW, graphH), s.graph);

            float legendY = cy + graphH + 2;
            GUI.Label(new Rect(indent, legendY, 100, 16), "— FPS", _smallLabelStyle);
            GUIStyle avgLeg = new GUIStyle(_smallLabelStyle) { normal = { textColor = new Color(1f, 0.85f, 0.25f) } };
            GUI.Label(new Rect(indent + 70, legendY, 100, 16), "— Avg", avgLeg);
            GUIStyle lowLeg = new GUIStyle(_smallLabelStyle) { normal = { textColor = new Color(1f, 0.3f, 0.3f) } };
            GUI.Label(new Rect(indent + 140, legendY, 100, 16), "— 1% Low", lowLeg);
            cy += graphH + 24;
        }

        // Separator
        GUI.DrawTexture(new Rect(cx, cy, contentW, 1), MakeTex(1, 1, new Color(0.25f, 0.25f, 0.3f)));
        cy += 10;

        return cy;
    }

    private void DrawStatRowSmall(float x, float y, float labelW, float valueW, float h, string label, string value)
    {
        GUI.Label(new Rect(x, y, labelW, h), label, _labelStyle);
        GUI.Label(new Rect(x + labelW, y, valueW, h), value, _valueLabelStyle);
    }

    private float ComputeGeomean()
    {
        List<float> avgs = new List<float>();
        if (_s1.valid) avgs.Add(_s1.avg);
        if (_s2.valid) avgs.Add(_s2.avg);
        if (_s3.valid) avgs.Add(_s3.avg);

        if (avgs.Count == 0) return 0f;

        double product = 1.0;
        foreach (float a in avgs) product *= a;
        return (float)Math.Pow(product, 1.0 / avgs.Count);
    }

    private float EstimateContentHeight()
    {
        float h = 130; // title + score + separator
        PhaseStats[] phases = { _s1, _s2, _s3 };
        foreach (var s in phases)
        {
            h += 30; // header
            if (!s.valid) { h += 30; continue; }
            h += 24 * 6 + 4; // stat rows
            if (s.graph != null)
            {
                float graphW = 820;
                h += graphW * (s.graph.height / (float)s.graph.width) + 24;
            }
            h += 10; // separator
        }
        h += 80; // exit button
        return h;
    }

    private static void QuitApp()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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
