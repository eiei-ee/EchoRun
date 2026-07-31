using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class PowerUpShopUI : MonoBehaviour
{
    private GameManager _gameManager;
    private GameObject _launcher;
    private GameObject _panel;
    private Text _walletText;
    private Text _feedbackText;
    private readonly Text[] _itemState = new Text[4];
    private readonly Button[] _buyButtons = new Button[4];
    private readonly Button[] _equipButtons = new Button[4];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (FindObjectOfType<PowerUpShopUI>() != null) return;
        new GameObject("Power Up Shop UI").AddComponent<PowerUpShopUI>();
    }

    IEnumerator Start()
    {
        _gameManager = GameManager.Instance;
        Canvas canvas = null;
        for (int i = 0; i < 60 && canvas == null; i++)
        {
            canvas = FindObjectOfType<Canvas>();
            if (canvas == null) yield return null;
        }
        if (canvas == null || _gameManager == null) yield break;

        Transform parent = canvas.transform.Find("SafeArea") ?? canvas.transform;
        Build(parent);
        _gameManager.OnStateChanged.AddListener(OnStateChanged);
        _gameManager.OnBankedCoinsChanged.AddListener(OnWalletChanged);
        OnStateChanged(_gameManager.State);
    }

    private void Build(Transform parent)
    {
        Button launcherButton = RuntimePanelFactory.Button("PowerUpLauncher", parent,
            "补给仓", new Vector2(0.10f, 0.09f), new Vector2(220f, 68f),
            RuntimePanelFactory.Raised, 26);
        launcherButton.onClick.AddListener(Open);
        _launcher = launcherButton.gameObject;

        _panel = RuntimePanelFactory.PanelObject("PowerUpShop", parent,
            new Vector2(0.5f, 0.5f), new Vector2(1120f, 720f),
            RuntimePanelFactory.Panel);

        Text title = RuntimePanelFactory.Text("Title", _panel.transform,
            "补给仓 · 下局生效", 38, TextAnchor.MiddleLeft,
            RuntimePanelFactory.TextPrimary);
        title.rectTransform.pivot = new Vector2(0f, 0.5f);
        RuntimePanelFactory.Place(title.rectTransform, new Vector2(0.08f, 0.90f),
            new Vector2(540f, 70f), Vector2.zero);
        _walletText = RuntimePanelFactory.Text("Wallet", _panel.transform, "",
            30, TextAnchor.MiddleRight, RuntimePanelFactory.Reward);
        RuntimePanelFactory.Place(_walletText.rectTransform, new Vector2(0.82f, 0.90f),
            new Vector2(320f, 60f), Vector2.zero);

        for (int i = 0; i < 4; i++) BuildItemRow(i);

        _feedbackText = RuntimePanelFactory.Text("Feedback", _panel.transform, "",
            22, TextAnchor.MiddleLeft, RuntimePanelFactory.TextMuted);
        _feedbackText.rectTransform.pivot = new Vector2(0f, 0.5f);
        RuntimePanelFactory.Place(_feedbackText.rectTransform, new Vector2(0.10f, 0.08f),
            new Vector2(700f, 50f), Vector2.zero);
        Button close = RuntimePanelFactory.Button("Close", _panel.transform, "返回",
            new Vector2(0.88f, 0.08f), new Vector2(190f, 58f),
            RuntimePanelFactory.Raised, 24);
        close.onClick.AddListener(() => _panel.SetActive(false));
        _panel.SetActive(false);
        Refresh();
    }

    private void BuildItemRow(int index)
    {
        PowerUpId id = (PowerUpId)index;
        PowerUpBalance definition = GameBalanceConfig.GetPowerUp(id);
        float y = 0.75f - index * 0.17f;
        GameObject row = RuntimePanelFactory.PanelObject("Item_" + id, _panel.transform,
            new Vector2(0.5f, y), new Vector2(1000f, 100f),
            new Color(0.09f, 0.13f, 0.18f, 0.96f));

        Text name = RuntimePanelFactory.Text("Name", row.transform,
            definition.displayName, 28, TextAnchor.MiddleLeft,
            RuntimePanelFactory.TextPrimary);
        name.rectTransform.pivot = new Vector2(0f, 0.5f);
        RuntimePanelFactory.Place(name.rectTransform, new Vector2(0.03f, 0.63f),
            new Vector2(260f, 44f), Vector2.zero);
        Text description = RuntimePanelFactory.Text("Description", row.transform,
            definition.description, 20, TextAnchor.MiddleLeft,
            RuntimePanelFactory.TextMuted);
        description.rectTransform.pivot = new Vector2(0f, 0.5f);
        RuntimePanelFactory.Place(description.rectTransform, new Vector2(0.03f, 0.25f),
            new Vector2(390f, 38f), Vector2.zero);

        _itemState[index] = RuntimePanelFactory.Text("State", row.transform, "",
            21, TextAnchor.MiddleCenter, RuntimePanelFactory.Reward);
        RuntimePanelFactory.Place(_itemState[index].rectTransform, new Vector2(0.54f, 0.5f),
            new Vector2(220f, 52f), Vector2.zero);

        _buyButtons[index] = RuntimePanelFactory.Button("Buy", row.transform,
            "购买 " + definition.cost, new Vector2(0.75f, 0.5f),
            new Vector2(180f, 58f), RuntimePanelFactory.Action, 21);
        _buyButtons[index].onClick.AddListener(() => Purchase(id));
        _equipButtons[index] = RuntimePanelFactory.Button("Equip", row.transform,
            "装备", new Vector2(0.93f, 0.5f), new Vector2(130f, 58f),
            RuntimePanelFactory.Raised, 21);
        _equipButtons[index].onClick.AddListener(() => Equip(id));
    }

    private void Open()
    {
        Refresh();
        _panel.SetActive(true);
    }

    private void Purchase(PowerUpId id)
    {
        bool success = _gameManager.TryPurchasePowerUp(id);
        _feedbackText.text = success ? "购买完成，可以装备到下一局。" : "金币不足，完成跑酷可继续积累。";
        _feedbackText.color = success
            ? RuntimePanelFactory.TextPrimary
            : new Color(0.94f, 0.45f, 0.38f);
        Refresh();
    }

    private void Equip(PowerUpId id)
    {
        bool success = _gameManager.SelectPowerUp(id);
        _feedbackText.text = success ? "已装备，下局开场自动消耗。" : "库存为空，请先购买。";
        Refresh();
    }

    private void Refresh()
    {
        if (_walletText == null) return;
        _walletText.text = "可用金币  " + EchoRunSaveSystem.TotalCoins;
        PowerUpId selected = EchoRunSaveSystem.GetSelectedPowerUp();
        for (int i = 0; i < 4; i++)
        {
            PowerUpId id = (PowerUpId)i;
            int count = EchoRunSaveSystem.GetPowerUpCount(id);
            _itemState[i].text = "库存 " + count + (selected == id ? "  ·  已装备" : "");
            _equipButtons[i].interactable = count > 0;
        }
    }

    private void OnWalletChanged(int value) => Refresh();

    private void OnStateChanged(GameState state)
    {
        bool menu = state == GameState.Menu;
        if (_launcher != null) _launcher.SetActive(menu);
        if (!menu && _panel != null) _panel.SetActive(false);
        if (menu) Refresh();
    }

    void OnDestroy()
    {
        if (_gameManager == null) return;
        _gameManager.OnStateChanged.RemoveListener(OnStateChanged);
        _gameManager.OnBankedCoinsChanged.RemoveListener(OnWalletChanged);
    }
}
