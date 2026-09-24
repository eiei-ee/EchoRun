using UnityEngine;

public enum EchoHudTransitionKind
{
    None,
    Scan,
    Activate,
    Fracture,
    Release
}

public struct EchoHudSkin
{
    public Color panel;
    public Color panelRaised;
    public Color ink;
    public Color mutedInk;
    public Color rule;
    public Color accent;
    public Color accentSoft;
    public EchoHudTransitionKind transition;
}

public static class EchoRunUITheme
{
    // One palette across the world-facing menu, social sheet, and race HUD.
    // Legacy names remain aliases so consumers do not maintain parallel palettes.
    public static readonly Color Backdrop = new Color32(20, 34, 57, 255);
    public static readonly Color Surface = new Color32(29, 48, 77, 255);
    public static readonly Color SurfaceRaised = new Color32(42, 61, 89, 255);
    public static readonly Color SurfaceSelected = new Color32(54, 79, 114, 255);
    public static readonly Color Echo = new Color32(197, 154, 239, 255);
    public static readonly Color ActionAccent = new Color32(243, 231, 142, 255);
    public static readonly Color ActionAccentDark = new Color32(124, 110, 53, 255);
    public static readonly Color Reward = new Color32(232, 215, 132, 255);
    public static readonly Color Danger = new Color32(214, 108, 140, 255);
    public static readonly Color Success = ActionAccent;
    public static readonly Color TextPrimary = new Color32(241, 234, 246, 255);
    public static readonly Color TextMuted = new Color32(185, 190, 209, 255);
    public static readonly Color Ink = Backdrop;
    public static readonly Color RouteCyan = Echo;
    public static readonly Color RouteCyanDark = SurfaceSelected;

    public static readonly Color MenuPaper = Backdrop;
    public static readonly Color MenuInk = TextPrimary;
    public static readonly Color MenuMutedInk = TextMuted;
    public static readonly Color MenuCoral = ActionAccent;
    public static readonly Color MenuCoralEdge = ActionAccentDark;
    public static readonly Color MenuLake = Echo;
    public static readonly Color MenuMint = ActionAccent;
    public static readonly Color MenuLakeSoft = SurfaceRaised;

    // Ink-blue backings keep text stable as track lighting changes.
    public static readonly Color HudPanel = new Color32(18, 30, 50, 224);
    public static readonly Color HudPanelRaised = new Color32(34, 50, 76, 222);
    public static readonly Color HudMessageVeil = Color.clear;
    public static readonly Color HudPredictionVeil = new Color32(18, 30, 50, 232);
    public static readonly Color HudInk = TextPrimary;
    public static readonly Color HudInkMuted = TextMuted;
    public static readonly Color HudRule = new Color32(185, 190, 209, 62);
    public static readonly Color HudTextShadow = new Color32(20, 34, 57, 150);
    public static readonly Color HudDangerText = Danger;
    public static readonly Color HudRewardText = Reward;
    public static readonly Color HudSuccessText = Success;
    public static readonly Color HudCalibrationAccent = new Color32(162, 136, 190, 255);
    public static readonly Color HudChallengeAccent = Echo;
    public static readonly Color HudRelearnAccent = Danger;
    public static readonly Color HudFinaleAccent = ActionAccent;

    public static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    public static EchoHudSkin HudSkinFor(SingleContractVisualState state)
    {
        switch (state)
        {
            case SingleContractVisualState.Challenge:
                return MakeHudSkin(HudChallengeAccent,
                    EchoHudTransitionKind.Activate);
            case SingleContractVisualState.RelearnPulse:
                return MakeHudSkin(HudRelearnAccent,
                    EchoHudTransitionKind.Fracture);
            case SingleContractVisualState.Finale:
                return MakeHudSkin(HudFinaleAccent,
                    EchoHudTransitionKind.Release);
            default:
                return MakeHudSkin(HudCalibrationAccent,
                    EchoHudTransitionKind.Scan);
        }
    }

    public static EchoHudSkin HudSkinFor(EchoHudMode mode)
    {
        switch (mode)
        {
            case EchoHudMode.Counterattack:
            case EchoHudMode.Rewrite:
            case EchoHudMode.FinaleFailed:
                return HudSkinFor(SingleContractVisualState.RelearnPulse);
            case EchoHudMode.FinaleClean:
            case EchoHudMode.FinaleContract:
                return HudSkinFor(SingleContractVisualState.Finale);
            case EchoHudMode.Reveal:
            case EchoHudMode.Resistance:
                return HudSkinFor(SingleContractVisualState.Challenge);
            default:
                return HudSkinFor(SingleContractVisualState.Calibration);
        }
    }

    private static EchoHudSkin MakeHudSkin(Color accent,
        EchoHudTransitionKind transition)
    {
        return new EchoHudSkin
        {
            panel = HudPanel,
            panelRaised = HudPanelRaised,
            ink = HudInk,
            mutedInk = HudInkMuted,
            rule = HudRule,
            accent = accent,
            accentSoft = WithAlpha(accent, 0.14f),
            transition = transition
        };
    }
}
