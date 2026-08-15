using NUnit.Framework;

public class CoinArcTests
{
    [Test]
    public void JumpRewardCoinsFollowThePlayerJumpHeight()
    {
        const float groundCoinHeight = 1f;
        const float jumpHeight = 3f;

        Assert.AreEqual(groundCoinHeight,
            TrackSpawnRules.JumpCoinHeight(
                0f, groundCoinHeight, jumpHeight), 0.0001f);
        Assert.AreEqual(groundCoinHeight + jumpHeight,
            TrackSpawnRules.JumpCoinHeight(
                0.5f, groundCoinHeight, jumpHeight), 0.0001f);
        Assert.AreEqual(groundCoinHeight,
            TrackSpawnRules.JumpCoinHeight(
                1f, groundCoinHeight, jumpHeight), 0.0001f);
    }

    [Test]
    public void CoinTrailOverlapChecksTheWholeTrailInsteadOfOnlyItsStart()
    {
        Assert.IsTrue(TrackSpawnRules.CoinTrailOverlapsObstacle(
            6f, 7, 1.8f, 11f, 0.35f),
            "An obstacle inside the later half of a coin trail must be detected.");
        Assert.IsFalse(TrackSpawnRules.CoinTrailOverlapsObstacle(
            6f, 7, 1.8f, 19f, 0.35f),
            "A distant obstacle must not convert an unrelated coin trail.");
    }

    [Test]
    public void JumpRewardArcFitsCompletelyInsideTheStraightSegment()
    {
        float center = TrackSpawnRules.ClampJumpRewardCenter(
            2f, 20f, TrackSpawnRules.JumpRewardCoinCount,
            TrackSpawnRules.CoinSpacing,
            TrackSpawnRules.CoinSegmentMargin);
        float halfSpan = (TrackSpawnRules.JumpRewardCoinCount - 1)
                         * TrackSpawnRules.CoinSpacing * 0.5f;

        Assert.GreaterOrEqual(center - halfSpan,
            TrackSpawnRules.CoinSegmentMargin);
        Assert.LessOrEqual(center + halfSpan,
            20f - TrackSpawnRules.CoinSegmentMargin);
        Assert.AreEqual(6.4f, center, 0.0001f);
    }
}
