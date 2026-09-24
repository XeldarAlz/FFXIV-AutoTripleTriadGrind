using AutoTripleTriadGrind.Core.Triad.Data;

namespace AutoTripleTriadGrind.Core.Triad.Logic.Rules;

[Flags]
public enum TriadRuleFeatures : byte
{
    None = 0,
    CardPlaced = 1 << 0,
    CaptureNeighbors = 1 << 1,
    CaptureWeights = 1 << 2,
    CaptureMath = 1 << 3,
    PostCapture = 1 << 4,
    AllPlaced = 1 << 5,
    FilterNext = 1 << 6,
}

public abstract class TriadRule
{
    protected TriadRule(TriadRuleId id, TriadRuleFeatures features = TriadRuleFeatures.None, TriadSpecialRules specialRules = TriadSpecialRules.None,
        bool allowsCombo = false, bool deckOrderMatters = false)
    {
        Id = id;
        BaseFeatures = features;
        BaseSpecialRules = specialRules;
        BaseAllowsCombo = allowsCombo;
        BaseDeckOrderMatters = deckOrderMatters;
    }

    public TriadRuleId Id { get; }

    protected TriadRuleFeatures BaseFeatures { get; }

    protected TriadSpecialRules BaseSpecialRules { get; }

    protected bool BaseAllowsCombo { get; }

    protected bool BaseDeckOrderMatters { get; }

    public virtual TriadRuleFeatures Features => BaseFeatures;

    public virtual TriadSpecialRules SpecialRules => BaseSpecialRules;

    public virtual bool AllowsCombo => BaseAllowsCombo;

    public virtual bool DeckOrderMatters => BaseDeckOrderMatters;

    public virtual TriadRule Clone() => TriadRules.Create(Id);

    public virtual void OnMatchInit()
    {
    }

    public virtual void OnCardPlaced(TriadGameState state, int boardPosition)
    {
    }

    public virtual void OnCheckCaptureNeighbors(TriadGameState state, int boardPosition, ReadOnlySpan<int> neighbors, List<int> captures)
    {
    }

    public virtual void OnCheckCaptureWeights(TriadGameState state, int boardPosition, int neighborPosition, bool reverseActive, ref int cardNumber, ref int neighborNumber)
    {
    }

    public virtual void OnCheckCaptureMath(TriadGameState state, int boardPosition, int neighborPosition, int cardNumber, int neighborNumber, ref bool captured)
    {
    }

    public virtual void OnPostCaptures(TriadGameState state, int boardPosition)
    {
    }

    public virtual void OnScreenUpdate(TriadGameState state)
    {
    }

    public virtual void OnAllCardsPlaced(TriadGameState state)
    {
    }

    public virtual void OnFilterNextCards(TriadGameState state, ref int allowedCardsMask)
    {
    }

    public virtual void OnScoreCard(ushort cardId, ref float score)
    {
    }
}
