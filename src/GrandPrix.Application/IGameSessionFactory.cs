using GrandPrix.Core.Abstractions;
using GrandPrix.AI;
using GrandPrix.Data;
using GrandPrix.Persistence;
using GrandPrix.Series.Abstractions;
using GrandPrix.Series.F1;

namespace GrandPrix.Application;

public interface IGameSessionFactory
{
    IGameSession CreateNew();
    IGameSession Load(string slot);
    IReadOnlyList<string> ListSaves();
    void Save(string slot, IGameSession session);
}

public sealed class GameSessionFactory : IGameSessionFactory
{
    private readonly ISaveStore _saveStore;
    private readonly GameContentDefinition _content;
    private readonly ISeriesRulesProvider _seriesRules;
    private readonly ITeamPrincipalPolicy _teamPrincipalPolicy;
    private readonly IAiRaceStrategyPolicy _aiRaceStrategyPolicy;
    private readonly VersionedSaveSerializer _serializer = new(1);

    public GameSessionFactory(
        ISaveStore? saveStore = null,
        GameContentDefinition? content = null,
        ISeriesRulesProvider? seriesRules = null,
        ITeamPrincipalPolicy? teamPrincipalPolicy = null,
        IAiRaceStrategyPolicy? aiRaceStrategyPolicy = null)
    {
        var defaultDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GrandPrixTeamPrincipal",
            "Saves");
        _saveStore = saveStore ?? new FileSaveStore(defaultDirectory);
        _content = content ?? GameContentLoader.LoadEmbedded();
        _seriesRules = seriesRules ?? new F1RulesProvider();
        _teamPrincipalPolicy = teamPrincipalPolicy ?? new ConservativeTeamPrincipalPolicy();
        _aiRaceStrategyPolicy = aiRaceStrategyPolicy ?? new ConservativeAiRaceStrategyPolicy();
    }

    public IGameSession CreateNew() =>
        new GameSession(_content, _seriesRules, _teamPrincipalPolicy, _aiRaceStrategyPolicy);

    public IGameSession Load(string slot)
    {
        var data = _serializer.Deserialize<GameSessionSaveData>(_saveStore.Load(slot));
        var session = new GameSession(
            _content,
            _seriesRules,
            _teamPrincipalPolicy,
            _aiRaceStrategyPolicy);
        session.RestoreSaveData(data);
        return session;
    }

    public IReadOnlyList<string> ListSaves() => _saveStore.ListSlots();

    public void Save(string slot, IGameSession session)
    {
        if (session is not GameSession gameSession)
            throw new ArgumentException(
                "The session was not created by the supported game-session implementation.",
                nameof(session));

        _saveStore.Save(slot, _serializer.Serialize(gameSession.CaptureSaveData()));
    }
}
