using System.Collections.Generic;
using NUnit.Framework;

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
}
