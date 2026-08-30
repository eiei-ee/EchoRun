using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class SingleContractProductionUiTests
{
    [Test]
    public void PredictionGateWorldMarkerBuildsThreeColliderFreeRouteBands()
    {
        GameObject managerObject = new GameObject("TrackManager_Test");
        GameObject segment = new GameObject("Segment_Test");
        try
        {
            TrackManager manager = managerObject.AddComponent<TrackManager>();
            manager.segmentLength = 20f;
            manager.laneDistance = 3f;
            TrackSegmentData data = segment.AddComponent<TrackSegmentData>();
            data.routeDistance = 100f;
            var gate = new PredictionGateDefinition
            {
                gateId = 1,
                sequence = 1,
                commitDistance = 96f,
                resolveDistance = 104f,
                lanes = new[]
                {
                    Lane(0, PredictionGateRole.Predicted),
                    Lane(1, PredictionGateRole.Counter),
                    Lane(2, PredictionGateRole.Neutral)
                }
            };
            MethodInfo spawn = typeof(TrackManager).GetMethod(
                "SpawnPredictionGateVisual",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(spawn);
            spawn.Invoke(manager, new object[] { segment, gate, 100f });

            Transform root = segment.transform.Find(
                TrackManager.PredictionGateVisualRootName);
            Assert.IsNotNull(root,
                "Every formal gate needs an unmistakable world marker.");
            Assert.AreEqual(3, root.childCount,
                "Predicted, counter and neutral routes each need a band.");
            float obstacleLocalZ = 4f;
            Assert.GreaterOrEqual(obstacleLocalZ - root.localPosition.z,
                TrackManager.PredictionGateMinimumObstacleClearance,
                "A cross-segment marker must not be clamped against its obstacle.");
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
                Assert.IsFalse(colliders[index].enabled,
                    "Route markers must never change gameplay collisions.");
            for (int laneIndex = 0; laneIndex < root.childCount; laneIndex++)
            {
                Transform lane = root.GetChild(laneIndex);
                Transform ribbon = lane.Find("ApproachRibbon");
                Transform decisionBand = lane.Find("DecisionBand");
                Assert.IsNotNull(ribbon);
                Assert.IsNotNull(decisionBand);
                Assert.AreEqual(TrackManager.PredictionGateRibbonWidth,
                    ribbon.localScale.x, 0.0001f,
                    "The route ribbon should read as a center guide, not a lane carpet.");
                Assert.AreEqual(TrackManager.PredictionGateRibbonLength,
                    ribbon.localScale.z, 0.0001f,
                    "The route ribbon should be a compact marker, not a long runway.");
                Assert.AreEqual(
                    -TrackManager.PredictionGateRibbonLength * 0.5f,
                    ribbon.localPosition.z, 0.0001f,
                    "The compact ribbon should end at the decision band.");
                Assert.AreEqual(
                    TrackManager.PredictionGateDecisionBandWidth,
                    decisionBand.localScale.x, 0.0001f,
                    "The decision band should leave visible road on both sides.");
                Assert.IsNull(lane.Find("LeftPost"));
                Assert.IsNull(lane.Find("RightPost"));
                Assert.IsNull(lane.Find("TopBeam"));
            }

            Color predicted = TrackManager.PredictionGateRoleColor(
                PredictionGateRole.Predicted);
            Color counter = TrackManager.PredictionGateRoleColor(
                PredictionGateRole.Counter);
            Color neutral = TrackManager.PredictionGateRoleColor(
                PredictionGateRole.Neutral);
            Assert.AreNotEqual(predicted, counter);
            Assert.AreNotEqual(counter, neutral);
            Assert.AreNotEqual(predicted, neutral);
        }
        finally
        {
            Object.DestroyImmediate(segment);
            Object.DestroyImmediate(managerObject);
        }
    }

    [Test]
    public void ChallengeResultTitlesDescribeTheRaceWinner()
    {
        Assert.AreEqual("你跑赢了第3代回声",
            UIManager.GetSingleContractGameOverTitle(
                "你跑赢了第3代回声\n第4代回声已经形成",
                RunEndReason.FinishReached, true, true));
        Assert.AreEqual("第3代回声胜出",
            UIManager.GetSingleContractGameOverTitle(
                "第3代回声胜出\n相同记忆等待重试",
                RunEndReason.FinishReached, true, false));

        string visible = UIManager.GetSingleContractGameOverTitle(
            "你跑赢了第3代回声\n第4代回声已经形成",
            RunEndReason.FinishReached, true, true);
        StringAssert.DoesNotContain("契约完成", visible);
        StringAssert.DoesNotContain("稳定度", visible);
        StringAssert.DoesNotContain("阶段", visible);
    }

    [Test]
    public void CalibrationResultTitlesStayInCalibrationLanguage()
    {
        Assert.AreEqual("校准完成",
            UIManager.GetSingleContractGameOverTitle(
                "校准完成\n第1代回声已经形成",
                RunEndReason.FinishReached, false, false));
        Assert.AreEqual("校准未完成",
            UIManager.GetSingleContractGameOverTitle(
                "回声记忆模糊\n请再完成一次短校准",
                RunEndReason.FinishReached, false, false));
        Assert.AreEqual("身份结算失败",
            UIManager.GetSingleContractGameOverTitle(
                AIShadowRunner.BuildSingleContractSaveFailureResult(
                    RunEndReason.FinishReached, false, false, 0),
                RunEndReason.FinishReached, false, false));
        Assert.AreEqual("身份结算失败",
            UIManager.GetSingleContractGameOverTitle(
                "你跑赢了第3代回声\n身份结算保存失败",
                RunEndReason.FinishReached, true, true));
    }

    [Test]
    public void ResultActionsNameTheEchoOrCalibrationTarget()
    {
        Assert.AreEqual("挑战第 4 代回声",
            UIManager.GetSingleContractGameOverActionLabel(
                RunEndReason.FinishReached, true, true, 4, true));
        Assert.AreEqual("重试第 3 代回声",
            UIManager.GetSingleContractGameOverActionLabel(
                RunEndReason.FinishReached, true, false, 3, true));
        Assert.AreEqual("挑战第 1 代回声",
            UIManager.GetSingleContractGameOverActionLabel(
                RunEndReason.FinishReached, false, true, 1, true));
        Assert.AreEqual("继续校准",
            UIManager.GetSingleContractGameOverActionLabel(
                RunEndReason.Collision, false, false, 0, false));
    }

    [Test]
    public void SaveFailureNeverClaimsThatANewIdentityExists()
    {
        string challenge =
            AIShadowRunner.BuildSingleContractSaveFailureResult(
                RunEndReason.FinishReached, true, true, 3);
        string calibration =
            AIShadowRunner.BuildSingleContractSaveFailureResult(
                RunEndReason.FinishReached, false, false, 0);

        StringAssert.Contains("你跑赢了第3代回声", challenge);
        StringAssert.Contains("下一代未形成", challenge);
        StringAssert.Contains("当前回声保持不变", challenge);
        StringAssert.DoesNotContain("已经形成", challenge);
        StringAssert.Contains("校准结算保存失败", calibration);
        StringAssert.Contains("不会改写当前回声", calibration);
        StringAssert.DoesNotContain("校准完成", calibration);
    }

    [Test]
    public void FixedValidationResultNamesWinnerWithoutClaimingPersistence()
    {
        string won = AIShadowRunner.BuildSingleContractValidationResult(
            RunEndReason.FinishReached, true, true, 1);
        string lost = AIShadowRunner.BuildSingleContractValidationResult(
            RunEndReason.FinishReached, true, false, 1);
        string interrupted =
            AIShadowRunner.BuildSingleContractValidationResult(
                RunEndReason.Collision, true, false, 1);

        StringAssert.StartsWith("你跑赢了第1代固定回声", won);
        StringAssert.StartsWith("第1代固定回声胜出", lost);
        StringAssert.Contains("身份档未修改", won);
        StringAssert.Contains("身份档未修改", lost);
        StringAssert.Contains("身份档未修改", interrupted);
        StringAssert.DoesNotContain("已经形成", won);
        Assert.AreEqual("你跑赢了第1代固定回声",
            UIManager.GetSingleContractGameOverTitle(won,
                RunEndReason.FinishReached, true, true));
    }

    [TestCase(0, 0.61f, 0, 0.83f, 5,
        EchoCognitionChangeKind.Consolidated)]
    [TestCase(0, 0.83f, 0, 0.67f, 4,
        EchoCognitionChangeKind.Shaken)]
    [TestCase(0, 0.83f, 1, 0.50f, 3,
        EchoCognitionChangeKind.Shifted)]
    [TestCase(0, 0.83f, 1, 0.67f, 4,
        EchoCognitionChangeKind.Reversed)]
    [TestCase(0, 0.67f, 0, 0.70f, 4,
        EchoCognitionChangeKind.NoNewCognition)]
    public void CognitionAssessmentClassifiesVisibleRouteMemoryChange(
        int previousLane, float previousConfidence,
        int nextLane, float nextConfidence, int nextEvidence,
        EchoCognitionChangeKind expected)
    {
        EchoCognitionAssessment assessment = EchoCognitionAssessment.Compare(
            CognitionIdentity(3, previousLane, previousConfidence, 4),
            CognitionIdentity(4, nextLane, nextConfidence, nextEvidence),
            successfulCounterCount: 4, totalGateCount: 6,
            relearnStartGateNumber: 3,
            nextLaneHasUniqueEvidence: true);

        Assert.IsTrue(assessment.IsAvailable);
        Assert.AreEqual(expected, assessment.ChangeKind);
        Assert.AreEqual(3, assessment.PreviousGeneration);
        Assert.AreEqual(4, assessment.NextGeneration);
        Assert.AreEqual(4, assessment.SuccessfulCounterCount);
        Assert.AreEqual(6, assessment.TotalGateCount);
        Assert.AreEqual(3, assessment.RelearnStartGateNumber);
    }

    [Test]
    public void CognitionSummaryShowsOldBeliefRunEvidenceAndNewBelief()
    {
        EchoCognitionAssessment assessment = EchoCognitionAssessment.Compare(
            CognitionIdentity(3, 0, 0.83f, 5),
            CognitionIdentity(4, 1, 0.67f, 4),
            successfulCounterCount: 4, totalGateCount: 6,
            relearnStartGateNumber: 3,
            nextLaneHasUniqueEvidence: true);

        string summary = EchoRunPresentation
            .BuildSingleContractCognitionSummary(assessment);

        Assert.AreEqual(
            "上一代路线认知：偏向左侧 · 置信度 83%\n"
            + "本局发生：反制 4/6 · 第3门起追学\n"
            + "路线认知反转：第4代改判为中间 · 67%",
            summary);
    }

    [Test]
    public void MissingPromotionCannotClaimNewCognition()
    {
        EchoCognitionAssessment assessment = EchoCognitionAssessment.Compare(
            CognitionIdentity(3, 0, 0.83f, 5), null,
            successfulCounterCount: 4, totalGateCount: 6,
            relearnStartGateNumber: 3,
            nextLaneHasUniqueEvidence: true);

        Assert.IsFalse(assessment.IsAvailable);
        Assert.AreEqual(EchoCognitionChangeKind.Unavailable,
            assessment.ChangeKind);
        Assert.AreEqual("", EchoRunPresentation
            .BuildSingleContractCognitionSummary(assessment));
    }

    [Test]
    public void AmbiguousLaneTieCannotClaimCognitionShift()
    {
        EchoCognitionAssessment assessment = EchoCognitionAssessment.Compare(
            CognitionIdentity(3, 0, 0.80f, 4),
            CognitionIdentity(4, 1, 0.40f, 2),
            successfulCounterCount: 2, totalGateCount: 5,
            relearnStartGateNumber: 0,
            nextLaneHasUniqueEvidence: false);

        Assert.IsTrue(assessment.IsAvailable);
        Assert.AreEqual(EchoCognitionChangeKind.Shaken,
            assessment.ChangeKind);
        StringAssert.DoesNotContain("开始转向",
            EchoRunPresentation.BuildSingleContractCognitionSummary(
                assessment));
    }

    [Test]
    public void ImprecisePreviousMemoryCannotClaimCognitionChange()
    {
        EchoCognitionAssessment assessment = EchoCognitionAssessment.Compare(
            CognitionIdentity(3, 0, 0.50f, 3),
            CognitionIdentity(4, 1, 0.67f, 4),
            successfulCounterCount: 3, totalGateCount: 6,
            relearnStartGateNumber: 3,
            nextLaneHasUniqueEvidence: true);

        Assert.IsFalse(assessment.IsAvailable);
        Assert.AreEqual(EchoCognitionChangeKind.Unavailable,
            assessment.ChangeKind);
    }

    [Test]
    public void CognitionComparisonDoesNotMutateEitherIdentity()
    {
        ActiveEchoIdentity previous = CognitionIdentity(3, 0, 0.80f, 4);
        ActiveEchoIdentity next = CognitionIdentity(4, 2, 0.67f, 4);
        string previousJson = JsonUtility.ToJson(previous);
        string nextJson = JsonUtility.ToJson(next);

        EchoCognitionAssessment.Compare(previous, next,
            successfulCounterCount: 3, totalGateCount: 6,
            relearnStartGateNumber: 3,
            nextLaneHasUniqueEvidence: true);

        Assert.AreEqual(previousJson, JsonUtility.ToJson(previous));
        Assert.AreEqual(nextJson, JsonUtility.ToJson(next));
    }

    [Test]
    public void CognitionSummaryStatesWhenEchoDidNotRelearn()
    {
        EchoCognitionAssessment assessment = EchoCognitionAssessment.Compare(
            CognitionIdentity(3, 0, 0.80f, 4),
            CognitionIdentity(4, 0, 0.82f, 5),
            successfulCounterCount: 1, totalGateCount: 6,
            relearnStartGateNumber: 0,
            nextLaneHasUniqueEvidence: true);

        string summary = EchoRunPresentation
            .BuildSingleContractCognitionSummary(assessment);
        StringAssert.Contains("反制 1/6 · 回声未追学", summary);
        StringAssert.Contains("路线无新认知", summary);
    }

    [Test]
    public void WrongParentCannotClaimNewCognition()
    {
        ActiveEchoIdentity next = CognitionIdentity(4, 1, 0.67f, 4);
        next.parentIdentityId = "unrelated-identity";

        EchoCognitionAssessment assessment = EchoCognitionAssessment.Compare(
            CognitionIdentity(3, 0, 0.83f, 5), next,
            successfulCounterCount: 4, totalGateCount: 6,
            relearnStartGateNumber: 3,
            nextLaneHasUniqueEvidence: true);

        Assert.IsFalse(assessment.IsAvailable);
        Assert.AreEqual(EchoCognitionChangeKind.Unavailable,
            assessment.ChangeKind);
    }

    private static PredictionGateLane Lane(int physicalLane,
        PredictionGateRole role)
    {
        return new PredictionGateLane
        {
            physicalLane = physicalLane,
            role = role
        };
    }

    private static ActiveEchoIdentity CognitionIdentity(int generation,
        int preferredLane, float confidence, int evidenceCount)
    {
        return new ActiveEchoIdentity
        {
            generation = generation,
            identityId = "identity-" + generation,
            parentIdentityId = generation > 1
                ? "identity-" + (generation - 1)
                : "",
            memoryContract = new EchoMemoryContract
            {
                preferredLane = preferredLane,
                confidence = confidence,
                evidenceCount = evidenceCount
            }
        };
    }
}
