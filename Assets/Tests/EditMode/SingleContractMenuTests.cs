using NUnit.Framework;

public sealed class SingleContractMenuTests
{
    [Test]
    public void MissingIdentityStartsFiveChoiceCalibration()
    {
        EchoMenuViewData menu =
            EchoRunPresentation.BuildSingleContractMenu(null);

        Assert.AreEqual("首次回声校准", menu.generation);
        Assert.AreEqual("完成 5 个正式路线选择", menu.learned);
        Assert.AreEqual("三条路线都能合理通过", menu.rule);
        Assert.AreEqual("到达终点，生成第 1 代回声", menu.objective);
        Assert.AreEqual("开始校准", menu.primaryAction);
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

        Assert.AreEqual("第 4 代回声", menu.generation);
        Assert.AreEqual("旧回声已保留", menu.learned);
        Assert.AreEqual("正在重建路线记忆", menu.rule);
        Assert.AreEqual("完成 5 个正式选择，建立第一条路线记忆",
            menu.objective);
        Assert.AreEqual("开始兼容校准", menu.primaryAction);
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

        Assert.AreEqual("第 3 代回声", menu.generation);
        Assert.AreEqual("它记住了：压力出现时，你偏向" + expectedLane,
            menu.learned);
        Assert.AreEqual("走它预料的路，它会获得距离优势；成功骗过它会失真；"
                        + "同一种骗法连续成功两次，它会追学一次",
            menu.rule);
        Assert.AreEqual("先到终点且领先者获胜", menu.objective);
        Assert.AreEqual("挑战第 3 代回声", menu.primaryAction);
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

        Assert.AreEqual("第 2 代回声", menu.generation);
        Assert.AreEqual("回声记忆模糊", menu.learned);
        Assert.AreEqual("你的选择尚未形成稳定模式", menu.rule);
        Assert.AreEqual("再完成 5 个正式选择，重建路线记忆",
            menu.objective);
        Assert.AreEqual("继续校准", menu.primaryAction);
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

        StringAssert.Contains("第 3 代回声", metrics);
        StringAssert.Contains("压力出现时，你偏向右侧", metrics);
        StringAssert.Contains("同一种骗法连续成功两次", summary);
        StringAssert.Contains("先到终点且领先者获胜", summary);
        foreach (string forbidden in new[]
                 {
                     "至少跳跃", "至少滑铲", "侦测", "暴露", "反抗",
                     "反扑", "稳定度", "契约锁死"
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
                     "0/100", "重写覆盖", "契约锁死", "未交锋"
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
