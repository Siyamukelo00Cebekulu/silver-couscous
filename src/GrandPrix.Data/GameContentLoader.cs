using System.Text.Json;

namespace GrandPrix.Data;

public static class GameContentLoader
{
    private const string EmbeddedContentName = "GrandPrix.Data.Content.default-season.json";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static GameContentDefinition LoadEmbedded()
    {
        using var stream = typeof(GameContentLoader).Assembly.GetManifestResourceStream(EmbeddedContentName)
            ?? throw new InvalidOperationException($"Embedded game content '{EmbeddedContentName}' is missing.");
        return Load(stream);
    }

    public static GameContentDefinition LoadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A content file path is required.", nameof(path));

        using var stream = File.OpenRead(path);
        return Load(stream);
    }

    public static IReadOnlyList<string> ValidateFile(string path)
    {
        try
        {
            _ = LoadFile(path);
            return Array.Empty<string>();
        }
        catch (GameContentException exception)
        {
            return exception.Errors;
        }
        catch (JsonException exception)
        {
            return new[] { $"Invalid JSON: {exception.Message}" };
        }
    }

    public static IReadOnlyList<string> Validate(GameContentDefinition content)
    {
        var errors = new List<string>();
        if (content.SchemaVersion != 1)
            errors.Add("schemaVersion must be 1.");
        if (content.StartDate == default)
            errors.Add("startDate must be a valid date.");
        if (string.IsNullOrWhiteSpace(content.PlayerTeamId))
            errors.Add("playerTeamId is required.");

        var teams = content.Teams ?? new List<TeamDefinition>();
        var drivers = content.Drivers ?? new List<DriverDefinition>();
        var circuits = content.Circuits ?? new List<CircuitDefinition>();
        var engineers = content.Engineers ?? new List<EngineerCandidateDefinition>();
        ValidateIds(teams.Select(item => item.Id), "team", errors);
        ValidateIds(drivers.Select(item => item.Id), "driver", errors);
        ValidateIds(circuits.Select(item => item.Id), "circuit", errors);
        ValidateIds(engineers.Select(item => item.Id), "engineer", errors);

        if (!teams.Any(item => item.Id == content.PlayerTeamId))
            errors.Add($"playerTeamId '{content.PlayerTeamId}' does not identify a team.");
        if (teams.Count < 2)
            errors.Add("At least two teams are required.");
        foreach (var team in teams)
        {
            if (string.IsNullOrWhiteSpace(team.Name))
                errors.Add($"Team '{team.Id}' must have a name.");
            if (team.BudgetCents <= 0 || team.MonthlyBasePayrollCents <= 0)
                errors.Add($"Team '{team.Id}' budget and payroll must be positive.");
            if (!IsFinite(team.CarPaceFactor) || team.CarPaceFactor <= 0)
                errors.Add($"Team '{team.Id}' carPaceFactor must be positive and finite.");
            var teamDrivers = drivers.Where(driver => driver.TeamId == team.Id).ToArray();
            if (teamDrivers.Length != 2)
                errors.Add($"Team '{team.Id}' must have exactly two drivers.");
        }

        var teamIds = new HashSet<string>(teams.Select(item => item.Id), StringComparer.Ordinal);
        foreach (var driver in drivers)
        {
            if (!teamIds.Contains(driver.TeamId))
                errors.Add($"Driver '{driver.Id}' references unknown team '{driver.TeamId}'.");
            if (string.IsNullOrWhiteSpace(driver.Name))
                errors.Add($"Driver '{driver.Id}' must have a name.");
            ValidateRatings(
                driver.Id,
                errors,
                driver.Pace,
                driver.Consistency,
                driver.TyreManagement,
                driver.WetSkill,
                driver.Racecraft,
                driver.Focus,
                driver.Experience,
                driver.Feedback,
                driver.Aggression,
                driver.Defending);
        }

        if (circuits.Count == 0)
            errors.Add("At least one circuit is required.");
        foreach (var circuit in circuits)
        {
            if (string.IsNullOrWhiteSpace(circuit.Name) ||
                string.IsNullOrWhiteSpace(circuit.Country) ||
                string.IsNullOrWhiteSpace(circuit.Characteristic))
                errors.Add($"Circuit '{circuit.Id}' is missing required text.");
            if (circuit.RaceLaps < 3 || circuit.RaceLaps > 100)
                errors.Add($"Circuit '{circuit.Id}' raceLaps must be between 3 and 100.");
            if (!IsFinite(circuit.BaseLapTimeSeconds) || circuit.BaseLapTimeSeconds <= 0 ||
                !IsFinite(circuit.TyreWearFactor) || circuit.TyreWearFactor <= 0 ||
                !IsFinite(circuit.PitLossSeconds) || circuit.PitLossSeconds <= 0 ||
                !IsFinite(circuit.OvertakingDifficulty) || circuit.OvertakingDifficulty < 0)
                errors.Add($"Circuit '{circuit.Id}' has invalid simulation values.");
        }

        foreach (var engineer in engineers)
        {
            if (string.IsNullOrWhiteSpace(engineer.Name) || engineer.AskingAnnualSalaryCents <= 0)
                errors.Add($"Engineer '{engineer.Id}' must have a name and positive annual salary.");
            ValidateRatings(engineer.Id, errors, engineer.Skill);
        }

        return errors;
    }

    private static GameContentDefinition Load(Stream stream)
    {
        var content = JsonSerializer.Deserialize<GameContentDefinition>(stream, SerializerOptions);

        if (content is null)
            throw new GameContentException(new[] { "Content document must not be null." });
        var errors = Validate(content);
        if (errors.Count > 0)
            throw new GameContentException(errors);
        return content;
    }

    private static void ValidateIds(IEnumerable<string> ids, string kind, ICollection<string> errors)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
                errors.Add($"{kind} IDs must not be empty.");
            else if (!seen.Add(id))
                errors.Add($"Duplicate {kind} ID '{id}'.");
        }
    }

    private static void ValidateRatings(string id, ICollection<string> errors, params int[] values)
    {
        if (values.Any(value => value < 0 || value > 100))
            errors.Add($"Ratings for '{id}' must be between 0 and 100.");
    }

    private static bool IsFinite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);
}

public sealed class GameContentException : Exception
{
    public GameContentException(IReadOnlyList<string> errors)
        : base(string.Join(Environment.NewLine, errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
