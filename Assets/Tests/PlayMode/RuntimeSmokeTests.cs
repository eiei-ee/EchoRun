using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class RuntimeSmokeTests
{
    [UnityTest]
    public IEnumerator BundledAudioAndBalanceLoadInPlayer()
    {
        yield return null;

        Assert.IsNotNull(Resources.Load<AudioClip>("Audio/bgm_transit"));
        Assert.IsNotNull(Resources.Load<AudioClip>("Audio/footstep_01"));
        Assert.IsNotNull(Resources.Load<AudioClip>("Audio/collision"));
        Assert.IsNotNull(Resources.Load<AudioClip>("Audio/coin"));
        Assert.IsNotNull(Resources.Load<AudioClip>("Audio/ui_click"));
        Assert.AreEqual(4, GameBalanceConfig.Current.powerUps.Length);
    }

    [UnityTest]
    public IEnumerator RuntimeManagersBootstrapWithoutExceptions()
    {
        yield return null;

        Assert.IsNotNull(GameManager.Instance);
        Assert.IsNotNull(TrackManager.Instance);
        Assert.IsNotNull(PowerUpController.Instance);
        Assert.IsNotNull(AudioManager.Instance);
    }

    [UnityTest]
    public IEnumerator RestartedRunRecreatesTrackBeforeAutoStart()
    {
        yield return null;

        GameManager.Instance.Restart();
        yield return null;
        yield return null;

        Assert.IsNotNull(GameManager.Instance);
        Assert.AreEqual(GameState.Playing, GameManager.Instance.State);
        Assert.IsNotNull(TrackManager.Instance);

        yield return null;
        Assert.Greater(TrackManager.Instance.ActiveSegmentCount, 0,
            "The restarted run must generate track segments.");

        GameManager.Instance.ReturnToMenu();
        yield return null;
        yield return null;
        Assert.AreEqual(GameState.Menu, GameManager.Instance.State);
    }

    [UnityTest]
    public IEnumerator MenuProgressionSystemsBootstrapOnTheRuntimeCanvas()
    {
        yield return null;
        yield return null;

        Assert.IsNotNull(Object.FindObjectOfType<Canvas>());
        Assert.IsNotNull(Object.FindObjectOfType<PowerUpShopUI>());
        Assert.IsNotNull(Object.FindObjectOfType<AITrainingDashboardUI>());
    }

    [UnityTest]
    public IEnumerator MenuPanelTextStaysInsideItsPanel()
    {
        yield return null;
        yield return null;

        PowerUpShopUI shop = Object.FindObjectOfType<PowerUpShopUI>();
        AITrainingDashboardUI training =
            Object.FindObjectOfType<AITrainingDashboardUI>();
        Assert.IsNotNull(shop);
        Assert.IsNotNull(training);

        GameObject shopPanel = GetPrivatePanel(shop);
        Assert.IsNotNull(shopPanel);
        AssertContainedHorizontally(
            shopPanel.transform.Find("Title") as RectTransform);
        AssertContainedHorizontally(
            shopPanel.transform.Find("Feedback") as RectTransform);
        for (int i = 0; i < 4; i++)
        {
            Transform row = shopPanel.transform.Find(
                "Item_" + (PowerUpId)i);
            Assert.IsNotNull(row);
            AssertContainedHorizontally(
                row.Find("Name") as RectTransform);
            AssertContainedHorizontally(
                row.Find("Description") as RectTransform);
        }

        GameObject trainingPanel = GetPrivatePanel(training);
        Assert.IsNotNull(trainingPanel);
        AssertContainedHorizontally(
            trainingPanel.transform.Find("Title") as RectTransform);
    }

    [TestCase(PowerUpId.Shield)]
    [TestCase(PowerUpId.Magnet)]
    [TestCase(PowerUpId.ScoreBoost)]
    [TestCase(PowerUpId.TurboStart)]
    public void PurchaseEquipAndConsumeActivatesEachPowerUp(PowerUpId id)
    {
        string archiveBefore = PlayerPrefs.GetString(EchoRunSaveSystem.SaveKey, "");
        bool hadArchive = PlayerPrefs.HasKey(EchoRunSaveSystem.SaveKey);
        int coinsBefore = PlayerPrefs.GetInt("TotalCoins", 0);
        bool hadCoins = PlayerPrefs.HasKey("TotalCoins");

        try
        {
            var isolated = new EchoRunSaveData
            {
                totalCoins = 200,
                powerUpInventory = new int[4],
                selectedPowerUp = -1
            };
            PlayerPrefs.SetString(EchoRunSaveSystem.SaveKey,
                JsonUtility.ToJson(isolated));
            PlayerPrefs.SetInt("TotalCoins", isolated.totalCoins);
            ResetSaveSystemCache();

            PowerUpBalance definition = GameBalanceConfig.GetPowerUp(id);
            Assert.IsNotNull(definition);
            Assert.IsTrue(EchoRunSaveSystem.TryPurchasePowerUp(id, definition.cost));
            Assert.AreEqual(200 - definition.cost, EchoRunSaveSystem.TotalCoins);
            Assert.AreEqual(1, EchoRunSaveSystem.GetPowerUpCount(id));
            Assert.IsTrue(EchoRunSaveSystem.SelectPowerUp(id));

            PowerUpController controller = PowerUpController.Instance;
            Assert.IsNotNull(controller);
            controller.BeginRun();

            Assert.AreEqual(id, controller.ActivePowerUp);
            Assert.AreEqual(0, EchoRunSaveSystem.GetPowerUpCount(id));
            Assert.AreEqual(PowerUpId.None,
                EchoRunSaveSystem.GetSelectedPowerUp());

            if (id == PowerUpId.Shield)
                Assert.IsTrue(controller.TryAbsorbCollision());
            else if (id == PowerUpId.Magnet)
                Assert.IsTrue(controller.HasMagnet);
            else if (id == PowerUpId.ScoreBoost)
                Assert.Greater(controller.ScoreMultiplier, 1f);
            else if (id == PowerUpId.TurboStart)
                Assert.Greater(controller.GetTurboStartBonus(), 0f);
        }
        finally
        {
            if (PowerUpController.Instance != null)
            {
                typeof(PowerUpController).GetMethod("ClearActive",
                    BindingFlags.Instance | BindingFlags.NonPublic)?
                    .Invoke(PowerUpController.Instance, null);
            }
            if (hadArchive)
                PlayerPrefs.SetString(EchoRunSaveSystem.SaveKey, archiveBefore);
            else
                PlayerPrefs.DeleteKey(EchoRunSaveSystem.SaveKey);
            if (hadCoins) PlayerPrefs.SetInt("TotalCoins", coinsBefore);
            else PlayerPrefs.DeleteKey("TotalCoins");
            PlayerPrefs.Save();
            ResetSaveSystemCache();
        }
    }

    private static void ResetSaveSystemCache()
    {
        typeof(EchoRunSaveSystem).GetField("_data",
            BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, null);
        typeof(EchoRunSaveSystem).GetField("_initialized",
            BindingFlags.Static | BindingFlags.NonPublic)?.SetValue(null, false);
    }

    private static GameObject GetPrivatePanel(object component)
    {
        return (GameObject)component.GetType().GetField("_panel",
            BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(component);
    }

    private static void AssertContainedHorizontally(RectTransform child)
    {
        Assert.IsNotNull(child);
        RectTransform parent = child.parent as RectTransform;
        Assert.IsNotNull(parent);

        float left = parent.rect.xMin
                     + child.anchorMin.x * parent.rect.width
                     + child.anchoredPosition.x
                     - child.pivot.x * child.rect.width;
        float right = left + child.rect.width;
        Assert.GreaterOrEqual(left, parent.rect.xMin - 0.01f,
            child.name + " extends beyond the left panel edge.");
        Assert.LessOrEqual(right, parent.rect.xMax + 0.01f,
            child.name + " extends beyond the right panel edge.");
    }
}
