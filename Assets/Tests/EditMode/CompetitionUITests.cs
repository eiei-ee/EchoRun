using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class CompetitionUITests
{
    // ---------- ShouldShowContractBriefing ----------

    [Test]
    public void BriefingOnlyForExistingContractWithoutAutoStart()
    {
        Assert.IsFalse(EchoRunPresentation.ShouldShowContractBriefing(0, false));
        Assert.IsFalse(EchoRunPresentation.ShouldShowContractBriefing(0, true));
        Assert.IsTrue(EchoRunPresentation.ShouldShowContractBriefing(1, false));
        Assert.IsTrue(EchoRunPresentation.ShouldShowContractBriefing(4, false));
        Assert.IsFalse(EchoRunPresentation.ShouldShowContractBriefing(4, true));
    }

    // ---------- BuildBriefing: calibration branch ----------

    [Test]
    public void CalibrationBriefingListsThreeRowsAndGoals()
    {
        EchoBriefingViewData view = EchoRunPresentation.BuildBriefing(
            0, null, null, 0f, 12, 8);

        StringAssert.Contains("校准", view.title);
        Assert.AreEqual("开始校准", view.primaryAction);
        Assert.AreEqual(3, view.rows.Length);
        StringAssert.Contains("12", view.rows[1].value);
        StringAssert.Contains("8", view.rows[1].value);
        StringAssert.Contains("第 1 代", view.rows[2].value);
        StringAssert.Contains("75", view.footnote);
    }

    // ---------- BuildBriefing: challenge branch ----------

    [Test]
    public void ChallengeBriefingBuildsContractFromStyleWhenPreviewMissing()
    {
        PlayerStyleData persona = new PlayerStyleData
        {
            lanePreference = 1f,
            laneSamples = 20,
            verticalActionSamples = 12,
            jumpActionSamples = 7,
            slideActionSamples = 5,
            rhythmSamples = 10
        };
        EchoBriefingViewData view = EchoRunPresentation.BuildBriefing(
            1, persona, null, 0.87f, 12, 8);

        StringAssert.Contains("第 1 代", view.title);
        Assert.AreEqual("开跑", view.primaryAction);
        Assert.AreEqual(4, view.rows.Length);
        Assert.AreEqual("AI 识别", view.rows[0].label);
        StringAssert.DoesNotContain("AI识别：", view.rows[0].value);
        Assert.IsNotEmpty(view.rows[0].value);
        Assert.AreEqual("本代规则", view.rows[1].label);
        Assert.IsNotEmpty(view.rows[1].value);
        Assert.AreEqual("破解目标", view.rows[2].label);
        Assert.IsNotEmpty(view.rows[2].value);
        Assert.AreEqual("87%", view.rows[3].value);
        StringAssert.Contains("侦测", view.footnote);
    }

    [Test]
    public void ChallengeBriefingUsesProvidedContractPreview()
    {
        PlayerStyleData persona = new PlayerStyleData
        {
            lanePreference = 1f,
            laneSamples = 20
        };
        EchoContractData contract = EchoContractPolicy.Create(persona, 2);
        EchoBriefingViewData view = EchoRunPresentation.BuildBriefing(
            2, persona, contract, 0.5f, 12, 8);

        Assert.AreEqual(contract.ruleDescription, view.rows[1].value);
        Assert.AreEqual(contract.objective, view.rows[2].value);
        Assert.AreEqual("50%", view.rows[3].value);
    }

    [Test]
    public void ChallengeBriefingClampsClarityPercent()
    {
        EchoBriefingViewData high = EchoRunPresentation.BuildBriefing(
            1, null, null, 1.4f, 12, 8);
        EchoBriefingViewData low = EchoRunPresentation.BuildBriefing(
            1, null, null, -0.2f, 12, 8);

        Assert.AreEqual("100%", high.rows[3].value);
        Assert.AreEqual("0%", low.rows[3].value);
    }

    // ---------- BuildPhaseBanner ----------

    [Test]
    public void BannerHiddenForNonInterruptingPhases()
    {
        Assert.IsNull(EchoRunPresentation.BuildPhaseBanner(EchoDuelPhase.None));
        Assert.IsNull(EchoRunPresentation.BuildPhaseBanner(
            EchoDuelPhase.Calibration));
        Assert.IsNull(EchoRunPresentation.BuildPhaseBanner(
            EchoDuelPhase.Finished));
    }

    [Test]
    public void BannerTitlesMatchDuelPhaseNames()
    {
        Assert.AreEqual("侦测", EchoRunPresentation
            .BuildPhaseBanner(EchoDuelPhase.Detection).title);
        Assert.AreEqual("暴露", EchoRunPresentation
            .BuildPhaseBanner(EchoDuelPhase.Reveal).title);
        Assert.AreEqual("反抗", EchoRunPresentation
            .BuildPhaseBanner(EchoDuelPhase.Resistance).title);
        Assert.AreEqual("反扑", EchoRunPresentation
            .BuildPhaseBanner(EchoDuelPhase.Counterattack).title);
        Assert.AreEqual("重写", EchoRunPresentation
            .BuildPhaseBanner(EchoDuelPhase.Rewrite).title);
        Assert.AreEqual("决胜", EchoRunPresentation
            .BuildPhaseBanner(EchoDuelPhase.Finale).title);
    }

    [Test]
    public void BannerAccentsComeFromThemeAndPointTheRightWay()
    {
        EchoPhaseBannerData counter = EchoRunPresentation
            .BuildPhaseBanner(EchoDuelPhase.Counterattack);
        EchoPhaseBannerData rewrite = EchoRunPresentation
            .BuildPhaseBanner(EchoDuelPhase.Rewrite);
        EchoPhaseBannerData finale = EchoRunPresentation
            .BuildPhaseBanner(EchoDuelPhase.Finale);

        Assert.AreEqual(EchoRunUITheme.PhaseCounterattack, counter.accent);
        Assert.AreEqual(EchoRunUITheme.PhaseRewrite, rewrite.accent);
        Assert.AreEqual(EchoRunUITheme.PhaseFinale, finale.accent);

        // 反扑偏暖（珊瑚红），重写偏冷（亮青），决胜偏暖（金）。
        Assert.Greater(counter.accent.r, counter.accent.b);
        Assert.Greater(rewrite.accent.b, rewrite.accent.r);
        Assert.Greater(rewrite.accent.g, rewrite.accent.r);
        Assert.Greater(finale.accent.r, finale.accent.b);
    }

    [Test]
    public void BannerSubtitlesAreFilledAndAccentsDistinct()
    {
        EchoDuelPhase[] phases =
        {
            EchoDuelPhase.Detection, EchoDuelPhase.Reveal,
            EchoDuelPhase.Resistance, EchoDuelPhase.Counterattack,
            EchoDuelPhase.Rewrite, EchoDuelPhase.Finale
        };
        HashSet<Color> accents = new HashSet<Color>();
        foreach (EchoDuelPhase phase in phases)
        {
            EchoPhaseBannerData banner =
                EchoRunPresentation.BuildPhaseBanner(phase);
            Assert.IsNotEmpty(banner.subtitle);
            Assert.IsTrue(accents.Add(banner.accent),
                "phase accent duplicated for " + phase);
        }
    }

    // ---------- BuildTrainingReportRows ----------

    [Test]
    public void ReportRowsAreEmptyForMissingReport()
    {
        Assert.AreEqual(0,
            EchoRunPresentation.BuildTrainingReportRows(null).Length);
    }

    [Test]
    public void ReportRowsSummarizePromotionAndDrift()
    {
        AITrainingReport report = new AITrainingReport
        {
            generationBefore = 1,
            generationAfter = 2,
            shadowWeightDelta = 0.12f,
            directorWeightDelta = 0.034f,
            skillBefore = 0.5f,
            skillAfter = 0.6f,
            learnedAction = "跳跃",
            actionSamples = new[] { 1, 2, 3, 4, 5 }
        };
        EchoReportRow[] rows =
            EchoRunPresentation.BuildTrainingReportRows(report);

        Assert.AreEqual(5, rows.Length);
        Assert.AreEqual("代际", rows[0].label);
        StringAssert.Contains("第 1 代", rows[0].value);
        StringAssert.Contains("第 2 代", rows[0].value);
        Assert.AreEqual("跳跃", rows[1].value);
        Assert.AreEqual("影子 ±12% · 导演 ±3%", rows[2].value);
        Assert.AreEqual("+10%", rows[3].value);
        Assert.AreEqual("15 个动作样本", rows[4].value);
    }

    [Test]
    public void ReportRowsHandleStagnantAndSparseRuns()
    {
        AITrainingReport report = new AITrainingReport
        {
            generationBefore = 3,
            generationAfter = 3,
            skillBefore = 0.5f,
            skillAfter = 0.5f,
            learnedAction = null,
            actionSamples = new int[5]
        };
        EchoReportRow[] rows =
            EchoRunPresentation.BuildTrainingReportRows(report);

        StringAssert.Contains("未晋升", rows[0].value);
        Assert.AreEqual("待观察", rows[1].value);
        Assert.AreEqual("评估稳定", rows[3].value);
        Assert.AreEqual("样本不足", rows[4].value);
    }

    // ---------- EchoIconSet ----------

    [Test]
    public void IconSetReturnsNullForBlankAndMissingNames()
    {
        Assert.IsNull(EchoIconSet.Get(null));
        Assert.IsNull(EchoIconSet.Get(""));
        Assert.IsFalse(EchoIconSet.Has(null));

        Assert.IsNull(EchoIconSet.Get("__missing_unit_test_icon__"));
        // The miss is cached, so a second lookup must stay quiet and null.
        Assert.IsNull(EchoIconSet.Get("__missing_unit_test_icon__"));
        Assert.IsFalse(EchoIconSet.Has("__missing_unit_test_icon__"));
    }

    [Test]
    public void IconSetShipsEveryIconTheUiReferences()
    {
        string[] required =
        {
            "jump", "slide", "left", "right", "hold", "echo", "contract",
            "generation", "pace", "clarity", "stability", "lead", "shard",
            "victory", "defeat", "detection", "reveal", "resistance",
            "counterattack", "rewrite", "finale"
        };
        foreach (string icon in required)
            Assert.IsTrue(EchoIconSet.Has(icon),
                "missing icon resource: " + icon);
    }
}
