using System;
using NUnit.Framework;

public sealed class SingleContractHudTests
{
    [Test]
    public void VisualStatesAreIndependentFromLegacyDuelPhases()
    {
        Array values = Enum.GetValues(typeof(SingleContractVisualState));

        Assert.AreEqual(4, values.Length);
        CollectionAssert.AreEqual(new[]
        {
            SingleContractVisualState.Calibration,
            SingleContractVisualState.Challenge,
            SingleContractVisualState.RelearnPulse,
            SingleContractVisualState.Finale
        }, values);

        foreach (var field in typeof(SingleContractHudInput).GetFields())
            Assert.AreNotEqual(typeof(EchoDuelPhase), field.FieldType);
        foreach (var field in typeof(SingleContractHudData).GetFields())
            Assert.AreNotEqual(typeof(EchoDuelPhase), field.FieldType);
    }

    [Test]
    public void ChallengeHudContainsOnlySingleContractSignals()
    {
        SingleContractHudData data = EchoRunPresentation.BuildSingleContractHud(
            new SingleContractHudInput
            {
                visualState = SingleContractVisualState.Challenge,
                memory = "压力下偏向右侧",
                showPrediction = true,
                predictedLane = 2,
                predictionGateNumber = 2,
                predictionGateCount = 6,
                leadMeters = 3.26f,
                injuries = 2,
                finishRemaining = 42.2f,
                powerUp = "护盾",
                instantFeedback = SingleContractInstantFeedback.CounterFailed,
                feedbackLeadDeltaMeters = -4.5f,
                feedbackSequence = 7,
                result = "相同记忆等待重试"
            });

        Assert.AreEqual(SingleContractVisualState.Challenge, data.visualState);
        Assert.IsFalse(data.showMemory,
            "Frozen identity memory is an opening card, not a live route instruction.");
        Assert.AreEqual("回声记忆：压力下偏向右侧", data.memory);
        Assert.AreEqual(
            "第2/6门 · 下一门预测：右侧路线\n红=预测　青=反制　白=安全",
            data.prediction);
        Assert.AreEqual(SingleContractLeadState.PlayerLeading, data.leadState);
        Assert.AreEqual("玩家领先：3.3米", data.lead);
        Assert.AreEqual(2, data.injuries);
        Assert.AreEqual("受伤次数：2", data.injuriesText);
        Assert.AreEqual(42.2f, data.finishRemaining, 0.0001f);
        Assert.AreEqual("终点距离：43米", data.finishRemainingText);
        Assert.IsTrue(data.showPowerUp);
        Assert.AreEqual("当前补给：护盾", data.powerUp);
        Assert.AreEqual("反制失败 · 回声 +4.5米", data.instantFeedback);
        Assert.AreEqual(7, data.feedbackSequence);
        Assert.AreEqual("相同记忆等待重试", data.result);

        string visible = string.Join("|", new[]
        {
            data.memory, data.prediction, data.lead, data.injuriesText,
            data.finishRemainingText, data.powerUp, data.instantFeedback,
            data.result
        });
        foreach (string forbidden in new[]
                 {
                     "侦测", "暴露", "反抗", "反扑", "阶段轨道",
                     "稳定度", "0/100", "重写覆盖", "契约锁死", "未交锋"
                 })
            StringAssert.DoesNotContain(forbidden, visible);
    }

    [Test]
    public void OpeningMemoryShowsOnlyGenerationAndFrozenMemoryCopy()
    {
        SingleContractHudData data = EchoRunPresentation.BuildSingleContractHud(
            new SingleContractHudInput
            {
                visualState = SingleContractVisualState.Challenge,
                openingMemory = true,
                generation = 3,
                memory = "压力出现时，你偏向右侧",
                showPrediction = true,
                predictedLane = 2,
                powerUp = "护盾",
                instantFeedback =
                    SingleContractInstantFeedback.RewriteSucceeded
            });

        Assert.IsTrue(data.openingMemory);
        Assert.IsTrue(data.showMemory);
        Assert.AreEqual(3, data.generation);
        Assert.AreEqual("第3代回声记忆\n压力出现时，你偏向右侧",
            data.memory);
        Assert.IsEmpty(data.prediction);
        Assert.IsFalse(data.showPowerUp);
        Assert.AreEqual(SingleContractInstantFeedback.None,
            data.instantFeedbackKind);
        Assert.IsEmpty(data.instantFeedback);
    }

    [TestCase(SingleContractInstantFeedback.PredictionHit, -3.2f,
        "预判命中 · 回声 +3.2米")]
    [TestCase(SingleContractInstantFeedback.RewriteSucceeded, 5.4f,
        "改写成功 · 玩家 +5.4米")]
    [TestCase(SingleContractInstantFeedback.SafePass,
        0f, "安全通过 · 距离不变")]
    [TestCase(SingleContractInstantFeedback.CounterFailed, -6.8f,
        "反制失败 · 回声 +6.8米")]
    [TestCase(SingleContractInstantFeedback.EchoRelearned,
        0f, "回声追学 · 预测更新")]
    public void InstantFeedbackUsesOnlyTheFivePlayerFacingMessages(
        SingleContractInstantFeedback feedback, float leadDeltaMeters,
        string expected)
    {
        SingleContractHudData data = EchoRunPresentation.BuildSingleContractHud(
            new SingleContractHudInput
            {
                visualState = SingleContractVisualState.RelearnPulse,
                instantFeedback = feedback,
                feedbackLeadDeltaMeters = leadDeltaMeters
            });

        Assert.AreEqual(feedback, data.instantFeedbackKind);
        Assert.AreEqual(expected, data.instantFeedback);
    }

    [Test]
    public void CalibrationHidesPredictionAndClampsRunCounters()
    {
        SingleContractHudData data = EchoRunPresentation.BuildSingleContractHud(
            new SingleContractHudInput
            {
                visualState = SingleContractVisualState.Calibration,
                memory = "",
                showPrediction = true,
                predictedLane = 2,
                leadMeters = -0.02f,
                injuries = -3,
                finishRemaining = -12f,
                powerUp = "",
                instantFeedback = SingleContractInstantFeedback.None,
                feedbackSequence = -4
            });

        Assert.AreEqual("回声记忆模糊 · 路线尚未稳定", data.memory);
        Assert.IsTrue(data.showMemory,
            "Calibration still needs the memory-building explanation.");
        Assert.IsEmpty(data.prediction);
        Assert.AreEqual(SingleContractLeadState.Tied, data.leadState);
        Assert.AreEqual("并驾齐驱：0.0米", data.lead);
        Assert.AreEqual(0, data.injuries);
        Assert.AreEqual(0f, data.finishRemaining);
        Assert.AreEqual("终点已到达", data.finishRemainingText);
        Assert.IsFalse(data.showPowerUp);
        Assert.AreEqual("当前补给：无", data.powerUp);
        Assert.IsEmpty(data.instantFeedback);
        Assert.AreEqual(0, data.feedbackSequence);
    }

    [Test]
    public void ImpreciseMemoryUsesOneShortNonDuplicatedLabel()
    {
        SingleContractHudData data = EchoRunPresentation.BuildSingleContractHud(
            new SingleContractHudInput
            {
                visualState = SingleContractVisualState.Calibration,
                memory = "回声记忆模糊\n你的选择尚未形成稳定模式"
            });

        Assert.AreEqual("回声记忆模糊 · 路线尚未稳定", data.memory);
        StringAssert.DoesNotContain("回声记忆：回声记忆", data.memory);
        StringAssert.DoesNotContain("\n", data.memory);
    }

    [Test]
    public void FinaleKeepsEchoLeadAndResultCopyExplicit()
    {
        SingleContractHudData data = EchoRunPresentation.BuildSingleContractHud(
            new SingleContractHudInput
            {
                visualState = SingleContractVisualState.Finale,
                memory = "回声记忆：压力下偏向中间",
                showPrediction = true,
                predictedLane = 1,
                predictionGateNumber = 6,
                predictionGateCount = 6,
                predictionGateActive = true,
                leadMeters = -4.44f,
                finishRemaining = 8f,
                result = "第3代回声胜出"
            });

        Assert.AreEqual("回声记忆：压力下偏向中间", data.memory);
        Assert.IsFalse(data.showMemory);
        Assert.AreEqual(
            "第6/6门 · 当前门预测：中间路线\n红=预测　青=反制　白=安全",
            data.prediction);
        Assert.AreEqual(SingleContractLeadState.EchoLeading, data.leadState);
        Assert.AreEqual("回声领先：4.4米", data.lead);
        Assert.AreEqual("第3代回声胜出", data.result);
    }
}
