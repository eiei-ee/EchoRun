using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GameStateTests
{
    private readonly List<GameObject> _objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        foreach (GameObject go in _objects)
            if (go != null)
                Object.DestroyImmediate(go);
        _objects.Clear();
    }

    [Test]
    public void StartGameResetsSessionValues()
    {
        GameManager manager = Create<GameManager>("GameManager");
        manager.BuffName = "Shield";
        manager.BuffTimeRemaining = 5f;
        manager.AddCoins(3);

        manager.StartGame();

        Assert.AreEqual(GameState.Playing, manager.State);
        Assert.AreEqual(manager.startSpeed, manager.CurrentSpeed);
        Assert.AreEqual(0, manager.Score);
        Assert.AreEqual(0, manager.Coins);
        Assert.AreEqual(0f, manager.Distance);
        Assert.IsNull(manager.BuffName);
        Assert.AreEqual(0f, manager.BuffTimeRemaining);
        Assert.AreEqual(1f, Time.timeScale);
    }

    [Test]
    public void ClearInputEmptiesQueuedSwipes()
    {
        InputManager input = Create<InputManager>("InputManager");
        FieldInfo field = typeof(InputManager).GetField(
            "_swipeQueue", BindingFlags.Instance | BindingFlags.NonPublic);
        var queue = (Queue<SwipeDirection>)field.GetValue(input);
        queue.Enqueue(SwipeDirection.Left);
        queue.Enqueue(SwipeDirection.Up);

        input.ClearInput();

        Assert.AreEqual(SwipeDirection.None, input.GetSwipe());
    }

    private T Create<T>(string name) where T : Component
    {
        GameObject go = new GameObject(name);
        _objects.Add(go);
        return go.AddComponent<T>();
    }
}
