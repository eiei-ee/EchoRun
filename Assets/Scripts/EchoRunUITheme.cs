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
    public static readonly Color Backdrop = new Color32(10, 21, 35, 255);
    public static readonly Color Surface = new Color32(22, 40, 59, 255);
    public static readonly Color SurfaceRaised = new Color32(31, 55, 78, 255);
    public static readonly Color SurfaceSelected = new Color32(18, 82, 105, 255);
    public static readonly Color RouteCyan = new Color32(57, 215, 255, 255);
    public static readonly Color RouteCyanDark = new Color32(14, 105, 132, 255);
    public static readonly Color Reward = new Color32(240, 173, 61, 255);
    public static readonly Color Danger = new Color32(255, 103, 90, 255);
    public static readonly Color Success = new Color32(116, 226, 197, 255);
    public static readonly Color TextPrimary = new Color32(232, 243, 248, 255);
    public static readonly Color TextMuted = new Color32(165, 187, 200, 255);
    public static readonly Color Ink = new Color32(5, 14, 22, 255);

    // The in-run HUD is one restrained floating instrument layer. Information
    // shares a dark translucent rail instead of becoming a stack of white
    // cards. Stage identity still belongs to the sparse accent geometry.
    public static readonly Color HudPanel = new Color32(4, 10, 14, 164);
    public static readonly Color HudPanelRaised = new Color32(8, 16, 21, 196);
    public static readonly Color HudMessageVeil = new Color32(0, 0, 0, 0);
    public static readonly Color HudInk = new Color32(238, 244, 246, 255);
    public static readonly Color HudInkMuted = new Color32(170, 185, 189, 240);
    public static readonly Color HudRule = new Color32(218, 232, 234, 46);
    public static readonly Color HudTextShadow = new Color32(0, 0, 0, 190);
    public static readonly Color HudDangerText = new Color32(255, 117, 104, 255);
    public static readonly Color HudRewardText = new Color32(241, 181, 74, 255);
    public static readonly Color HudSuccessText = new Color32(102, 220, 196, 255);

    public static readonly Color HudCalibrationAccent =
        new Color32(74, 143, 196, 255);
    public static readonly Color HudChallengeAccent =
        new Color32(20, 177, 177, 255);
    public static readonly Color HudRelearnAccent =
        new Color32(212, 70, 64, 255);
    public static readonly Color HudFinaleAccent =
        new Color32(224, 145, 45, 255);

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
            accentSoft = WithAlpha(accent, 0.22f),
            transition = transition
        };
    }
}
