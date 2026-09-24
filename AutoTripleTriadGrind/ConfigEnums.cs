namespace AutoTripleTriadGrind;

public enum AfterRunAction
{
    StayLoggedIn,
    Logout,
    ReturnToInn,
    CloseGame,
}

public enum TriadRunMode
{
    Collect,
    Farm,
}

public enum FarmStopKind
{
    Never,
    Matches,
}

public enum DeckSource
{
    Optimized,
    OwnDeck,
}

public enum LibraryView
{
    ByNpc,
    ByCard,
}

public enum LibraryFilter
{
    All,
    Missing,
    Owned,
}
