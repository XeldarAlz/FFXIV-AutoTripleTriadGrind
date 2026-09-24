using AutoTripleTriadGrind.Core.Localization;
using AutoTripleTriadGrind.Windows.Components;

namespace AutoTripleTriadGrind.Windows.Sections.Config;

internal static class PlaySettings
{
    public static void Draw(Configuration configuration)
    {
        DrawNpcGroup(configuration);
        DrawSafetyGroup(configuration);
    }

    private static void DrawNpcGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Play.GroupNpcs));

        SettingsRow.Draw(Loc.T(L.Play.LossStreak), Loc.T(L.Play.LossStreakHelp), SettingsControls.RowSliderWidth,
            () => SettingsControls.DrawIntSlider(configuration, "##attg_play_losses", () => configuration.LossStreakSkip,
                value => configuration.LossStreakSkip = value, 0, 20, configuration.LossStreakSkip == 0 ? Loc.T(L.Play.Never) : "%d"));

        SettingsRow.Draw(Loc.T(L.Play.MatchLimit), Loc.T(L.Play.MatchLimitHelp), SettingsControls.RowSliderWidth,
            () => SettingsControls.DrawIntSlider(configuration, "##attg_play_matches", () => configuration.MaxMatchesPerNpc,
                value => configuration.MaxMatchesPerNpc = value, 0, 500, configuration.MaxMatchesPerNpc == 0 ? Loc.T(L.Play.NoLimit) : "%d"));

        SettingsRow.Draw(Loc.T(L.Play.MaxFee), Loc.T(L.Play.MaxFeeHelp), SettingsControls.RowSliderWidth,
            () => SettingsControls.DrawIntSlider(configuration, "##attg_play_fee", () => configuration.MaxMatchFee,
                value => configuration.MaxMatchFee = value, 0, 1000, configuration.MaxMatchFee == 0 ? Loc.T(L.Play.AnyFee) : Loc.T(L.Play.MgpFormat)));

        SettingsRow.Draw(Loc.T(L.Play.Delay), Loc.T(L.Play.DelayHelp), SettingsControls.RowSliderWidth,
            () => SettingsControls.DrawIntSlider(configuration, "##attg_play_delay", () => configuration.DelayBetweenMatchesMs,
                value => configuration.DelayBetweenMatchesMs = value, 0, 10_000, Loc.T(L.Play.MillisecondsFormat)));
    }

    private static void DrawSafetyGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Play.GroupInventory));

        SettingsRow.Draw(Loc.T(L.Play.FreeSlots), Loc.T(L.Play.FreeSlotsHelp), SettingsControls.RowSliderWidth,
            () => SettingsControls.DrawIntSlider(configuration, "##attg_play_slots", () => configuration.MinFreeBagSlots,
                value => configuration.MinFreeBagSlots = value, 1, 20));
    }
}
