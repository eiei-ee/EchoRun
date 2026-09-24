using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class AsyncEchoPlatformTests
{
    private sealed class FakeTransport : IAsyncEchoTransport
    {
        public bool IsAvailable { get; set; } = true;
        public string UnavailableReason => "ASYNC_UNAVAILABLE";
        public readonly List<Dictionary<string, object>> Requests = new List<Dictionary<string, object>>();
        public readonly List<Action<AsyncEchoTransportResult>> Callbacks = new List<Action<AsyncEchoTransportResult>>();
        public void Send(Dictionary<string, object> request, Action<AsyncEchoTransportResult> completed)
        { Requests.Add(request); Callbacks.Add(completed); }
        public void Reply(int index, AsyncEchoCloudData data)
        { Callbacks[index](new AsyncEchoTransportResult(JsonUtility.ToJson(new AsyncEchoCloudReply
            { ok = true, apiVersion = 1, data = data }))); }
        public void Fail(int index, string code, bool retryable)
        { Callbacks[index](new AsyncEchoTransportResult(null, code, retryable)); }
    }

    private FakeTransport _transport;
    private AsyncEchoCloud _cloud;
    private double _now;

    [SetUp]
    public void SetUp()
    { _now = 0; _transport = new FakeTransport(); _cloud = new AsyncEchoCloud(_transport, () => _now); }
    [TearDown]
    public void TearDown() { _cloud.Dispose(); }

    [Test]
    public void InvitationRoundTripsStringAndSdkDictionary()
    {
        var invitation = new AsyncEchoInvitation("o_owner", "echo-abc", 1);
        Assert.IsTrue(AsyncEchoInvitation.TryParse(invitation.ToQuery(), out var parsed, out var error), error);
        Assert.AreEqual(invitation.Key, parsed.Key);
        var query = new Dictionary<string, string>
            { { "inviter", "o_owner" }, { "shadow", "echo-abc" }, { "rules", "1" } };
        Assert.IsTrue(AsyncEchoInvitation.TryParse(query, out var sdk, out error), error);
        Assert.AreEqual(parsed.Key, sdk.Key);
        Assert.IsTrue(AsyncEchoInvitation.TryParse("scene=launch", out parsed, out error));
        Assert.IsNull(parsed);
    }

    [TestCase("inviter=a&shadow=b&rules=1&shadow=c")]
    [TestCase("inviter=a&shadow=b")]
    [TestCase("inviter=a&shadow=b&rules=2")]
    [TestCase("inviter=%GG&shadow=b&rules=1")]
    [TestCase("inviter=a&shadow=b%&rules=1")]
    [TestCase("inviter=a&shadow=%2Fetc&rules=1")]
    [TestCase("inviter=&shadow=b&rules=1")]
    public void InvalidQueryIsRejected(string query)
    { Assert.IsFalse(AsyncEchoInvitation.TryParse(query, out _, out _)); }

    [Test]
    public void QueryUsesByteLimitAndDoesNotDoubleDecodeSdkValues()
    {
        Assert.IsFalse(AsyncEchoInvitation.TryParse("inviter=a&shadow=b&rules=1&x=" + new string('影', 400), out _, out _));
        var query = new Dictionary<string, string>
            { { "inviter", "a" }, { "shadow", "echo%2Dabc" }, { "rules", "1" } };
        Assert.IsFalse(AsyncEchoInvitation.TryParse(query, out _, out _));
    }

    [Test]
    public void UnavailableDoesNotSendOrBlock()
    {
        _transport.IsAvailable = false;
        _cloud.SetMenuAvailable(true);
        _cloud.ReceiveInvitation(new AsyncEchoInvitation("owner", "echo-x", 1));
        Assert.AreEqual(AsyncEchoOperationState.Unavailable, _cloud.InvitationState);
        Assert.AreEqual(0, _transport.Requests.Count);
        Assert.AreEqual(0, _cloud.InFlightCount);
    }

    [Test]
    public void FiveSecondAttemptHasOneRetryAndTenSecondTotalBudget()
    {
        _cloud.LoadLeaderboard("board-a");
        _now = 4.99; _cloud.Tick(); Assert.AreEqual(1, _transport.Requests.Count);
        _now = 5; _cloud.Tick(); Assert.AreEqual(2, _transport.Requests.Count);
        _now = 10; _cloud.Tick();
        Assert.AreEqual(AsyncEchoOperationState.Failed, _cloud.LeaderboardState);
        Assert.AreEqual(0, _cloud.InFlightCount);
        _now = 100; _cloud.Tick(); Assert.AreEqual(2, _transport.Requests.Count);
    }

    [Test]
    public void ResumeAfterWholeBudgetDoesNotStartAnotherAttempt()
    {
        _cloud.LoadLeaderboard("board-a");
        _transport.Reply(0, BoardReply());
        _now = 20; _cloud.Tick();
        Assert.AreEqual(1, _transport.Requests.Count);
        Assert.AreEqual(AsyncEchoOperationState.Failed, _cloud.LeaderboardState);
        Assert.AreEqual("TIMEOUT", _cloud.LeaderboardError);
    }

    [Test]
    public void PauseDoesNotAffectInjectedRealTimeDeadline()
    {
        float previousScale = Time.timeScale;
        try
        {
            Time.timeScale = 0;
            _cloud.LoadLeaderboard("board-a");
            _now = 5; _cloud.Tick(); Assert.AreEqual(2, _transport.Requests.Count);
        }
        finally { Time.timeScale = previousScale; }
    }

    [Test]
    public void OldAttemptAndDuplicateCallbacksCannotCompleteNewAttempt()
    {
        _cloud.LoadLeaderboard("board-a");
        _now = 5; _cloud.Tick();
        _transport.Reply(0, BoardReply()); _cloud.Tick();
        Assert.AreEqual(AsyncEchoOperationState.Pending, _cloud.LeaderboardState);
        _transport.Reply(1, BoardReply()); _transport.Fail(1, "NETWORK_ERROR", true); _cloud.Tick();
        Assert.AreEqual(AsyncEchoOperationState.Succeeded, _cloud.LeaderboardState);
        Assert.AreEqual(2, _transport.Requests.Count);
    }

    [Test]
    public void PermanentFailureDoesNotRetryAndEmptyBoardIsNotAnError()
    {
        _cloud.LoadLeaderboard("board-a");
        _transport.Fail(0, "RULES_UNSUPPORTED", false); _cloud.Tick();
        Assert.AreEqual(1, _transport.Requests.Count);
        Assert.AreEqual(AsyncEchoOperationState.Failed, _cloud.LeaderboardState);
        _cloud.LoadLeaderboard("board-a");
        _transport.Reply(1, BoardReply()); _cloud.Tick();
        Assert.AreEqual(AsyncEchoOperationState.Succeeded, _cloud.LeaderboardState);
        Assert.IsEmpty(_cloud.Leaderboard);
        StringAssert.Contains("还没有", AsyncEchoPanel.BuildLeaderboardText(_cloud.LeaderboardState, _cloud.Leaderboard));
        StringAssert.Contains("无法", AsyncEchoPanel.BuildLeaderboardText(AsyncEchoOperationState.Failed, new AsyncEchoLeaderboardEntry[0]));
    }

    [Test]
    public void NewInvitationInvalidatesOldResponseAndOfflineInvalidatesGet()
    {
        ActiveEchoIdentity identity = Identity();
        _cloud.SetMenuAvailable(true);
        _cloud.ReceiveInvitation(new AsyncEchoInvitation("owner-a", identity.identityId, 1));
        _cloud.ReceiveInvitation(new AsyncEchoInvitation("owner-b", identity.identityId, 1));
        _transport.Reply(0, GetReply(identity, "owner-a")); _cloud.Tick();
        Assert.IsNull(_cloud.CurrentSnapshot);
        _transport.Reply(1, GetReply(identity, "owner-b")); _cloud.Tick();
        Assert.AreEqual("owner-b", _cloud.CurrentSnapshot.Invitation.InviterOpenid);
        _cloud.DismissInvitation(); _cloud.OpenRecentInvitation();
        _cloud.DismissInvitation();
        _transport.Reply(2, GetReply(identity, "owner-b")); _cloud.Tick();
        Assert.IsNull(_cloud.CurrentSnapshot);
        Assert.IsFalse(_cloud.InvitationVisible);
    }

    [Test]
    public void ColdAndWarmDuplicateKeepsRecentInvitationUsable()
    {
        var invitation = new AsyncEchoInvitation("owner", "echo-x", 1);
        _cloud.SetMenuAvailable(true);
        _cloud.ReceiveInvitation(invitation); _cloud.ReceiveInvitation(invitation);
        Assert.AreEqual(1, _transport.Requests.Count);
        _cloud.DismissInvitation(); _cloud.ReceiveInvitation(invitation);
        Assert.IsFalse(_cloud.InvitationVisible);
        Assert.IsNotNull(_cloud.RecentInvitation);
        _cloud.OpenRecentInvitation();
        Assert.AreEqual(2, _transport.Requests.Count);
        Assert.IsTrue(_cloud.InvitationVisible);
    }

    [Test]
    public void BusyGameQueuesOnlyLatestInvitationUntilMenu()
    {
        _cloud.ReceiveInvitation(new AsyncEchoInvitation("a", "echo-x", 1));
        _cloud.ReceiveInvitation(new AsyncEchoInvitation("b", "echo-x", 1));
        Assert.IsEmpty(_transport.Requests);
        _cloud.SetMenuAvailable(true);
        Assert.AreEqual("b", _transport.Requests[0]["inviterOpenid"]);
    }

    [Test]
    public void ResumeAndSceneRestartPreserveInvitationQueuedWhileRunning()
    {
        _cloud.SetMenuAvailable(true);
        _cloud.ReceiveInvitation(new AsyncEchoInvitation("original", "echo-x", 1));
        _cloud.RunStarted(); // Invalidates the visible invitation's unfinished get.
        _cloud.ReceiveInvitation(new AsyncEchoInvitation("new-friend", "echo-y", 1));
        _cloud.SetMenuAvailable(false); // Pause/resume never exposes a menu.
        Assert.AreEqual(1, _transport.Requests.Count);
        _cloud.RunStarted(); // Restart can begin a second run without consuming new-friend.
        _cloud.SetMenuAvailable(false);
        Assert.AreEqual(1, _transport.Requests.Count);
        Assert.AreEqual("new-friend", _cloud.RecentInvitation.InviterOpenid);
        _cloud.SetMenuAvailable(true); // Only a stable, actual menu opens the latest invitation.
        Assert.AreEqual(2, _transport.Requests.Count);
        Assert.AreEqual("new-friend", _transport.Requests[1]["inviterOpenid"]);
        Assert.IsTrue(_cloud.InvitationVisible);
    }

    [Test]
    public void UnknownGetMetadataVersionIsRejected()
    {
        var identity = Identity();
        _cloud.SetMenuAvailable(true);
        _cloud.ReceiveInvitation(new AsyncEchoInvitation("owner", identity.identityId, 1));
        var reply = GetReply(identity, "owner"); reply.payloadVersion = 99;
        _transport.Reply(0, reply); _cloud.Tick();
        Assert.IsNull(_cloud.CurrentSnapshot);
        Assert.AreEqual(AsyncEchoOperationState.Failed, _cloud.InvitationState);
    }

    [Test]
    public void PublishValidatesWithoutMutatingSourceAndInvalidatesOlderResponse()
    {
        var identity = Identity();
        string before = JsonUtility.ToJson(identity);
        _cloud.Publish(identity);
        Assert.AreEqual(before, JsonUtility.ToJson(identity));
        Assert.AreEqual(1, _transport.Requests.Count);
        identity.version = 999;
        string invalid = JsonUtility.ToJson(identity);
        _cloud.Publish(identity);
        Assert.AreEqual(invalid, JsonUtility.ToJson(identity));
        Assert.AreEqual(1, _transport.Requests.Count);
        _transport.Reply(0, new AsyncEchoCloudData { boardId = "board-a", inviterOpenid = "owner",
            identityId = identity.identityId, generation = 1, rulesVersion = 1, runSeed = 123 });
        _cloud.Tick(); Assert.IsNull(_cloud.Published);
        Assert.AreEqual(AsyncEchoOperationState.Failed, _cloud.PublishState);
    }

    [Test]
    public void ReportRetryKeepsFrozenBusinessIdAndNeverNeedsLocalIdentity()
    {
        var identity = Identity();
        var snapshot = new AsyncEchoSnapshot("board-a", new AsyncEchoInvitation("owner", identity.identityId, 1), 123, identity);
        var result = new AsyncChallengeResult(new AsyncChallengeRunParameters("challenge-one", 1, 123),
            identity.identityId, 100, -2, RunEndReason.Collision, false);
        _cloud.Report(result, snapshot);
        _now = 5; _cloud.Tick(); _now = 10; _cloud.Tick();
        Assert.AreEqual(AsyncEchoOperationState.Unconfirmed, _cloud.ReportState);
        _cloud.RetryReport();
        Assert.AreEqual(3, _transport.Requests.Count);
        foreach (var request in _transport.Requests)
        {
            Assert.AreEqual("challenge-one", request["challengeId"]);
            Assert.AreEqual("board-a", request["boardId"]);
            Assert.AreEqual(100f, request["distanceMeters"]);
        }
        _transport.Reply(2, new AsyncEchoCloudData { receiptId = "receipt-one", acceptedAt = 1234, duplicate = true, eligible = true });
        _cloud.Tick(); Assert.AreEqual(AsyncEchoOperationState.Succeeded, _cloud.ReportState);
    }

    [Test]
    public void MalformedLeaderboardIsRejectedWithoutShowingRemoteMarkup()
    {
        _cloud.LoadLeaderboard("board-a");
        var reply = BoardReply();
        reply.items = new[] { new AsyncEchoLeaderboardEntry { rank = 4, entryId = "entry-a", displayLabel = "Runner", distanceMeters = 3 } };
        _transport.Reply(0, reply); _cloud.Tick();
        Assert.AreEqual(AsyncEchoOperationState.Failed, _cloud.LeaderboardState);
    }

    [Test]
    public void SafeAreaConvertsCoordinatesAndFallbackHasNoPlatformDependency()
    {
        var fallback = new Rect(0, 0, 600, 1200);
        Assert.AreEqual(fallback, new WeixinSafeAreaProvider().Resolve(fallback, 600, 1200));
        Rect result = WeChatSafeArea.Convert(Rect.MinMaxRect(0, 20, 300, 580), 40, 300, 600, 600, 1200, fallback);
        Assert.AreEqual(40f, result.yMin); Assert.AreEqual(1104f, result.yMax);
        Assert.AreEqual(fallback, WeChatSafeArea.Convert(new Rect(), 0, 0, 0, 600, 1200, fallback));
    }

    private static AsyncEchoCloudData BoardReply() => new AsyncEchoCloudData
        { boardId = "board-a", rulesVersion = 1, items = new AsyncEchoLeaderboardEntry[0] };
    private static AsyncEchoCloudData GetReply(ActiveEchoIdentity identity, string owner) => new AsyncEchoCloudData
    {
        found = true, boardId = "board-a", ownerOpenid = owner, identityId = identity.identityId,
        payloadJson = identity.ToJson(), payloadVersion = ActiveEchoIdentity.CurrentVersion,
        generation = identity.generation, rulesVersion = 1, runSeed = 123
    };
    private static ActiveEchoIdentity Identity()
    {
        var sequence = new AIShadowSequencePolicy().ExportState();
        var identity = new ActiveEchoIdentity
        {
            generation = 1, sourceRunSequence = 1, policyWeights = new AIShadowPolicy().ExportWeights(),
            sequenceTransitions = sequence.transitions, sequencePairCount = sequence.pairCount,
            style = EchoIdentityStyleSnapshot.FromPlayerStyle(new PlayerStyleData()),
            pace = 13f, sourceCourseDuration = 95f, clarity = 1f,
            memoryContract = new EchoMemoryContract { contractId = "route-test", preferredLane = 1, confidence = 1f, evidenceCount = 5 }
        };
        identity.identityId = ActiveEchoIdentity.CreateIdentityId(identity);
        identity.memoryContract.identityId = identity.identityId;
        Assert.IsTrue(ActiveEchoIdentity.TryFromExternalJson(identity.ToJson(), out _, out var error), error);
        return identity;
    }
}
