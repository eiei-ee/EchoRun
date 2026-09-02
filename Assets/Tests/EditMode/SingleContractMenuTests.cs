using NUnit.Framework;

public sealed class SingleContractMenuTests
{
    [Test]
    public void MissingIdentityStartsFiveChoiceCalibration()
    {
        EchoMenuViewData menu =
            EchoRunPresentation.BuildSingleContractMenu(null);

        Assert.AreEqual("你的操作，会变成下一局的对手", menu.generation);
        Assert.AreEqual("本机 AI 会实时观察你的选路、跳跃和滑铲",
            menu.learned);
        Assert.AreEqual("多做不同动作和选路，让学习条变亮", menu.rule);
        Assert.AreEqual("学习条亮后跑到终点，形成下一局的回声",
            menu.objective);
        Assert.AreEqual("开始第一局", menu.primaryAction);
        AssertNoLegacyContractLanguage(menu);
    }

    [Test]
    public void MigratedIdentityWithoutMemoryPreservesEchoAndRequestsRebuild()
    {
        ActiveEchoIdentity identity = new ActiveEchoIdentity
        {
            generation = 4,
            identityId = "legacy-echo",
            memoryContract = null
        };

        EchoMenuViewData menu =
            EchoRunPresentation.BuildSingleContractMenu(identity);

        Assert.AreEqual("第4代回声还在", menu.generation);
        Assert.AreEqual("AI 还没看清你的路线习惯", menu.learned);
        Assert.AreEqual("继续做不同动作和选路，让学习条变亮", menu.rule);
        Assert.AreEqual("学习条亮后跑到终点；旧回声不会丢",
            menu.objective);
        Assert.AreEqual("让它再观察一局", menu.primaryAction);
        AssertNoLegacyContractLanguage(menu);
    }

    [TestCase(0, "左侧")]
    [TestCase(1, "中间")]
    [TestCase(2, "右侧")]
    public void PreciseMemoryShowsGenerationAndObservedRoute(
        int preferredLane, string expectedLane)
    {
        ActiveEchoIdentity identity = IdentityWithMemory(
            generation: 3,
            preferredLane: preferredLane,
            confidence: 0.8f,
            evidenceCount: 3);

        EchoMenuViewData menu =
            EchoRunPresentation.BuildSingleContractMenu(identity);

        Assert.AreEqual("第3代回声", menu.generation);
        Assert.AreEqual("它记住了：压力出现时，你偏向" + expectedLane,
            menu.learned);
        Assert.AreEqual("它猜中会抢先；连续两次骗过它，它会改猜",
            menu.rule);
        Assert.AreEqual("先到终点，并把回声留在身后", menu.objective);
        Assert.AreEqual("挑战第3代回声", menu.primaryAction);
        AssertNoLegacyContractLanguage(menu);
    }

    [TestCase(2, 1f, 2)]
    [TestCase(0, 0.59f, 5)]
    public void ImpreciseMemoryNeverRevealsAPreferredLane(
        int preferredLane, float confidence, int evidenceCount)
    {
        ActiveEchoIdentity identity = IdentityWithMemory(
            generation: 2,
            preferredLane: preferredLane,
            confidence: confidence,
            evidenceCount: evidenceCount);

        EchoMenuViewData menu =
            EchoRunPresentation.BuildSingleContractMenu(identity);

        Assert.AreEqual("第2代回声还在", menu.generation);
        Assert.AreEqual("AI 还没看清你的路线习惯", menu.learned);
        Assert.AreEqual("继续做不同动作和选路，让学习条变亮", menu.rule);
        Assert.AreEqual("学习条亮后跑到终点；旧回声不会丢",
            menu.objective);
        Assert.AreEqual("让它再观察一局", menu.primaryAction);
        StringAssert.DoesNotContain("左侧", VisibleText(menu));
        StringAssert.DoesNotContain("中间", VisibleText(menu));
        StringAssert.DoesNotContain("右侧", VisibleText(menu));
        AssertNoLegacyContractLanguage(menu);
    }

    [Test]
    public void BuildingMenuDoesNotNormalizeTheSavedIdentityInPlace()
    {
        ActiveEchoIdentity identity = IdentityWithMemory(
            generation: 0,
            preferredLane: 9,
            confidence: 2f,
            evidenceCount: -1);

        EchoRunPresentation.BuildSingleContractMenu(identity);

        Assert.AreEqual(0, identity.generation);
        Assert.AreEqual(9, identity.memoryContract.preferredLane);
        Assert.AreEqual(2f, identity.memoryContract.confidence);
        Assert.AreEqual(-1, identity.memoryContract.evidenceCount);
    }

    [Test]
    public void FormalEchoReportUsesOnlySingleContractMemoryAndObjective()
    {
        ActiveEchoIdentity identity = IdentityWithMemory(
            generation: 3,
            preferredLane: 2,
            confidence: 0.8f,
            evidenceCount: 3);

        AITrainingDashboardUI.BuildSingleContractReport(identity,
            out string metrics, out string summary);

        StringAssert.Contains("第3代回声", metrics);
        StringAssert.Contains("压力出现时，你偏向右侧", metrics);
        StringAssert.Contains("连续两次骗过它", summary);
        StringAssert.Contains("把回声留在身后", summary);
        foreach (string forbidden in new[]
                 {
                     "至少跳跃", "至少滑铲", "侦测", "暴露", "反抗",
                     "反扑", "稳定度", "契约锁死", "校准", "契约",
                     "正式选择", "草稿", "身份", "采样", "追学",
                     "置信度", "路线认知"
                 })
            StringAssert.DoesNotContain(forbidden, metrics + summary);
    }

    private static ActiveEchoIdentity IdentityWithMemory(int generation,
        int preferredLane, float confidence, int evidenceCount)
    {
        const string identityId = "echo-identity";
        return new ActiveEchoIdentity
        {
            generation = generation,
            identityId = identityId,
            memoryContract = new EchoMemoryContract
            {
                contractId = "route-memory",
                identityId = identityId,
                preferredLane = preferredLane,
                confidence = confidence,
                evidenceCount = evidenceCount
            }
        };
    }

    private static void AssertNoLegacyContractLanguage(EchoMenuViewData menu)
    {
        string visible = VisibleText(menu);
        foreach (string forbidden in new[]
                 {
                     "侦测", "暴露", "反抗", "反扑", "阶段", "稳定度",
                     "0/100", "重写覆盖", "契约锁死", "未交锋",
                     "校准", "契约", "正式选择", "草稿", "身份",
                     "采样", "追学", "置信度", "路线认知"
                 })
            StringAssert.DoesNotContain(forbidden, visible);
    }

    private static string VisibleText(EchoMenuViewData menu)
    {
        return string.Join("|", new[]
        {
            menu.generation,
            menu.learned,
            menu.rule,
            menu.objective,
            menu.primaryAction
        });
    }
}
