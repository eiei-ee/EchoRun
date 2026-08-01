using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class ProgressionAndTrainingTests
{
    [Test]
    public void BalanceDefinesExactlyFourPurchasablePowerUps()
    {
        GameBalanceData balance = GameBalanceConfig.Current;

        Assert.IsNotNull(balance.powerUps);
        Assert.AreEqual(4, balance.powerUps.Length);
        var ids = new HashSet<string>();
        foreach (PowerUpBalance powerUp in balance.powerUps)
        {
            Assert.IsNotNull(powerUp);
            Assert.Greater(powerUp.cost, 0);
            Assert.IsTrue(ids.Add(powerUp.id), "Power-up IDs must be unique.");
        }
    }

    [Test]
    public void TrainingReportShowsBeforeAfterAndDominantAction()
    {
        var telemetry = new AIRunTelemetryData
        {
            completed = true,
            finishReason = "game_over",
            shadowGenerationAtStart = 2,
            shadowGenerationAtEnd = 3,
            directorUpdatesAtStart = 10,
            directorUpdatesAtEnd = 14,
            playerSkillAtStart = 0.42f,
            playerSkillAtEnd = 0.55f,
            shadowWeightsAtStart = new[] { 0f, 0.2f },
            shadowWeightsAtEnd = new[] { 0.2f, 0.4f },
            directorWeightsAtStart = new[] { 0f },
            directorWeightsAtEnd = new[] { 0.3f }
        };
        telemetry.shadowSamples.Add(new AIShadowTrainingSample { action = (int)ShadowAction.Jump });
        telemetry.shadowSamples.Add(new AIShadowTrainingSample { action = (int)ShadowAction.Jump });
        telemetry.shadowSamples.Add(new AIShadowTrainingSample { action = (int)ShadowAction.Left });

        AITrainingReport report = AITrainingReportBuilder.FromTelemetry(telemetry);

        Assert.IsNotNull(report);
        Assert.AreEqual(2, report.generationBefore);
        Assert.AreEqual(3, report.generationAfter);
        Assert.AreEqual(4, report.directorUpdatesAfter - report.directorUpdatesBefore);
        Assert.AreEqual("跳跃", report.learnedAction);
        Assert.AreEqual(0.2f, report.shadowWeightDelta, 0.0001f);
        StringAssert.Contains("本代重点学习", report.summary);
    }

    [Test]
    public void TrainingReportRejectsIncompleteRun()
    {
        Assert.IsNull(AITrainingReportBuilder.FromTelemetry(
            new AIRunTelemetryData { completed = false }));
    }

    [Test]
    public void TrainingReportOnlyCountsPlayerTrainingSamples()
    {
        var telemetry = new AIRunTelemetryData
        {
            completed = true,
            finishReason = "game_over"
        };
        telemetry.shadowSamples.Add(new AIShadowTrainingSample
        {
            action = (int)ShadowAction.Jump
        });
        for (int i = 0; i < 4; i++)
        {
            telemetry.shadowSamples.Add(new AIShadowTrainingSample
            {
                action = (int)ShadowAction.Left,
                opponentDecision = true
            });
        }

        AITrainingReport report = AITrainingReportBuilder.FromTelemetry(telemetry);

        Assert.AreEqual("跳跃", report.learnedAction);
        Assert.AreEqual(1, report.actionSamples[(int)ShadowAction.Jump]);
        Assert.AreEqual(0, report.actionSamples[(int)ShadowAction.Left]);
    }

    [Test]
    public void TrainingReportExplainsWhenNoPlayerSampleWasLearned()
    {
        var telemetry = new AIRunTelemetryData
        {
            completed = true,
            finishReason = "game_over"
        };
        telemetry.shadowSamples.Add(new AIShadowTrainingSample
        {
            action = (int)ShadowAction.Slide,
            opponentDecision = true
        });

        AITrainingReport report = AITrainingReportBuilder.FromTelemetry(telemetry);

        Assert.AreEqual("暂无有效动作", report.learnedAction);
        StringAssert.Contains("没有采集到新的玩家动作样本", report.summary);
    }

    [TestCase("menu")]
    [TestCase("restart")]
    public void TrainingReportRejectsAbandonedRun(string finishReason)
    {
        Assert.IsNull(AITrainingReportBuilder.FromTelemetry(
            new AIRunTelemetryData
            {
                completed = true,
                finishReason = finishReason
            }));
    }

    [Test]
    public void SkillProfileOnlyCountsCompletedRuns()
    {
        var profile = new AIPlayerSkillProfile();

        profile.RecordRunEnd(120f, false);
        Assert.AreEqual(0, profile.completedRuns);
        Assert.AreEqual(0f, profile.bestDistance);

        profile.RecordRunEnd(95f, true);
        Assert.AreEqual(1, profile.completedRuns);
        Assert.AreEqual(95f, profile.bestDistance);
    }

    [Test]
    public void SaveNormalizationClearsSelectionWithoutInventory()
    {
        FieldInfo dataField = typeof(EchoRunSaveSystem).GetField(
            "_data", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo normalize = typeof(EchoRunSaveSystem).GetMethod(
            "Normalize", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = dataField.GetValue(null);
        try
        {
            dataField.SetValue(null, new EchoRunSaveData
            {
                powerUpInventory = new[] { 0, 0, 0, 0 },
                selectedPowerUp = 2
            });

            normalize.Invoke(null, null);

            var normalized = (EchoRunSaveData)dataField.GetValue(null);
            Assert.AreEqual(-1, normalized.selectedPowerUp);
        }
        finally
        {
            dataField.SetValue(null, previous);
        }
    }

    [Test]
    public void RestoringEmptyArchiveRemovesStaleLegacyShadowProfile()
    {
        const string legacyKey = "AIShadowProfileV1";
        bool hadValue = PlayerPrefs.HasKey(legacyKey);
        string previousValue = PlayerPrefs.GetString(legacyKey, "");
        FieldInfo dataField = typeof(EchoRunSaveSystem).GetField(
            "_data", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo restore = typeof(EchoRunSaveSystem).GetMethod(
            "RestoreLegacyKeys", BindingFlags.Static | BindingFlags.NonPublic);
        object previousData = dataField.GetValue(null);
        try
        {
            PlayerPrefs.SetString(legacyKey, "stale-training-data");
            dataField.SetValue(null, new EchoRunSaveData { shadowProfileJson = "" });

            restore.Invoke(null, null);

            Assert.IsFalse(PlayerPrefs.HasKey(legacyKey));
        }
        finally
        {
            if (hadValue) PlayerPrefs.SetString(legacyKey, previousValue);
            else PlayerPrefs.DeleteKey(legacyKey);
            dataField.SetValue(null, previousData);
        }
    }
}
