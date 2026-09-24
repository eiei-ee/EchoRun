using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Serialized presentation only. Cloud and run state belong to AsyncEchoPanel.</summary>
public sealed class AsyncEchoPanelView : MonoBehaviour
{
    public RectTransform frame, invitationSummary;
    public GameObject challengePage, boardPage, publishCard, publishDetails, reportCard, emptyBoard, actionDock;
    public Text heading, invitationStatus, invitationTitle, invitationDescription, generation, publishStatus;
    public Text reportStatus, boardMessage, boardDetail;
    public Button close, challengeTab, boardTab, start, offline, publish, share, publishToggle, recent, retry, reload;
    public Image challengeTabFill, boardTabFill, invitationRule;
    public ScrollRect challengeScroll, boardScroll;
    public RectTransform rows;
    public AsyncEchoLeaderboardRow rowTemplate;
    private readonly List<AsyncEchoLeaderboardRow> _rows = new List<AsyncEchoLeaderboardRow>();
    private bool _showBoard, _publishingExpanded, _fitting;

    // One project palette; the platform UI does not invent another theme.
    public static Color Backdrop => EchoRunUITheme.Backdrop;
    public static Color Surface => EchoRunUITheme.Surface;
    public static Color Raised => EchoRunUITheme.SurfaceRaised;
    public static Color Selected => EchoRunUITheme.SurfaceSelected;
    public static Color Primary => EchoRunUITheme.ActionAccent;
    public static Color Foreground => EchoRunUITheme.TextPrimary;
    public static Color Muted => EchoRunUITheme.TextMuted;
    public static Color Echo => EchoRunUITheme.Echo;
    public static Color Danger => EchoRunUITheme.Danger;
    public static Color Ink => EchoRunUITheme.Ink;

    public void ShowBoard(bool board)
    {
        bool changed = _showBoard != board;
        _showBoard = board;
        challengePage.SetActive(!board);
        boardPage.SetActive(board);
        challengeTabFill.color = board ? Color.clear : Selected;
        boardTabFill.color = board ? Selected : Color.clear;
        challengeTab.GetComponentInChildren<Text>().color = board ? Muted : Foreground;
        boardTab.GetComponentInChildren<Text>().color = board ? Foreground : Muted;
        if (changed)
        {
            ScrollRect scroll = board ? boardScroll : challengeScroll;
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = 1f;
        }
        FitFrame();
    }

    public void TogglePublishing()
    {
        _publishingExpanded = !_publishingExpanded;
        publishDetails.SetActive(_publishingExpanded);
        publishToggle.GetComponentInChildren<Text>().text = _publishingExpanded ? "我的影子 · 收起" : "我的影子 · 展开";
        if (!_publishingExpanded && EventSystem.current != null)
        {
            GameObject selected = EventSystem.current.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(publishDetails.transform))
                EventSystem.current.SetSelectedGameObject(publishToggle.gameObject);
        }
        FitFrame();
    }

    public void ArrangeActions(bool hasChallenge)
    {
        var primary = (RectTransform)start.transform;
        var solo = (RectTransform)offline.transform;
        primary.anchorMin = Vector2.zero;
        primary.anchorMax = new Vector2(.64f, 1);
        primary.offsetMin = Vector2.zero;
        primary.offsetMax = new Vector2(-12, 0);
        solo.anchorMin = new Vector2(hasChallenge ? .64f : 0, 0);
        solo.anchorMax = Vector2.one;
        solo.offsetMin = new Vector2(hasChallenge ? 12 : 0, 0);
        solo.offsetMax = Vector2.zero;
        offline.GetComponent<Image>().color = hasChallenge ? Raised : Primary;
        offline.GetComponentInChildren<Text>().color = hasChallenge ? Foreground : Ink;
    }

    public void SetInvitationTone(bool ready, bool loading, bool failed)
    {
        Color tone = failed ? Danger : ready ? Primary : Echo;
        invitationRule.color = tone;
        invitationStatus.color = tone;
        generation.color = ready ? Foreground : Muted;
        invitationDescription.color = failed ? Danger : Muted;
    }

    public void SetLeaderboard(AsyncEchoOperationState state, AsyncEchoLeaderboardEntry[] items, bool hasBoard)
    {
        bool hasRows = hasBoard && state == AsyncEchoOperationState.Succeeded && items != null && items.Length > 0;
        int count = hasRows ? Mathf.Min(items.Length, 50) : 0;
        emptyBoard.SetActive(count == 0);
        for (int i = 0; i < count; i++)
        {
            if (i == _rows.Count) _rows.Add(Instantiate(rowTemplate, rows));
            _rows[i].gameObject.SetActive(true);
            _rows[i].Bind(items[i]);
        }
        for (int i = count; i < _rows.Count; i++) _rows[i].gameObject.SetActive(false);
        bool failed = hasBoard && state != AsyncEchoOperationState.Idle
            && state != AsyncEchoOperationState.Pending && state != AsyncEchoOperationState.Succeeded;
        boardMessage.color = failed ? Danger : Foreground;
        if (!hasBoard)
        {
            boardMessage.text = "还没有可查看的挑战榜";
            boardDetail.text = "打开好友邀请，或发布自己的影子。\n每份影子都有独立的挑战榜。";
        }
        else if (state == AsyncEchoOperationState.Idle)
        {
            boardMessage.text = "这份影子的挑战榜";
            boardDetail.text = "刷新后查看最佳距离。";
        }
        else if (state == AsyncEchoOperationState.Pending)
        {
            boardMessage.text = "正在读取成绩";
            boardDetail.text = "稍等片刻，成绩正在返回。";
        }
        else if (state == AsyncEchoOperationState.Succeeded)
        {
            boardMessage.text = "还没有挑战成绩";
            boardDetail.text = "完成第一场挑战，留下你的纪录。";
        }
        else
        {
            boardMessage.text = "暂时无法读取成绩";
            boardDetail.text = "网络暂时不可用。\n可以刷新重试，也可以继续单机。";
        }
        reload.interactable = hasBoard && state != AsyncEchoOperationState.Pending;
    }

    private void OnEnable()
    {
        EchoRunAccessibility.ApplyToHierarchy(transform);
        FitFrame();
    }

    private void OnRectTransformDimensionsChange() { FitFrame(); }

    private void FitFrame()
    {
        if (_fitting || frame == null || invitationSummary == null || challengeScroll == null || actionDock == null) return;
        _fitting = true;
        try
        {
            Rect available = ((RectTransform)transform).rect;
            bool large = Application.isPlaying && EchoRunAccessibility.LargeText;
            float desiredHeight = (large ? 1400f : 1320f) + (!_showBoard && _publishingExpanded ? 180f : 0f);
            frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Min(1160f, Mathf.Max(0f, available.width - 64f)));
            frame.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Min(desiredHeight, Mathf.Max(0f, available.height - 48f)));
            float summaryHeight = large ? 386f : 330f;
            float actionHeight = large ? 160f : 144f;
            Top(invitationSummary, 0, summaryHeight);
            Top((RectTransform)actionDock.transform, summaryHeight + 16f, actionHeight);
            RectTransform scroll = (RectTransform)challengeScroll.transform;
            scroll.offsetMax = new Vector2(0, -summaryHeight - actionHeight - 36f);
        }
        finally { _fitting = false; }
    }

    private static void Top(RectTransform rect, float top, float height)
    {
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, 1);
        rect.offsetMin = new Vector2(0, -top - height);
        rect.offsetMax = new Vector2(0, -top);
    }
}
