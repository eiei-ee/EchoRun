using System;
using UnityEngine;

[Serializable]
public sealed class AITrainingReport
{
    public int generationBefore;
    public int generationAfter;
    public int directorUpdatesBefore;
    public int directorUpdatesAfter;
    public float skillBefore;
    public float skillAfter;
    public float shadowWeightDelta;
    public float directorWeightDelta;
    public int[] actionSamples = new int[5];
    public string learnedAction;
    public string summary;
}

public static class AITrainingReportBuilder
{
    private static readonly string[] ActionNames =
    {
        "保持路线", "向左变道", "向右变道", "跳跃", "滑铲"
    };

    public static AITrainingReport FromTelemetry(AIRunTelemetryData data)
    {
        if (!AIRunTelemetry.IsCompletedTrainingRun(data)) return null;
        AITrainingReport report = new AITrainingReport
        {
            generationBefore = data.shadowGenerationAtStart,
            generationAfter = data.shadowGenerationAtEnd,
            directorUpdatesBefore = data.directorUpdatesAtStart,
            directorUpdatesAfter = data.directorUpdatesAtEnd,
            skillBefore = data.playerSkillAtStart,
            skillAfter = data.playerSkillAtEnd,
            shadowWeightDelta = MeanAbsoluteDelta(
                data.shadowWeightsAtStart, data.shadowWeightsAtEnd),
            directorWeightDelta = MeanAbsoluteDelta(
                data.directorWeightsAtStart, data.directorWeightsAtEnd)
        };

        if (data.shadowSamples != null)
        {
            foreach (AIShadowTrainingSample sample in data.shadowSamples)
            {
                if (sample.opponentDecision) continue;
                int action = Mathf.Clamp(sample.action, 0, report.actionSamples.Length - 1);
                report.actionSamples[action]++;
            }
        }

        int activeSamples = ActiveSampleCount(report.actionSamples);
        int learnedIndex = DominantAction(report.actionSamples);
        report.learnedAction = learnedIndex >= 0
            ? ActionNames[learnedIndex]
            : activeSamples > 0 ? "样本不足" : "暂无有效动作";
        int updateGain = Mathf.Max(0,
            report.directorUpdatesAfter - report.directorUpdatesBefore);
        float skillDelta = report.skillAfter - report.skillBefore;
        string skillTrend = skillDelta > 0.01f ? "上调难度判断"
            : (skillDelta < -0.01f ? "加强恢复节奏" : "保持当前节奏");
        report.summary = learnedIndex >= 0
            ? "本代重点学习了“" + report.learnedAction + "”，"
              + "导演新增 " + updateGain + " 次反馈更新，并倾向"
              + skillTrend + "。"
            : activeSamples > 0
                ? "本代采集到 " + activeSamples
                  + " 个有效动作，但尚未达到确定画像阈值。"
                : "本代没有采集到新的玩家动作样本，导演新增 " + updateGain
                  + " 次反馈更新，并倾向" + skillTrend + "。";
        return report;
    }

    public static float MeanAbsoluteDelta(float[] before, float[] after)
    {
        if (before == null || after == null || before.Length == 0
            || before.Length != after.Length)
            return 0f;
        float total = 0f;
        for (int i = 0; i < before.Length; i++)
            total += Mathf.Abs(after[i] - before[i]);
        return total / before.Length;
    }

    private static int DominantAction(int[] counts)
    {
        if (counts == null || counts.Length < 5) return -1;
        int best = 1;
        int second = 0;
        for (int i = 2; i < counts.Length; i++)
        {
            if (counts[i] > counts[best])
            {
                second = counts[best];
                best = i;
            }
            else second = Mathf.Max(second, counts[i]);
        }
        return counts[best] >= 3 && counts[best] - second >= 2
            ? best : -1;
    }

    private static int ActiveSampleCount(int[] counts)
    {
        if (counts == null) return 0;
        int total = 0;
        for (int i = 1; i < counts.Length; i++)
            total += Mathf.Max(0, counts[i]);
        return total;
    }
}
