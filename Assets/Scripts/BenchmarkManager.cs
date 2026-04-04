using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Multi-phase benchmark manager.
/// Phase 1: Mosh Pit (pool balls in funnel)
/// Phase 2: Space Rocks (n-body asteroid sim)
/// Phase 3: Corn Maze (1000 AI agents pathfinding)
/// Shows results screen after each phase with FPS graph and stats.
/// </summary>
public class BenchmarkManager : MonoBehaviour
{
    public enum Phase { Phase1_Running, Phase1_Results, Phase2_Running, Phase2_Results, Phase3_Running, Phase3_Results }

    // ── configuration ──────────────────────────────────────────────
    [Header("Benchmark Settings")]
    public float phase1Duration = 45f;
    public float phase2Duration = 45f;
    public float phase3Duration = 45f;

    /// <summary>Set by SceneBootstrap to handle the transition to Phase 2.</summary>
    public Action onTransitionToPhase2;
    /// <summary>Set by SceneBootstrap to handle the transition to Phase 3.</summary>
    public Action onTransitionToPhase3;

    // ── state ──────────────────────────────────────────────────────
    public Phase CurrentPhase { get; private set; } = Phase.Phase1_Running;

    // Phase 1 data
    private readonly List<float> _p1FpsHistory = new List<float>();
    private float _p1Elapsed;
    private float _p1Avg, _p1Min, _p1Max, _p1Low;
    private int _p1Balls;
    private Texture2D _p1Graph;

    // Phase 2 data
    private readonly List<float> _p2FpsHistory = new List<float>();
    private float _p2Elapsed;
    private float _p2Avg, _p2Min, _p2Max, _p2Low;
    private int _p2Balls;
    private Texture2D _p2Graph;

    // Phase 3 data
    private readonly List<float> _p3FpsHistory = new List<float>();
    private float _p3Elapsed;
    private float _p3Avg, _p3Min, _p3Max, _p3Low;
    private int _p3Agents;
    private Texture2D _p3Graph;

    // HUD (updated periodically)
    private float _hudFps;
    private int _hudBalls;
    private float _hudUpdateTimer;
    private const float HudUpdateInterval = 0.25f;

    // Styles
    private GUIStyle _boxStyle, _titleStyle, _labelStyle, _valueLabelStyle;
    private GUIStyle _buttonStyle, _continueButtonStyle, _smallLabelStyle, _hudStyle;
    private bool _stylesInitialized;

    // ── MonoBehaviour ──────────────────────────────────────────────
    private void Update()
    {
        if (CurrentPhase == Phase.Phase1_Running)
        {
            _p1Elapsed += Time.unscaledDeltaTime;
            float fps = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            _p1FpsHistory.Add(fps);
            UpdateHud(fps);
            if (_p1Elapsed >= phase1Duration) FinishPhase1();
        }
        else if (CurrentPhase == Phase.Phase2_Running)
        {
            _p2Elapsed += Time.unscaledDeltaTime;
            float fps = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            _p2FpsHistory.Add(fps);
            UpdateHud(fps);
            if (_p2Elapsed >= phase2Duration) FinishPhase2();
        }
        else if (CurrentPhase == Phase.Phase3_Running)
        {
            _p3Elapsed += Time.unscaledDeltaTime;
            float fps = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            _p3FpsHistory.Add(fps);
            UpdateHud(fps);
            if (_p3Elapsed >= phase3Duration) FinishPhase3();
        }
    }

    private void UpdateHud(float fps)
    {
        _hudUpdateTimer -= Time.unscaledDeltaTime;
        if (_hudUpdateTimer <= 0f)
        {
            _hudFps = fps;
            _hudBalls = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length
                      + FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None).Length;
            _hudUpdateTimer = HudUpdateInterval;
        }
    }

    // ── Phase logic ────────────────────────────────────────────────
    private void FinishPhase1()
    {
        CurrentPhase = Phase.Phase1_Results;
        Time.timeScale = 0f;
        ComputeStats(_p1FpsHistory, out _p1Avg, out _p1Min, out _p1Max, out _p1Low);
        _p1Balls = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length;
        _p1Graph = BuildGraphTexture(_p1FpsHistory, _p1Avg, _p1Low, _p1Max, phase1Duration, 800, 300);
    }

    private void TransitionToPhase2()
    {
        onTransitionToPhase2?.Invoke();
        CurrentPhase = Phase.Phase2_Running;
        Time.timeScale = 1f;
        _hudUpdateTimer = 0f;
    }

    private void FinishPhase2()
    {
        CurrentPhase = Phase.Phase2_Results;
        Time.timeScale = 0f;
        ComputeStats(_p2FpsHistory, out _p2Avg, out _p2Min, out _p2Max, out _p2Low);
        _p2Balls = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length;
        _p2Graph = BuildGraphTexture(_p2FpsHistory, _p2Avg, _p2Low, _p2Max, phase2Duration, 800, 300);
    }

    private void TransitionToPhase3()
    {
        onTransitionToPhase3?.Invoke();
        CurrentPhase = Phase.Phase3_Running;
        Time.timeScale = 1f;
        _hudUpdateTimer = 0f;
    }

    private void FinishPhase3()
    {
        CurrentPhase = Phase.Phase3_Results;
        Time.timeScale = 0f;
        ComputeStats(_p3FpsHistory, out _p3Avg, out _p3Min, out _p3Max, out _p3Low);
        _p3Agents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None).Length;
        _p3Graph = BuildGraphTexture(_p3FpsHistory, _p3Avg, _p3Low, _p3Max, phase3Duration, 800, 300);
    }

    private void ComputeStats(List<float> fps, out float avg, out float min, out float max, out float low)
    {
        avg = fps.Average();
        min = fps.Min();
        max = fps.Max();
        var sorted = fps.OrderBy(f => f).ToList();
        int count1Pct = Mathf.Max(1, Mathf.FloorToInt(sorted.Count * 0.01f));
        low = sorted.Take(count1Pct).Average();
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

        int marginLeft = 50, marginBottom = 30, marginTop = 10, marginRight = 10;
        int graphW = width - marginLeft - marginRight;
        int graphH = height - marginBottom - marginTop;

        float fpsMin = 0;
        float fpsMax = Mathf.Ceil(maxFps / 10f) * 10f;
        if (fpsMax < 10) fpsMax = 10;

        // Grid lines
        float gridStep = Mathf.Max(10, Mathf.Round(fpsMax / 6f / 10f) * 10f);
        for (float g = 0; g <= fpsMax; g += gridStep)
        {
            int y = marginBottom + Mathf.RoundToInt((g - fpsMin) / (fpsMax - fpsMin) * graphH);
            if (y < 0 || y >= height) continue;
            for (int x = marginLeft; x < marginLeft + graphW; x++)
                tex.SetPixel(x, y, gridColor);
        }

        // Avg line
        int avgY = marginBottom + Mathf.Clamp(Mathf.RoundToInt((avgFps - fpsMin) / (fpsMax - fpsMin) * graphH), 0, graphH);
        for (int x = marginLeft; x < marginLeft + graphW; x++)
        {
            tex.SetPixel(x, avgY, avgColor);
            if (avgY + 1 < height) tex.SetPixel(x, avgY + 1, avgColor);
        }

        // 1% Low line
        int lowY = marginBottom + Mathf.Clamp(Mathf.RoundToInt((lowFps - fpsMin) / (fpsMax - fpsMin) * graphH), 0, graphH);
        for (int x = marginLeft; x < marginLeft + graphW; x++)
        {
            tex.SetPixel(x, lowY, lowColor);
            if (lowY + 1 < height) tex.SetPixel(x, lowY + 1, lowColor);
        }

        // FPS data line
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
        { normal = { background = MakeTex(2, 2, new Color(0.08f, 0.08f, 0.12f, 0.96f)) } };

        _titleStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
          normal = { textColor = Color.white } };

        _labelStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 22, normal = { textColor = new Color(0.75f, 0.75f, 0.80f) } };

        _valueLabelStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight,
          normal = { textColor = Color.white } };

        _smallLabelStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 14, normal = { textColor = new Color(0.55f, 0.55f, 0.60f) } };

        _buttonStyle = new GUIStyle(GUI.skin.button)
        { fontSize = 24, fontStyle = FontStyle.Bold, fixedHeight = 55,
          normal = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.75f, 0.18f, 0.18f, 1f)) },
          hover  = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.90f, 0.25f, 0.25f, 1f)) },
          active = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.60f, 0.12f, 0.12f, 1f)) } };

        _continueButtonStyle = new GUIStyle(GUI.skin.button)
        { fontSize = 24, fontStyle = FontStyle.Bold, fixedHeight = 55,
          normal = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.18f, 0.55f, 0.25f, 1f)) },
          hover  = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.25f, 0.70f, 0.35f, 1f)) },
          active = { textColor = Color.white, background = MakeTex(2, 2, new Color(0.12f, 0.40f, 0.18f, 1f)) } };

        _hudStyle = new GUIStyle(GUI.skin.label)
        { fontSize = 18, fontStyle = FontStyle.Bold,
          normal = { textColor = new Color(1f, 1f, 1f, 0.85f) } };
    }

    private void OnGUI()
    {
        InitStyles();

        switch (CurrentPhase)
        {
            case Phase.Phase1_Running:
                DrawHud("Phase 1: Mosh Pit", phase1Duration - _p1Elapsed);
                break;
            case Phase.Phase1_Results:
                DrawResultsScreen("Phase 1: Mosh Pit — Results",
                    _p1Avg, _p1Low, _p1Min, _p1Max, _p1FpsHistory.Count, _p1Balls,
                    phase1Duration, _p1Graph, continueLabel: "Continue to Phase 2");
                break;
            case Phase.Phase2_Running:
                DrawHud("Phase 2: Space Rocks", phase2Duration - _p2Elapsed);
                break;
            case Phase.Phase2_Results:
                DrawResultsScreen("Phase 2: Space Rocks — Results",
                    _p2Avg, _p2Low, _p2Min, _p2Max, _p2FpsHistory.Count, _p2Balls,
                    phase2Duration, _p2Graph, continueLabel: "Continue to Phase 3");
                break;
            case Phase.Phase3_Running:
                DrawHud("Phase 3: Corn Maze", phase3Duration - _p3Elapsed);
                break;
            case Phase.Phase3_Results:
                DrawResultsScreen("Phase 3: Corn Maze \u2014 Results",
                    _p3Avg, _p3Low, _p3Min, _p3Max, _p3FpsHistory.Count, _p3Agents,
                    phase3Duration, _p3Graph, continueLabel: null);
                break;
        }
    }

    private void DrawHud(string phaseName, float timeLeft)
    {
        string hudText = $"{phaseName}  |  FPS: {_hudFps:F0}  |  Time: {timeLeft:F1}s  |  Bodies: {_hudBalls}";
        GUI.Label(new Rect(10, 10, 700, 30), hudText, _hudStyle);
    }

    private void DrawResultsScreen(string title, float avg, float low, float min, float max,
        int totalFrames, int totalBalls, float duration, Texture2D graph, string continueLabel = null, bool showContinue = false)
    {
        float panelW = 880;
        float panelH = 700;
        float x = (Screen.width - panelW) / 2f;
        float y = (Screen.height - panelH) / 2f;

        // Dim
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height),
            MakeTex(1, 1, new Color(0, 0, 0, 0.65f)));

        GUI.Box(new Rect(x, y, panelW, panelH), GUIContent.none, _boxStyle);

        float cx = x + 40;
        float cy = y + 15;
        float contentW = panelW - 80;

        GUI.Label(new Rect(cx, cy, contentW, 45), title, _titleStyle);
        cy += 52;

        float rowH = 34;
        DrawStatRow(cx, cy, contentW, rowH, "Average FPS", $"{avg:F1}"); cy += rowH;
        DrawStatRow(cx, cy, contentW, rowH, "1% Low FPS", $"{low:F1}"); cy += rowH;
        DrawStatRow(cx, cy, contentW, rowH, "Min FPS", $"{min:F1}"); cy += rowH;
        DrawStatRow(cx, cy, contentW, rowH, "Max FPS", $"{max:F1}"); cy += rowH;
        DrawStatRow(cx, cy, contentW, rowH, "Total Frames", $"{totalFrames}"); cy += rowH;
        DrawStatRow(cx, cy, contentW, rowH, "Total Bodies", $"{totalBalls}"); cy += rowH;
        DrawStatRow(cx, cy, contentW, rowH, "Duration", $"{duration:F0}s"); cy += rowH + 8;

        if (graph != null)
        {
            float graphW = contentW;
            float graphH = graphW * (graph.height / (float)graph.width);
            GUI.DrawTexture(new Rect(cx, cy, graphW, graphH), graph);

            float legendY = cy + graphH + 4;
            GUI.Label(new Rect(cx, legendY, 200, 20), "— FPS    ", _smallLabelStyle);

            GUIStyle avgLegend = new GUIStyle(_smallLabelStyle) { normal = { textColor = new Color(1f, 0.85f, 0.25f) } };
            GUI.Label(new Rect(cx + 80, legendY, 200, 20), "— Avg", avgLegend);

            GUIStyle lowLegend = new GUIStyle(_smallLabelStyle) { normal = { textColor = new Color(1f, 0.30f, 0.30f) } };
            GUI.Label(new Rect(cx + 160, legendY, 200, 20), "— 1% Low", lowLegend);

            GUI.Label(new Rect(cx, legendY + 16, 80, 20), "0 s", _smallLabelStyle);
            GUIStyle rightAligned = new GUIStyle(_smallLabelStyle) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(cx + graphW - 80, legendY + 16, 80, 20), $"{duration:F0} s", rightAligned);

            cy += graphH + 50;
        }

        // Buttons
        float btnW = 260;
        if (continueLabel != null)
        {
            float gap = 20;
            float totalBtnW = btnW * 2 + gap;
            float btnX = x + (panelW - totalBtnW) / 2f;

            if (GUI.Button(new Rect(btnX, cy, btnW, 55), continueLabel, _continueButtonStyle))
            {
                if (CurrentPhase == Phase.Phase1_Results)
                    TransitionToPhase2();
                else if (CurrentPhase == Phase.Phase2_Results)
                    TransitionToPhase3();
            }
            if (GUI.Button(new Rect(btnX + btnW + gap, cy, btnW, 55), "Exit", _buttonStyle))
            {
                QuitApp();
            }
        }
        else
        {
            if (GUI.Button(new Rect(x + (panelW - btnW) / 2f, cy, btnW, 55), "Exit", _buttonStyle))
            {
                QuitApp();
            }
        }
    }

    private void DrawStatRow(float x, float y, float w, float h, string label, string value)
    {
        GUI.Label(new Rect(x, y, w * 0.6f, h), label, _labelStyle);
        GUI.Label(new Rect(x + w * 0.6f, y, w * 0.4f, h), value, _valueLabelStyle);
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
