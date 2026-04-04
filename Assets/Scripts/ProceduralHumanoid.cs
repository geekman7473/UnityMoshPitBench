using UnityEngine;

/// <summary>
/// Builds a simple humanoid character from primitives with a legacy Animation
/// running cycle. All characters share the same AnimationClip for efficiency.
/// </summary>
public static class ProceduralHumanoid
{
    // Shared across all characters
    static AnimationClip _sharedRunClip;
    static Material[] _bodyMaterials;
    static Material _skinMat;
    static Material _pantsMat;

    static readonly Color[] BodyColors = new Color[]
    {
        new Color(0.85f, 0.15f, 0.15f), // red
        new Color(0.15f, 0.45f, 0.85f), // blue
        new Color(0.15f, 0.70f, 0.25f), // green
        new Color(0.90f, 0.60f, 0.10f), // orange
        new Color(0.60f, 0.15f, 0.70f), // purple
        new Color(0.20f, 0.75f, 0.75f), // cyan
        new Color(0.85f, 0.85f, 0.15f), // yellow
        new Color(0.85f, 0.35f, 0.55f), // pink
    };

    static void EnsureInitialized()
    {
        if (_sharedRunClip != null) return;

        _sharedRunClip = CreateRunAnimationClip();

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        // Skin material (shared)
        _skinMat = new Material(shader);
        _skinMat.color = new Color(0.87f, 0.72f, 0.53f);
        _skinMat.SetFloat("_Smoothness", 0.3f);

        // Pants material (shared)
        _pantsMat = new Material(shader);
        _pantsMat.color = new Color(0.2f, 0.2f, 0.3f);
        _pantsMat.SetFloat("_Smoothness", 0.2f);

        // Body/shirt color variations
        _bodyMaterials = new Material[BodyColors.Length];
        for (int i = 0; i < BodyColors.Length; i++)
        {
            _bodyMaterials[i] = new Material(shader);
            _bodyMaterials[i].color = BodyColors[i];
            _bodyMaterials[i].SetFloat("_Smoothness", 0.3f);
        }
    }

    /// <summary>
    /// Creates a humanoid character at the given position.
    /// Returns the root GameObject (caller adds NavMeshAgent etc.)
    /// </summary>
    public static GameObject Create(Vector3 position, System.Random rng)
    {
        EnsureInitialized();

        Material bodyMat = _bodyMaterials[rng.Next(_bodyMaterials.Length)];

        GameObject root = new GameObject("MazeAgent");
        root.transform.position = position;

        // Body (torso capsule)
        GameObject body = CreateCapsule("Body", root.transform,
            new Vector3(0f, 1.05f, 0f), new Vector3(0.35f, 0.45f, 0.35f), bodyMat);

        // Head (sphere)
        GameObject head = CreateSphere("Head", root.transform,
            new Vector3(0f, 1.55f, 0f), Vector3.one * 0.22f, _skinMat);

        // Arm joints (empty transforms at shoulder positions)
        GameObject leftArmJoint = CreateJoint("LeftArmJoint", root.transform,
            new Vector3(-0.25f, 1.30f, 0f));
        GameObject leftArm = CreateCapsule("LeftArm", leftArmJoint.transform,
            new Vector3(0f, -0.22f, 0f), new Vector3(0.1f, 0.25f, 0.1f), bodyMat);

        GameObject rightArmJoint = CreateJoint("RightArmJoint", root.transform,
            new Vector3(0.25f, 1.30f, 0f));
        GameObject rightArm = CreateCapsule("RightArm", rightArmJoint.transform,
            new Vector3(0f, -0.22f, 0f), new Vector3(0.1f, 0.25f, 0.1f), bodyMat);

        // Leg joints (empty transforms at hip positions)
        GameObject leftLegJoint = CreateJoint("LeftLegJoint", root.transform,
            new Vector3(-0.1f, 0.7f, 0f));
        GameObject leftLeg = CreateCapsule("LeftLeg", leftLegJoint.transform,
            new Vector3(0f, -0.33f, 0f), new Vector3(0.14f, 0.38f, 0.14f), _pantsMat);

        GameObject rightLegJoint = CreateJoint("RightLegJoint", root.transform,
            new Vector3(0.1f, 0.7f, 0f));
        GameObject rightLeg = CreateCapsule("RightLeg", rightLegJoint.transform,
            new Vector3(0f, -0.33f, 0f), new Vector3(0.14f, 0.38f, 0.14f), _pantsMat);

        // Animation
        Animation anim = root.AddComponent<Animation>();
        anim.AddClip(_sharedRunClip, "Run");
        anim.clip = _sharedRunClip;
        anim.playAutomatically = true;
        anim.Play("Run");

        // Random phase offset so characters aren't in sync
        anim["Run"].time = (float)rng.NextDouble() * _sharedRunClip.length;

        return root;
    }

    // ── Animation clip creation ────────────────────────────────────

    static AnimationClip CreateRunAnimationClip()
    {
        AnimationClip clip = new AnimationClip();
        clip.legacy = true;
        clip.name = "Run";
        clip.wrapMode = WrapMode.Loop;

        float cycleTime = 0.5f; // half-second run cycle

        // Leg swing: ±35 degrees around X axis
        float legSwing = 35f;
        AnimationCurve leftLegCurve = new AnimationCurve(
            new Keyframe(0f, legSwing),
            new Keyframe(cycleTime * 0.5f, -legSwing),
            new Keyframe(cycleTime, legSwing)
        );
        MakeSmooth(leftLegCurve);

        AnimationCurve rightLegCurve = new AnimationCurve(
            new Keyframe(0f, -legSwing),
            new Keyframe(cycleTime * 0.5f, legSwing),
            new Keyframe(cycleTime, -legSwing)
        );
        MakeSmooth(rightLegCurve);

        clip.SetCurve("LeftLegJoint", typeof(Transform), "localEulerAngles.x", leftLegCurve);
        clip.SetCurve("RightLegJoint", typeof(Transform), "localEulerAngles.x", rightLegCurve);

        // Arm swing: ±25 degrees, opposite to same-side leg (cross-lateral)
        float armSwing = 25f;
        AnimationCurve leftArmCurve = new AnimationCurve(
            new Keyframe(0f, -armSwing),
            new Keyframe(cycleTime * 0.5f, armSwing),
            new Keyframe(cycleTime, -armSwing)
        );
        MakeSmooth(leftArmCurve);

        AnimationCurve rightArmCurve = new AnimationCurve(
            new Keyframe(0f, armSwing),
            new Keyframe(cycleTime * 0.5f, -armSwing),
            new Keyframe(cycleTime, armSwing)
        );
        MakeSmooth(rightArmCurve);

        clip.SetCurve("LeftArmJoint", typeof(Transform), "localEulerAngles.x", leftArmCurve);
        clip.SetCurve("RightArmJoint", typeof(Transform), "localEulerAngles.x", rightArmCurve);

        // Slight body bob (up/down, two bounces per cycle)
        AnimationCurve bodyCurve = new AnimationCurve(
            new Keyframe(0f, 1.05f),
            new Keyframe(cycleTime * 0.25f, 1.08f),
            new Keyframe(cycleTime * 0.5f, 1.05f),
            new Keyframe(cycleTime * 0.75f, 1.08f),
            new Keyframe(cycleTime, 1.05f)
        );
        MakeSmooth(bodyCurve);
        clip.SetCurve("Body", typeof(Transform), "localPosition.y", bodyCurve);

        return clip;
    }

    static void MakeSmooth(AnimationCurve curve)
    {
        for (int i = 0; i < curve.keys.Length; i++)
            curve.SmoothTangents(i, 0f);
    }

    // ── Primitive helpers ──────────────────────────────────────────

    static GameObject CreateCapsule(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;

        // Remove collider (character collision handled by NavMeshAgent)
        Object.Destroy(go.GetComponent<Collider>());

        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static GameObject CreateSphere(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;

        Object.Destroy(go.GetComponent<Collider>());

        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static GameObject CreateJoint(string name, Transform parent, Vector3 localPos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go;
    }
}
