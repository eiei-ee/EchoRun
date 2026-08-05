using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        for (int frame = 0; frame < 120 && GameManager.Instance == null; frame++)
            yield return null;

        Assert.IsNotNull(GameManager.Instance);
        Assert.IsNotNull(TrackManager.Instance);
        Assert.IsNotNull(PowerUpController.Instance);
        Assert.IsNotNull(AudioManager.Instance);
    }

    [UnityTest]
    public IEnumerator StyledGameplayPropsKeepTheirGameplayColliders()
    {
        for (int frame = 0; frame < 120 && WorldStyler.Instance == null; frame++)
            yield return null;

        Assert.IsNotNull(WorldStyler.Instance);
        GameObject coin = null;
        var obstacles = new GameObject[3];

        try
        {
            coin = new GameObject("VisualTestCoin");
            BoxCollider coinCollider = coin.AddComponent<BoxCollider>();
            coinCollider.isTrigger = true;
            WorldStyler.Instance.StyleCoin(coin);

            Transform coinVisual = coin.transform.Find("StreamlinedVisual");
            Assert.IsNotNull(coinVisual);
            Assert.IsNotNull(coinVisual.Find("TokenRim"));
            Assert.IsNotNull(coinVisual.Find("TokenInset"));
            Assert.IsNotNull(coinVisual.Find("EnergyCore"));
            Assert.AreEqual(3, coinVisual.childCount,
                "Coin trails must keep the lightweight three-renderer style.");
            Assert.AreSame(coinCollider, coin.GetComponent<BoxCollider>());
            Assert.IsTrue(coinCollider.isTrigger);

            string[] signatureParts =
            {
                "SlideShutterBody",
                "JumpBlockBody",
                "LaneBulkheadBody"
            };
            Vector3[] colliderSizes =
            {
                new Vector3(2.8f, 1.8f, 0.7f),
                new Vector3(3.2f, 0.9f, 0.7f),
                new Vector3(3.4f, 2.7f, 0.9f)
            };
            Vector3[] colliderCenters =
            {
                new Vector3(0f, 0.65f, 0f),
                new Vector3(0f, -0.45f, 0f),
                new Vector3(0f, 0.25f, 0f)
            };
            for (int i = 0; i < obstacles.Length; i++)
            {
                GameObject obstacle = new GameObject("VisualTestObstacle_" + i);
                obstacles[i] = obstacle;
                BoxCollider gameplayCollider = obstacle.AddComponent<BoxCollider>();
                gameplayCollider.isTrigger = true;
                gameplayCollider.size = colliderSizes[i];
                gameplayCollider.center = colliderCenters[i];
                Obstacle data = obstacle.AddComponent<Obstacle>();
                data.type = (ObstacleType)i;

                WorldStyler.Instance.StyleObstacle(obstacle);

                Transform visual = obstacle.transform.Find("StreamlinedVisual");
                Assert.IsNotNull(visual);
                Assert.IsNotNull(visual.Find(signatureParts[i]));
                Assert.AreSame(gameplayCollider,
                    obstacle.GetComponent<BoxCollider>());
                Assert.IsTrue(gameplayCollider.isTrigger);

                Bounds visualBounds = CombinedRendererBounds(visual);
                Assert.GreaterOrEqual(visualBounds.size.x,
                    gameplayCollider.bounds.size.x * 0.9f,
                    data.type + " visual must visibly block its collider width.");
                Assert.LessOrEqual(visualBounds.size.x,
                    gameplayCollider.bounds.size.x * 1.05f,
                    data.type + " visual must not extend beyond its collider.");
                if (data.type == ObstacleType.Low)
                {
                    Assert.LessOrEqual(gameplayCollider.bounds.size.x, 3f,
                        "The slide shutter must stay inside one 3m lane.");
                }
                AssertNoPointLikeObstacleParts(visual);
            }

            yield return null;
        }
        finally
        {
            if (coin != null) Object.Destroy(coin);
            foreach (GameObject obstacle in obstacles)
            {
                if (obstacle != null) Object.Destroy(obstacle);
            }
        }
    }

    private static Bounds CombinedRendererBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        Assert.IsNotEmpty(renderers);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static void AssertNoPointLikeObstacleParts(Transform visual)
    {
        string[] forbiddenNames = { "Node", "Joint", "Hub", "Eye", "Dot" };
        Transform[] parts = visual.GetComponentsInChildren<Transform>();
        foreach (Transform part in parts)
        {
            foreach (string forbiddenName in forbiddenNames)
            {
                Assert.IsFalse(part.name.Contains(forbiddenName),
                    "Point-like obstacle decoration remains: " + part.name);
            }
        }
    }

    [UnityTest]
    public IEnumerator RestartedRunRecreatesTrackBeforeAutoStart()
    {
        GameManager bootstrapManager = GameManager.Instance;
        SceneManager.LoadScene(0);
        for (int frame = 0; frame < 120
             && (GameManager.Instance == null
                 || object.ReferenceEquals(GameManager.Instance, bootstrapManager));
             frame++)
            yield return null;

        Assert.IsNotNull(GameManager.Instance);
        GameManager firstRunManager = GameManager.Instance;

        firstRunManager.Restart();
        for (int frame = 0; frame < 120
             && (GameManager.Instance == null
                 || object.ReferenceEquals(GameManager.Instance, firstRunManager)
                 || GameManager.Instance.State != GameState.Playing);
             frame++)
            yield return null;

        Assert.IsNotNull(GameManager.Instance);
        Assert.AreEqual(GameState.Playing, GameManager.Instance.State);
        Assert.IsNotNull(TrackManager.Instance);

        yield return null;
        Assert.Greater(TrackManager.Instance.ActiveSegmentCount, 0,
            "The restarted run must generate track segments.");

        GameManager restartedManager = GameManager.Instance;
        restartedManager.ReturnToMenu();
        for (int frame = 0; frame < 120
             && (GameManager.Instance == null
                 || object.ReferenceEquals(GameManager.Instance, restartedManager)
                 || GameManager.Instance.State != GameState.Menu);
             frame++)
            yield return null;
        Assert.IsNotNull(GameManager.Instance);
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
