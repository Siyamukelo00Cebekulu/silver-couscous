using System.Collections.Generic;
using System;
using GrandPrix.Core.Abstractions;
using GrandPrix.Core.Common;

namespace GrandPrix.Application;

public interface IGameSession
{
    CommandResult Execute(ICommand command);
    CommandResult HireEngineer(int marketNumber, long offeredAnnualSalaryCents, int contractYears);
    ActionResultViewModel UpgradeFacilityView(string facility);
    ActionResultViewModel StartDevelopmentView(string focus, long budgetCents);
    ActionResultViewModel AdvanceDevelopmentView();
    void AdvanceDay();
    AdvanceSummaryViewModel AdvanceDays(int days);
    AdvanceSummaryViewModel AdvanceToNextEvent();
    DashboardViewModel GetDashboard();
    IReadOnlyList<InboxItemViewModel> GetInbox();
    void MarkInboxRead();
    IReadOnlyList<DriverViewModel> GetDrivers();
    IReadOnlyList<CalendarEventViewModel> GetCalendar();
    IReadOnlyList<ChampionshipEntryViewModel> GetStandings();
    StaffViewModel GetStaff();
    FacilitiesViewModel GetFacilities();
    DevelopmentViewModel GetDevelopment();
    RaceWeekendViewModel GetRaceWeekend();
    IReadOnlyList<PracticeResultViewModel> RunPracticeView(int seed, string focus);
    IReadOnlyList<QualifyingResultViewModel> RunQualifyingView(int seed, string tyre, string risk, int runs);
    RaceTimingViewModel StartRace(int laps, int seed);
    RaceTimingViewModel AdvanceRace(double elapsedSeconds);
    RaceTimingViewModel GetRaceTiming();
    RaceActionViewModel QueuePitStop(int carNumber, string tyre, double extraDelaySeconds);
    RaceActionViewModel SetRaceMode(int carNumber, string mode);
    RaceAdviceViewModel GetRaceAdvice(int carNumber);
    RaceSettlementViewModel SettleLiveRace();
}

public sealed class DashboardViewModel
{
    public string TeamName { get; set; } = string.Empty;
    public DateTime Today { get; set; }
    public string NextEvent { get; set; } = string.Empty;
    public string NextCircuit { get; set; } = string.Empty;
    public DateTime NextEventDate { get; set; }
    public int RoundsRemaining { get; set; }
    public long BudgetRemainingCents { get; set; }
    public int ChampionshipPosition { get; set; }
    public int ChampionshipPoints { get; set; }
    public long CostCapSpentCents { get; set; }
    public long CostCapHeadroomCents { get; set; }
    public double Downforce { get; set; }
    public double DragEfficiency { get; set; }
    public double Reliability { get; set; }
    public double ComponentLifeLaps { get; set; }
    public int StaffCount { get; set; }
    public int UnreadInboxCount { get; set; }
    public IReadOnlyList<string> LatestInboxHeadlines { get; set; } = Array.Empty<string>();
}

public sealed class AdvanceSummaryViewModel
{
    public int DaysAdvanced { get; set; }
    public DateTime Today { get; set; }
    public bool ReachedNextEvent { get; set; }
    public bool StoppedForAttention { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class InboxItemViewModel
{
    public DateTime Date { get; set; }
    public string Headline { get; set; } = string.Empty;
    public bool Unread { get; set; }
}

public sealed class CalendarEventViewModel
{
    public DateTime Date { get; set; }
    public string Circuit { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public int RaceLaps { get; set; }
    public string Characteristic { get; set; } = string.Empty;
    public bool Completed { get; set; }
    public bool Next { get; set; }
}

public sealed class ChampionshipEntryViewModel
{
    public int Position { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public int Points { get; set; }
    public bool IsPlayer { get; set; }
}

public sealed class StaffViewModel
{
    public string TeamName { get; set; } = string.Empty;
    public IReadOnlyList<EngineerViewModel> Engineers { get; set; } = Array.Empty<EngineerViewModel>();
    public IReadOnlyList<EngineerCandidateViewModel> Candidates { get; set; } = Array.Empty<EngineerCandidateViewModel>();
}

public sealed class EngineerViewModel
{
    public string Name { get; set; } = string.Empty;
    public int Skill { get; set; }
    public long AnnualSalaryCents { get; set; }
    public int ContractYears { get; set; }
}

public sealed class EngineerCandidateViewModel
{
    public int MarketNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Skill { get; set; }
    public long AskingAnnualSalaryCents { get; set; }
    public bool Hired { get; set; }
}

public sealed class DriverViewModel
{
    public string Name { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public int CarNumber { get; set; }
    public int Pace { get; set; }
    public int Racecraft { get; set; }
    public int Focus { get; set; }
    public int Consistency { get; set; }
    public int TyreManagement { get; set; }
    public int WetSkill { get; set; }
    public int Experience { get; set; }
    public int Feedback { get; set; }
    public int Aggression { get; set; }
    public int Defending { get; set; }
}

public sealed class FacilitiesViewModel
{
    public int WindTunnelLevel { get; set; }
    public int SimulatorLevel { get; set; }
    public int FactoryLevel { get; set; }
    public long WindTunnelUpgradeCostCents { get; set; }
    public long SimulatorUpgradeCostCents { get; set; }
    public long FactoryUpgradeCostCents { get; set; }
    public long BudgetRemainingCents { get; set; }
}

public sealed class DevelopmentViewModel
{
    public double Downforce { get; set; }
    public double DragEfficiency { get; set; }
    public double Reliability { get; set; }
    public double ComponentLifeLaps { get; set; }
    public long CostCapSpentCents { get; set; }
    public long CostCapLimitCents { get; set; }
    public long CostCapHeadroomCents { get; set; }
    public bool HasActiveProject { get; set; }
    public string ProjectFocus { get; set; } = string.Empty;
    public string ProjectStage { get; set; } = string.Empty;
    public double ProjectProgress { get; set; }
}

public sealed class RaceWeekendViewModel
{
    public string Circuit { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public int RaceLaps { get; set; }
    public bool PracticeComplete { get; set; }
    public bool QualifyingComplete { get; set; }
    public bool RaceComplete { get; set; }
}

public sealed class PracticeResultViewModel
{
    public string DriverName { get; set; } = string.Empty;
    public double BestLapSeconds { get; set; }
    public double LongRunLapSeconds { get; set; }
    public double TyreWearPerLap { get; set; }
    public string EngineerNote { get; set; } = string.Empty;
}

public sealed class QualifyingResultViewModel
{
    public string DriverName { get; set; } = string.Empty;
    public int CarNumber { get; set; }
    public int GridPosition { get; set; }
    public double LapTimeSeconds { get; set; }
}

public sealed class ActionResultViewModel
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class RaceTimingViewModel
{
    public int CurrentLap { get; set; }
    public int TotalLaps { get; set; }
    public TimeSpan RaceTime { get; set; }
    public bool SafetyCarActive { get; set; }
    public double RainIntensity { get; set; }
    public bool Finished { get; set; }
    public IReadOnlyList<RaceCarViewModel> Cars { get; set; } = Array.Empty<RaceCarViewModel>();
    public IReadOnlyList<string> RecentEvents { get; set; } = Array.Empty<string>();
}

public sealed class RaceCarViewModel
{
    public int CarNumber { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public int Position { get; set; }
    public int LapsCompleted { get; set; }
    public double GapToLeaderSeconds { get; set; }
    public double GapToAheadSeconds { get; set; }
    public string Tyre { get; set; } = string.Empty;
    public double TyreWear { get; set; }
    public int PitStops { get; set; }
    public string Mode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsPlayerCar { get; set; }
}

public sealed class RaceActionViewModel
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class RaceAdviceViewModel
{
    public string Message { get; set; } = string.Empty;
    public string SuggestedTyre { get; set; } = string.Empty;
    public bool RecommendPit { get; set; }
}

public sealed class RaceSettlementViewModel
{
    public IReadOnlyList<string> Classification { get; set; } = Array.Empty<string>();
    public IReadOnlyList<RacePayoutViewModel> Payouts { get; set; } = Array.Empty<RacePayoutViewModel>();
    public long TeamBudgetCents { get; set; }
}

public sealed class RacePayoutViewModel
{
    public string TeamName { get; set; } = string.Empty;
    public long AmountCents { get; set; }
    public bool IsPlayer { get; set; }
}
