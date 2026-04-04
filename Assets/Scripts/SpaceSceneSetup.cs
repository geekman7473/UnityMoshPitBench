using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sets up the space rocks scene: clears Phase 1 objects, creates 1000 asteroids
/// with n-body gravity, procedural textures, star skybox, and reconfigures the camera.
/// </summary>
public static class SpaceSceneSetup
{
    private const int AsteroidCount = 100;
    private const float MinRadius = 0.1f;
    private const float MaxRadius = 10f;   // 2 orders of magnitude
    private const float Density = 2f;      // uniform density for all rocks
    private const float SpawnRadius = 80f;  // initial spread
    private const float MinSpeed = 2f;
    private const float MaxSpeed = 6f;
    private const int TextureVariations = 8;
    private const int TextureSize = 128;

    public static void Setup()
    {
        CleanupPhase1();

        // Disable Unity gravity for space
        Physics.gravity = Vector3.zero;

        CreateStarSkybox();
        CreateAsteroids();
        CreateNBodySimulation();
        ReconfigureCamera();
        ReconfigureLight();
    }

    static void CleanupPhase1()
    {
        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();

        foreach (var root in roots)
        {
            string name = root.name;
            // Keep essential objects
            if (name == "Main Camera" || name == "Directional Light" ||
                name == "Global Volume" || name == "GameBootstrap" ||
                name == "BenchmarkManager")
                continue;

            Object.Destroy(root);
        }
    }

    static void CreateStarSkybox()
    {
        System.Random rng = new System.Random(99999);

        // Try to create a 6-sided skybox; fall back to cubemap approach
        Shader skyShader = Shader.Find("Skybox/6 Sided");
        if (skyShader == null) skyShader = Shader.Find("Skybox/Cubemap");
        if (skyShader == null) skyShader = Shader.Find("Skybox/Panoramic");

        if (skyShader == null)
        {
            // Last resort: grab shader from existing skybox material if any
            if (RenderSettings.skybox != null)
                skyShader = RenderSettings.skybox.shader;
        }

        if (skyShader == null || skyShader.name == "Skybox/Procedural")
        {
            // Can't do a textured skybox — just make camera solid dark blue
            Camera cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.01f, 0.01f, 0.03f, 1f);
            }
            Debug.LogWarning("SpaceSceneSetup: No suitable skybox shader found. Using solid color fallback.");
            return;
        }

        Material skyMat = new Material(skyShader);

        if (skyShader.name == "Skybox/6 Sided")
        {
            Texture2D[] faces = new Texture2D[6];
            for (int f = 0; f < 6; f++)
                faces[f] = GenerateStarFace(512, rng);

            skyMat.SetTexture("_FrontTex", faces[0]);
            skyMat.SetTexture("_BackTex", faces[1]);
            skyMat.SetTexture("_LeftTex", faces[2]);
            skyMat.SetTexture("_RightTex", faces[3]);
            skyMat.SetTexture("_UpTex", faces[4]);
            skyMat.SetTexture("_DownTex", faces[5]);
        }
        else if (skyShader.name == "Skybox/Cubemap")
        {
            Cubemap cubemap = new Cubemap(512, TextureFormat.RGBA32, false);
            CubemapFace[] cubeFaces = {
                CubemapFace.PositiveX, CubemapFace.NegativeX,
                CubemapFace.PositiveY, CubemapFace.NegativeY,
                CubemapFace.PositiveZ, CubemapFace.NegativeZ
            };
            for (int f = 0; f < 6; f++)
            {
                Texture2D faceTex = GenerateStarFace(512, rng);
                cubemap.SetPixels(faceTex.GetPixels(), cubeFaces[f]);
            }
            cubemap.Apply();
            skyMat.SetTexture("_Tex", cubemap);
        }
        else
        {
            // Panoramic fallback
            Texture2D pano = GenerateStarFace(2048, rng);
            skyMat.mainTexture = pano;
        }

        RenderSettings.skybox = skyMat;

        // Ensure camera renders the skybox
        Camera mainCam = Camera.main;
        if (mainCam != null)
            mainCam.clearFlags = CameraClearFlags.Skybox;
    }

    static Texture2D GenerateStarFace(int size, System.Random rng)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[size * size];

        // Very dark blue-black space background
        for (int i = 0; i < pixels.Length; i++)
        {
            float v = 0.01f + (float)rng.NextDouble() * 0.015f;
            pixels[i] = new Color(v * 0.7f, v * 0.7f, v * 1.1f, 1f);
        }

        // Many more stars, much brighter
        int starCount = rng.Next(800, 1200);
        for (int s = 0; s < starCount; s++)
        {
            int x = rng.Next(size);
            int y = rng.Next(size);
            float brightness = (float)rng.NextDouble();

            // Slight color temperature variation
            float temp = (float)rng.NextDouble();
            float r, g, b;
            if (temp < 0.2f) // blue-white hot stars
            {
                r = 0.7f + brightness * 0.3f;
                g = 0.8f + brightness * 0.2f;
                b = 1.0f;
            }
            else if (temp > 0.85f) // red/orange cool stars
            {
                r = 1.0f;
                g = 0.5f + brightness * 0.3f;
                b = 0.3f + brightness * 0.2f;
            }
            else // white/yellow main sequence
            {
                r = 0.95f + brightness * 0.05f;
                g = 0.93f + brightness * 0.07f;
                b = 0.85f + brightness * 0.15f;
            }

            // Stars range from dim to very bright
            float finalBrightness = 0.4f + brightness * 0.6f;
            Color starColor = new Color(r * finalBrightness, g * finalBrightness, b * finalBrightness, 1f);
            pixels[y * size + x] = starColor;

            // Brighter stars get a cross/glow pattern
            if (brightness > 0.4f)
            {
                float glow = finalBrightness * 0.5f;
                Color glowColor = new Color(r * glow, g * glow, b * glow, 1f);

                // 3x3 glow
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int gx = x + dx, gy = y + dy;
                        if (gx >= 0 && gx < size && gy >= 0 && gy < size)
                            pixels[gy * size + gx] = glowColor;
                    }
            }

            // Very bright stars get a larger 5x5 halo
            if (brightness > 0.8f)
            {
                float halo = finalBrightness * 0.2f;
                Color haloColor = new Color(r * halo, g * halo, b * halo, 1f);
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        if (Mathf.Abs(dx) <= 1 && Mathf.Abs(dy) <= 1) continue; // skip inner 3x3
                        int gx = x + dx, gy = y + dy;
                        if (gx >= 0 && gx < size && gy >= 0 && gy < size)
                            pixels[gy * size + gx] = haloColor;
                    }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    static void CreateAsteroids()
    {
        System.Random rng = new System.Random(67890);

        // Pre-generate texture variations
        Material[] asteroidMaterials = new Material[TextureVariations];
        for (int i = 0; i < TextureVariations; i++)
        {
            Texture2D tex = GenerateAsteroidTexture(TextureSize, rng);
            asteroidMaterials[i] = CreateAsteroidMaterial(tex);
        }

        // Shared physics material
        PhysicsMaterial asteroidPhysMat = new PhysicsMaterial("AsteroidPhysics");
        asteroidPhysMat.bounciness = 0.3f;
        asteroidPhysMat.dynamicFriction = 0.5f;
        asteroidPhysMat.staticFriction = 0.5f;
        asteroidPhysMat.bounceCombine = PhysicsMaterialCombine.Average;
        asteroidPhysMat.frictionCombine = PhysicsMaterialCombine.Average;

        float logMin = Mathf.Log(MinRadius);
        float logMax = Mathf.Log(MaxRadius);

        for (int i = 0; i < AsteroidCount; i++)
        {
            // Log-uniform size distribution (many small, few large)
            float radius = Mathf.Exp(logMin + (float)rng.NextDouble() * (logMax - logMin));

            // Mass = density * (4/3) * pi * r^3
            float volume = (4f / 3f) * Mathf.PI * radius * radius * radius;
            float mass = Density * volume;

            GameObject asteroid = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            asteroid.name = "Asteroid";
            asteroid.transform.localScale = Vector3.one * radius * 2f;

            // Random position within spawn sphere
            Vector3 pos = RandomInsideSphere(rng, SpawnRadius);
            asteroid.transform.position = pos;

            // Rigidbody
            Rigidbody rb = asteroid.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.useGravity = false;
            rb.linearDamping = 0f;
            rb.angularDamping = 0.05f;

            // Random initial velocity
            float speed = MinSpeed + (float)rng.NextDouble() * (MaxSpeed - MinSpeed);
            rb.linearVelocity = RandomOnSphere(rng) * speed;

            // Random initial spin
            rb.angularVelocity = RandomOnSphere(rng) * (float)rng.NextDouble() * 2f;

            // Physics material
            SphereCollider col = asteroid.GetComponent<SphereCollider>();
            col.material = asteroidPhysMat;

            // Visual material
            Renderer rend = asteroid.GetComponent<Renderer>();
            rend.material = asteroidMaterials[rng.Next(TextureVariations)];
        }
    }

    static Texture2D GenerateAsteroidTexture(int size, System.Random rng)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        float offsetX = (float)rng.NextDouble() * 1000f;
        float offsetY = (float)rng.NextDouble() * 1000f;
        float scale = 3f + (float)rng.NextDouble() * 5f;

        // Random base color in grey-brown range
        float baseR = 0.30f + (float)rng.NextDouble() * 0.15f;
        float baseG = 0.26f + (float)rng.NextDouble() * 0.10f;
        float baseB = 0.20f + (float)rng.NextDouble() * 0.08f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / size * scale + offsetX;
                float ny = (float)y / size * scale + offsetY;

                // Layered noise for rocky appearance
                float noise1 = Mathf.PerlinNoise(nx, ny);
                float noise2 = Mathf.PerlinNoise(nx * 2.5f, ny * 2.5f) * 0.4f;
                float noise3 = Mathf.PerlinNoise(nx * 6f, ny * 6f) * 0.15f;
                float combined = Mathf.Clamp01(noise1 + noise2 + noise3 - 0.15f);

                // Crater-like dark spots
                float crater = Mathf.PerlinNoise(nx * 1.2f + 500f, ny * 1.2f + 500f);
                if (crater < 0.35f) combined *= 0.6f;

                float r = baseR * combined;
                float g = baseG * combined;
                float b = baseB * combined;

                tex.SetPixel(x, y, new Color(r, g, b, 1f));
            }
        }

        tex.Apply();
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Repeat;
        return tex;
    }

    static Material CreateAsteroidMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.name = "AsteroidMaterial";
        mat.mainTexture = texture;
        mat.SetFloat("_Metallic", 0.05f);
        mat.SetFloat("_Smoothness", 0.15f);
        return mat;
    }

    static void CreateNBodySimulation()
    {
        GameObject nbodyGo = new GameObject("NBodySimulation");
        NBodySimulation nbody = nbodyGo.AddComponent<NBodySimulation>();
        nbody.gravitationalConstant = 0.5f;
        nbody.softening = 2f;
    }

    static void ReconfigureCamera()
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        OrbitCamera orbit = mainCam.GetComponent<OrbitCamera>();
        if (orbit != null)
        {
            orbit.focalPoint = Vector3.zero;
            orbit.orbitRadius = 150f;
            orbit.orbitSpeed = 10f;
            orbit.baseHeight = 40f;
            orbit.heightAmplitude = 35f;
            orbit.heightFrequency = 0.08f;
        }
    }

    static void ReconfigureLight()
    {
        // Make directional light dimmer for space ambiance
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        foreach (var light in lights)
        {
            if (light.type == LightType.Directional)
            {
                light.intensity = 1.2f;
                light.color = new Color(0.9f, 0.92f, 1f);
            }
        }

        // Darken ambient
        RenderSettings.ambientLight = new Color(0.05f, 0.05f, 0.08f);
    }

    // ── Helpers ────────────────────────────────────────────────────

    static Vector3 RandomInsideSphere(System.Random rng, float radius)
    {
        Vector3 v;
        do
        {
            v = new Vector3(
                (float)rng.NextDouble() * 2f - 1f,
                (float)rng.NextDouble() * 2f - 1f,
                (float)rng.NextDouble() * 2f - 1f
            );
        } while (v.sqrMagnitude > 1f);

        return v * radius;
    }

    static Vector3 RandomOnSphere(System.Random rng)
    {
        float theta = (float)rng.NextDouble() * Mathf.PI * 2f;
        float cosPhiRaw = (float)rng.NextDouble() * 2f - 1f;
        float sinPhi = Mathf.Sqrt(1f - cosPhiRaw * cosPhiRaw);
        return new Vector3(
            sinPhi * Mathf.Cos(theta),
            sinPhi * Mathf.Sin(theta),
            cosPhiRaw
        );
    }
}
