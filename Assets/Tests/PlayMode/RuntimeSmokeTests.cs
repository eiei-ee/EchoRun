using System.Collections;
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
        Assert.IsNotNull(PowerUpController.Instance);
        Assert.IsNotNull(AudioManager.Instance);
    }
}
