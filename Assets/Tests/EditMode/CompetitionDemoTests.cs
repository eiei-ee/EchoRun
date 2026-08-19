using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class CompetitionDemoTests
{
    // ---------- EchoDemoMode: URL command parsing ----------

    [Test]
    public void UrlCommandSeedsOnlyForExplicitDemoTokens()
    {
        Assert.AreEqual(EchoDemoMode.DemoCommand.Seed,
            EchoDemoMode.ParseUrlCommand("https://example.com/game/?demo=echo"));
        Assert.AreEqual(EchoDemoMode.DemoCommand.Seed,
            EchoDemoMode.ParseUrlCommand("https://example.com/game/?DEMO=1"));
        Assert.AreEqual(EchoDemoMode.DemoCommand.Seed,
            EchoDemoMode.ParseUrlCommand("https://example.com/#demo=true"));
        Assert.AreEqual(EchoDemoMode.DemoCommand.Restore,
            EchoDemoMode.ParseUrlCommand("https://example.com/?demo=off"));
        Assert.AreEqual(EchoDemoMode.DemoCommand.Restore,
            EchoDemoMode.ParseUrlCommand("https://example.com/?a=1&demo=0&b=2"));
    }

    [Test]
    public void UrlCommandIgnoresLookalikeParameters()
    {
        Assert.AreEqual(EchoDemoMode.DemoCommand.None,
            EchoDemoMode.ParseUrlCommand("https://example.com/?demoecho=1"));
        Assert.AreEqual(EchoDemoMode.DemoCommand.None,
            EchoDemoMode.ParseUrlCommand("https://example.com/?demo=echox"));
        Assert.AreEqual(EchoDemoMode.DemoCommand.None,
            EchoDemoMode.ParseUrlCommand("https://example.com/?xdemo=off"));
        Assert.AreEqual(EchoDemoMode.DemoCommand.None,
            EchoDemoMode.ParseUrlCommand("https://example.com/"));
        Assert.AreEqual(EchoDemoMode.DemoCommand.None,
            EchoDemoMode.ParseUrlCommand(null));
        Assert.AreEqual(EchoDemoMode.DemoCommand.None,
            EchoDemoMode.ParseUrlCommand(""));
    }

    [Test]
    public void AutoStartOnlyForExplicitPlayTokens()
    {
        Assert.IsTrue(EchoDemoMode.ParseAutoStart(
            "https://example.com/?demo=echo&play=1"));
        Assert.IsTrue(EchoDemoMode.ParseAutoStart(
            "https://example.com/?autostart=true"));
        Assert.IsFalse(EchoDemoMode.ParseAutoStart(
            "https://example.com/?play=0"));
        Assert.IsFalse(EchoDemoMode.ParseAutoStart(
            "https://example.com/?xplay=1"));
        Assert.IsFalse(EchoDemoMode.ParseAutoStart(
            "https://example.com/?demo=echo"));
    }

    // ---------- EchoDemoMode: seeded persona and archive ----------

    [Test]
    public void DemoStyleProducesTheReadableRightLaneContract()
    {
        PlayerStyleData style = EchoDemoMode.BuildDemoStyle();

        EchoContractData contract = EchoContractPolicy.Create(style, 1);

        Assert.AreEqual(EchoContractType.BreakLaneHabit, contract.type);
        Assert.AreEqual(2, contract.learnedLane);
        Assert.AreEqual(0, contract.targetLane);
        Assert.IsFalse(contract.exploratory,
            "Seeded demo must show an identified habit, not AI exploration.");
    }

    [Test]
    public void DemoPolicyWeightsAreDeterministicAndWellFormed()
    {
        float[] first = EchoDemoMode.TrainDemoPolicyWeights();
        float[] second = EchoDemoMode.TrainDemoPolicyWeights();

        Assert.AreEqual(AIShadowPolicy.ActionCount * AIShadowPolicy.FeatureCount,
            first.Length);
        Assert.AreEqual(first, second,
            "Seeded training must be reproducible for every judge.");
        bool anyNonZero = false;
        for (int i = 0; i < first.Length; i++)
        {
            Assert.GreaterOrEqual(first[i], -4f);
            Assert.LessOrEqual(first[i], 4f);
            anyNonZero |= first[i] != 0f;
        }
        Assert.IsTrue(anyNonZero);
    }

    [Test]
    public void DemoShadowBehavesLikeTheSeededPersona()
    {
        var policy = new AIShadowPolicy(EchoDemoMode.TrainDemoPolicyWeights());

        // Feature layout: {1, lane-1, speed, proximity, relativeLane,
        // ((int)obstacleType+1)/3, jumping, sliding}.
        var highThreat = new[] { 1f, 0f, 0.5f, 0.9f, 0f, 2f / 3f, 0f, 0f };
        var lowThreat = new[] { 1f, 0f, 0.5f, 0.9f, 0f, 1f / 3f, 0f, 0f };
        var barrierLeftLane = new[] { 1f, -1f, 0.5f, 0.9f, 0f, 1f, 0f, 0f };
        var calm = new[] { 1f, 0f, 0.4f, 0f, 0f, 0f, 0f, 0f };

        Assert.AreEqual((int)ShadowAction.Jump, policy.Predict(highThreat),
            "Trained shadow must jump over high obstacles.");
        Assert.AreEqual((int)ShadowAction.Slide, policy.Predict(lowThreat),
            "Trained shadow must slide under low obstacles.");
        Assert.AreEqual((int)ShadowAction.Right, policy.Predict(barrierLeftLane),
            "Right-lane-biased persona dodges barriers to the right.");
        Assert.AreEqual((int)ShadowAction.Keep, policy.Predict(calm),
            "Shadow holds its lane when nothing threatens it.");
    }

    [Test]
    public void DemoProfileJsonIsCompleteAndInternallyConsistent()
    {
        string json = EchoDemoMode.BuildDemoShadowProfileJson();
        var profile = JsonUtility.FromJson<EchoDemoMode.DemoShadowProfile>(json);

        Assert.AreEqual(EchoDemoMode.DemoProfileVersion, profile.version);
        Assert.AreEqual(EchoDemoMode.DemoGeneration, profile.generation);
        Assert.AreEqual(104, profile.sampleCount);
        Assert.AreEqual(70, profile.activeSampleCount);
        int actionTotal = 0;
        for (int i = 0; i < profile.actionCounts.Length; i++)
            actionTotal += profile.actionCounts[i];
        Assert.AreEqual(profile.sampleCount, actionTotal,
            "Action counts must sum to the advertised sample count.");
        Assert.AreEqual(AIShadowPolicy.ActionCount * AIShadowPolicy.FeatureCount,
            profile.weights.Length);
        Assert.AreEqual(EchoDemoMode.DemoPace, profile.pace, 0.001f);
        Assert.AreEqual(EchoDemoMode.DemoClarity, profile.clarity, 0.001f);
        Assert.AreEqual(EchoDemoMode.DemoBestProgress, profile.bestProgress, 0.001f);

        var snapshot = JsonUtility.FromJson<EchoGenerationSnapshot>(
            profile.activeGenerationJson);
        Assert.AreEqual(EchoDemoMode.DemoGeneration, snapshot.generation);
        Assert.AreEqual(profile.weights, snapshot.policyWeights);
        Assert.AreEqual(0.72f, snapshot.GetStyle().lanePreference, 0.001f);
    }

    [Test]
    public void DemoProfileMirrorMatchesTheRuntimeProfileLayout()
    {
        // The demo seeds JSON consumed by AIShadowRunner's private profile
        // class; this reflection guard fails loudly if the runtime layout
        // ever drifts from the demo mirror.
        Type runtimeProfile = typeof(AIShadowRunner)
            .GetNestedType("ShadowProfile", BindingFlags.NonPublic);
        Assert.IsNotNull(runtimeProfile,
            "AIShadowRunner.ShadowProfile was renamed or removed.");

        var runtimeFields = FieldNames(runtimeProfile);
        var demoFields = FieldNames(typeof(EchoDemoMode.DemoShadowProfile));
        CollectionAssert.AreEquivalent(runtimeFields, demoFields,
            "DemoShadowProfile must mirror ShadowProfile field for field.");
    }

    // ---------- EchoAtmosphereDirector ----------

    [Test]
    public void EveryDuelPhaseMapsToItsOwnAtmosphereMood()
    {
        Assert.AreEqual(EchoAtmosphereMood.Detection,
            EchoAtmosphereDirector.MoodForPhase(EchoDuelPhase.Detection));
        Assert.AreEqual(EchoAtmosphereMood.Reveal,
            EchoAtmosphereDirector.MoodForPhase(EchoDuelPhase.Reveal));
        Assert.AreEqual(EchoAtmosphereMood.Resistance,
            EchoAtmosphereDirector.MoodForPhase(EchoDuelPhase.Resistance));
        Assert.AreEqual(EchoAtmosphereMood.Counterattack,
            EchoAtmosphereDirector.MoodForPhase(EchoDuelPhase.Counterattack));
        Assert.AreEqual(EchoAtmosphereMood.Rewrite,
            EchoAtmosphereDirector.MoodForPhase(EchoDuelPhase.Rewrite));
        Assert.AreEqual(EchoAtmosphereMood.Finale,
            EchoAtmosphereDirector.MoodForPhase(EchoDuelPhase.Finale));
        Assert.AreEqual(EchoAtmosphereMood.Neutral,
            EchoAtmosphereDirector.MoodForPhase(EchoDuelPhase.None));
        Assert.AreEqual(EchoAtmosphereMood.Neutral,
            EchoAtmosphereDirector.MoodForPhase(EchoDuelPhase.Calibration));
        Assert.AreEqual(EchoAtmosphereMood.Neutral,
            EchoAtmosphereDirector.MoodForPhase(EchoDuelPhase.Finished));
    }

    [Test]
    public void NeutralPresetReplaysTheBaselineExactly()
    {
        EchoAtmospherePreset baseline = MakeBaseline();

        EchoAtmospherePreset neutral = EchoAtmosphereDirector.PresetForMood(
            EchoAtmosphereMood.Neutral, baseline);

        Assert.AreEqual(baseline.fogColor, neutral.fogColor);
        Assert.AreEqual(baseline.fogStartDistance, neutral.fogStartDistance);
        Assert.AreEqual(baseline.fogEndDistance, neutral.fogEndDistance);
        Assert.AreEqual(baseline.ambientSkyColor, neutral.ambientSkyColor);
        Assert.AreEqual(baseline.keyLightColor, neutral.keyLightColor);
        Assert.AreEqual(baseline.keyLightIntensity, neutral.keyLightIntensity);
        Assert.AreEqual(baseline.skyboxTint, neutral.skyboxTint);
        Assert.AreEqual(baseline.skyboxExposure, neutral.skyboxExposure);
    }

    [Test]
    public void MoodPresetsGradeInTheRightColorDirections()
    {
        EchoAtmospherePreset baseline = MakeBaseline();

        EchoAtmospherePreset counter = EchoAtmosphereDirector.PresetForMood(
            EchoAtmosphereMood.Counterattack, baseline);
        Assert.Greater(counter.fogColor.r, counter.fogColor.g,
            "Counterattack fog must lean coral red.");
        Assert.Greater(counter.fogColor.r, counter.fogColor.b);

        EchoAtmospherePreset rewrite = EchoAtmosphereDirector.PresetForMood(
            EchoAtmosphereMood.Rewrite, baseline);
        Assert.Greater(rewrite.fogColor.b, rewrite.fogColor.r,
            "Rewrite fog must lean bright cyan.");
        Assert.Greater(rewrite.fogColor.g, rewrite.fogColor.r);

        EchoAtmospherePreset finale = EchoAtmosphereDirector.PresetForMood(
            EchoAtmosphereMood.Finale, baseline);
        Assert.Greater(finale.keyLightColor.r, finale.keyLightColor.b,
            "Finale key light must turn gold.");
        Assert.Greater(finale.keyLightColor.g, finale.keyLightColor.b);

        EchoAtmospherePreset defeat = EchoAtmosphereDirector.PresetForMood(
            EchoAtmosphereMood.Defeat, baseline);
        Assert.Less(defeat.keyLightIntensity, baseline.keyLightIntensity,
            "Defeat must visibly dim the scene.");

        EchoAtmospherePreset victory = EchoAtmosphereDirector.PresetForMood(
            EchoAtmosphereMood.Victory, baseline);
        Assert.Greater(victory.keyLightIntensity, finale.keyLightIntensity,
            "Victory should burn brighter than the finale standoff.");

        foreach (EchoAtmosphereMood mood in Enum.GetValues(
            typeof(EchoAtmosphereMood)))
        {
            EchoAtmospherePreset preset =
                EchoAtmosphereDirector.PresetForMood(mood, baseline);
            Assert.Less(preset.fogStartDistance, preset.fogEndDistance,
                mood + " fog distances must stay ordered.");
        }
    }

    [Test]
    public void GameOverBackdropKeepsVictoryAndDefeatGradesVisible()
    {
        Assert.GreaterOrEqual(UIManager.GameOverBackdropAlpha, 0.70f);
        Assert.LessOrEqual(UIManager.GameOverBackdropAlpha, 0.82f,
            "The full-screen result overlay must not hide the atmosphere grade.");
    }

    // ---------- EchoEnvironmentKit + WorldStyler fallback ----------

    [Test]
    public void MissingKitPiecesResolveToNullWithoutErrors()
    {
        Assert.IsFalse(EchoEnvironmentKit.Has("NoSuchPiece_404"));
        Assert.IsFalse(EchoEnvironmentKit.Has(""));
        Assert.IsFalse(EchoEnvironmentKit.Has(null));

        var parent = new GameObject("KitTestParent");
        try
        {
            Assert.IsNull(EchoEnvironmentKit.Spawn(
                "NoSuchPiece_404", parent.transform, Vector3.zero));
            Assert.IsNull(EchoEnvironmentKit.Spawn(
                "NoSuchPiece_404", null, Vector3.zero));
            // Repeated lookups hit the missing-piece cache instead of
            // hammering Resources.Load on every segment.
            Assert.IsFalse(EchoEnvironmentKit.Has("NoSuchPiece_404"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(parent);
        }
    }

    [Test]
    public void WorldStylerFallsBackToPrimitivesWhenKitIsAbsent()
    {
        var stylerObject = new GameObject("WorldStyler_Test");
        var segment = new GameObject("Segment_Test");
        string[] kitPieces =
        {
            "Arch", "CityCard_A", "CityCard_B", "CityCard_C", "Halo",
            "HaloPylon", "Island", "Pylon_L", "Pylon_M", "Pylon_S",
            "PylonAccent_L", "PylonAccent_M", "PylonAccent_S", "Totem_A",
            "Totem_B"
        };
        FieldInfo missingField = typeof(EchoEnvironmentKit).GetField(
            "_missing", BindingFlags.NonPublic | BindingFlags.Static);
        FieldInfo cacheField = typeof(EchoEnvironmentKit).GetField(
            "_cache", BindingFlags.NonPublic | BindingFlags.Static);
        var missing = (HashSet<string>)missingField.GetValue(null);
        var cache = (Dictionary<string, GameObject>)cacheField.GetValue(null);
        var previouslyMissing = new HashSet<string>();
        var cachedPieces = new Dictionary<string, GameObject>();
        for (int i = 0; i < kitPieces.Length; i++)
        {
            string piece = kitPieces[i];
            if (!missing.Add(piece)) previouslyMissing.Add(piece);
            if (cache.TryGetValue(piece, out GameObject cached))
            {
                cachedPieces[piece] = cached;
                cache.Remove(piece);
            }
        }
        try
        {
            WorldStyler styler = stylerObject.AddComponent<WorldStyler>();
            styler.DecorateSegment(segment, TrackSegmentType.Straight);

            Transform environment = segment.transform.Find("EchoEnvironment");
            Assert.IsNotNull(environment);

            Assert.IsNotNull(FindDeep(environment, "LeftIsland"),
                "Primitive island fallback must still build.");
            Assert.IsNotNull(FindDeep(environment, "PylonFoot"),
                "Primitive pylon fallback must still build.");
            Assert.IsNull(FindDeep(environment, "Pylon_S"),
                "Kit pieces must not appear while the FBX kit is missing.");
            Assert.IsNull(FindDeep(environment, "CityCard_A"),
                "Backdrop cards stay absent without the kit.");

            // Decorating twice must not duplicate the environment.
            styler.DecorateSegment(segment, TrackSegmentType.Straight);
            Assert.AreEqual(1, segment.transform.childCount);
        }
        finally
        {
            for (int i = 0; i < kitPieces.Length; i++)
            {
                string piece = kitPieces[i];
                if (!previouslyMissing.Contains(piece)) missing.Remove(piece);
            }
            foreach (KeyValuePair<string, GameObject> pair in cachedPieces)
                cache[pair.Key] = pair.Value;
            UnityEngine.Object.DestroyImmediate(segment);
            UnityEngine.Object.DestroyImmediate(stylerObject);
        }
    }

    // ---------- helpers ----------

    private static EchoAtmospherePreset MakeBaseline()
    {
        return new EchoAtmospherePreset
        {
            fogColor = new Color(0.055f, 0.105f, 0.17f),
            fogStartDistance = 52f,
            fogEndDistance = 130f,
            ambientSkyColor = new Color(0.28f, 0.38f, 0.52f),
            ambientEquatorColor = new Color(0.12f, 0.19f, 0.28f),
            ambientGroundColor = new Color(0.03f, 0.05f, 0.08f),
            keyLightColor = new Color(1f, 0.93f, 0.84f),
            keyLightIntensity = 1.12f,
            skyboxTint = new Color(0.50f, 0.64f, 0.82f),
            skyboxExposure = 0.42f
        };
    }

    private static List<string> FieldNames(Type type)
    {
        var names = new List<string>();
        FieldInfo[] fields = type.GetFields(
            BindingFlags.Public | BindingFlags.Instance);
        for (int i = 0; i < fields.Length; i++)
            names.Add(fields[i].Name);
        return names;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == name) return child;
            Transform found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
