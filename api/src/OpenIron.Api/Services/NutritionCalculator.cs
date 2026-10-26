using OpenIron.Api.Contracts;
using OpenIron.Api.Data.Entities;

namespace OpenIron.Api.Services;

public record WeighIn(DateOnly Date, double WeightKg);

public record PlanInput(
    Sex Sex,
    DateOnly DateOfBirth,
    double HeightCm,
    ActivityLevel Activity,
    GoalType Goal,
    double? TargetWeightKg,
    double WeeklyRateKg,
    int? AppliedKcal,
    DateOnly? AppliedOn,
    IReadOnlyList<WeighIn> WeighIns,
    DateOnly Today);

/// <summary>
/// Turns a profile, a goal and a run of weigh-ins into a daily calorie target and a verdict on how it's going.
///
/// The baseline is the Mifflin-St Jeor equation times an activity factor. The adaptive part works backwards from
/// the scale: if someone ate roughly <c>A</c> kcal a day and their weight changed by <c>r</c> kg a week, their real
/// maintenance is <c>A - r * 7700 / 7</c>. How much that is trusted over the formula grows with how much data there
/// is. There is no food logging, so "ate roughly A" is the target they last applied, which they can correct.
/// </summary>
public static class NutritionCalculator
{
    public const double KcalPerKg = 7700;

    /// <summary>Days after starting a new target to ignore: the first days of a change mostly move water, not fat.</summary>
    public const int SettlingDays = 5;
    public const int MinDaysOfData = 14;
    public const int MinWeighIns = 5;
    public const int WindowDays = 56;
    public const int TrendDays = 90;

    private const double TrendAlphaPerDay = 0.1;
    private const double MaxLossKgPerWeek = 1.0;
    private const double MaxLossFractionOfBodyWeight = 0.01;
    private const double MaxGainKgPerWeek = 0.5;
    private const double MinProteinKgFactor = 2.0;

    public static int AgeYears(DateOnly dateOfBirth, DateOnly today)
    {
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth.AddYears(age) > today) age--;
        return age;
    }

    /// <summary>Mifflin-St Jeor resting energy expenditure in kcal/day.</summary>
    public static double Bmr(Sex sex, double weightKg, double heightCm, int age)
    {
        var offset = sex switch { Sex.Male => 5, Sex.Female => -161, _ => -78 };
        return 10 * weightKg + 6.25 * heightCm - 5 * age + offset;
    }

    public static double ActivityFactor(ActivityLevel level) => level switch
    {
        ActivityLevel.Sedentary => 1.2,
        ActivityLevel.Light => 1.375,
        ActivityLevel.Moderate => 1.55,
        ActivityLevel.Active => 1.725,
        _ => 1.9,
    };

    /// <summary>The lowest daily target this will ever suggest, whatever the goal says.</summary>
    public static int SafetyFloorKcal(Sex sex) => sex switch { Sex.Male => 1500, Sex.Female => 1200, _ => 1350 };

    /// <summary>
    /// Exponentially smoothed weight. Weigh-ins swing by a kilo or more day to day from water and food in the gut,
    /// so the raw numbers are a poor guide. Smoothing is by elapsed days, so gaps between weigh-ins are handled.
    /// </summary>
    public static List<WeighIn> Trend(IEnumerable<WeighIn> weighIns)
    {
        var result = new List<WeighIn>();
        foreach (var w in weighIns.OrderBy(w => w.Date))
        {
            if (result.Count == 0)
            {
                result.Add(w);
                continue;
            }

            var previous = result[^1];
            var days = Math.Max(w.Date.DayNumber - previous.Date.DayNumber, 0);
            var weight = 1 - Math.Pow(1 - TrendAlphaPerDay, days);
            result.Add(new WeighIn(w.Date, previous.WeightKg + weight * (w.WeightKg - previous.WeightKg)));
        }
        return result;
    }

    /// <summary>Least-squares slope in kg per week, or null if the points don't span any time.</summary>
    public static double? WeeklySlope(IReadOnlyList<WeighIn> points)
    {
        if (points.Count < 2) return null;

        var x0 = points[0].Date.DayNumber;
        var xs = points.Select(p => (double)(p.Date.DayNumber - x0)).ToArray();
        var ys = points.Select(p => p.WeightKg).ToArray();
        var meanX = xs.Average();
        var meanY = ys.Average();
        var variance = xs.Sum(x => (x - meanX) * (x - meanX));
        if (variance == 0) return null;

        var covariance = xs.Zip(ys, (x, y) => (x - meanX) * (y - meanY)).Sum();
        return covariance / variance * 7;
    }

    /// <summary>Null when there is no weigh-in to work from.</summary>
    public static PlanDto? Compute(PlanInput input)
    {
        var weighIns = input.WeighIns.OrderBy(w => w.Date).ToList();
        if (weighIns.Count == 0) return null;

        var trend = Trend(weighIns);
        var weight = trend[^1].WeightKg;
        var age = AgeYears(input.DateOfBirth, input.Today);
        var bmr = Bmr(input.Sex, weight, input.HeightCm, age);
        var formulaMaintenance = bmr * ActivityFactor(input.Activity);

        var (plannedRate, rateLimited) = PlannedRate(input.Goal, input.WeeklyRateKg, weight);

        var (observed, daysOfData, weighInsUsed) = ObservedRate(input, weighIns);

        // Only a rate measured since the current target began can be tied back to that target's calories.
        var tracked = input.AppliedKcal is not null && input.AppliedOn is not null && observed is not null;
        var confidence = tracked ? Confidence(daysOfData, weighInsUsed) : 0;
        var maintenance = formulaMaintenance;
        if (tracked)
        {
            var implied = input.AppliedKcal!.Value - observed!.Value * KcalPerKg / 7;
            implied = Math.Clamp(implied, formulaMaintenance * 0.65, formulaMaintenance * 1.35);
            maintenance = formulaMaintenance * (1 - confidence) + implied * confidence;
        }

        var rawTarget = maintenance + plannedRate * KcalPerKg / 7;
        var floor = SafetyFloorKcal(input.Sex);
        var limited = rawTarget < floor;
        var target = RoundTo(Math.Max(rawTarget, floor), 10);

        var progress = Progress(input, weight, plannedRate, observed, daysOfData, weighInsUsed, target);

        return new PlanDto(
            Math.Round(weight, 1),
            age,
            (int)Math.Round(bmr),
            (int)Math.Round(formulaMaintenance),
            (int)Math.Round(maintenance),
            Math.Round(confidence, 2),
            target,
            limited,
            rateLimited,
            Macros(weight, target),
            progress,
            trend.Where(t => t.Date >= input.Today.AddDays(-TrendDays))
                .Select(t => new TrendPointDto(t.Date, Math.Round(t.WeightKg, 1)))
                .ToList());
    }

    /// <summary>Signed kg per week: negative to lose, positive to gain. Capped to what is realistic and safe.</summary>
    private static (double Rate, bool Limited) PlannedRate(GoalType goal, double requested, double weightKg)
    {
        switch (goal)
        {
            case GoalType.Lose:
                var lossCap = Math.Min(MaxLossKgPerWeek, MaxLossFractionOfBodyWeight * weightKg);
                return (-Math.Min(requested, lossCap), requested > lossCap);
            case GoalType.Gain:
                return (Math.Min(requested, MaxGainKgPerWeek), requested > MaxGainKgPerWeek);
            default:
                return (0, false);
        }
    }

    /// <summary>
    /// The measured weekly change. With a target applied, it is measured from just after it began; without one it is
    /// only a recent figure for display. Null if there isn't enough data to say.
    /// </summary>
    private static (double? Rate, int DaysOfData, int Count) ObservedRate(PlanInput input, List<WeighIn> weighIns)
    {
        DateOnly from;
        int minDays, minCount;
        if (input.AppliedOn is { } applied)
        {
            from = applied.AddDays(SettlingDays);
            minDays = MinDaysOfData;
            minCount = MinWeighIns;
        }
        else
        {
            from = input.Today.AddDays(-28);
            minDays = 10;
            minCount = 4;
        }

        var windowStart = input.Today.AddDays(-WindowDays);
        var points = weighIns.Where(w => w.Date >= from && w.Date >= windowStart && w.Date <= input.Today).ToList();
        if (points.Count == 0) return (null, 0, 0);

        var days = points[^1].Date.DayNumber - points[0].Date.DayNumber;
        if (points.Count < minCount || days < minDays) return (null, days, points.Count);

        return (WeeklySlope(points), days, points.Count);
    }

    /// <summary>How far to trust the scale over the formula: more days, and weigh-ins often enough, mean more.</summary>
    private static double Confidence(int daysOfData, int count)
    {
        var duration = Math.Clamp((daysOfData - 10) / 20.0, 0, 1);
        var frequency = daysOfData == 0 ? 0 : Math.Clamp(count / (daysOfData / 3.0), 0, 1);
        return duration * frequency;
    }

    private static ProgressDto Progress(
        PlanInput input, double weight, double planned, double? observed, int daysOfData, int count, int target)
    {
        var status = ProgressStatus.NoPlan;
        int? daysUntilCheck = null;
        int? suggestion = null;

        if (input.AppliedKcal is not null && input.AppliedOn is { } applied)
        {
            if (observed is null)
            {
                status = ProgressStatus.Gathering;
                var sinceApplied = input.Today.DayNumber - applied.DayNumber;
                var remaining = SettlingDays + MinDaysOfData - sinceApplied;
                daysUntilCheck = remaining > 0 ? remaining : null;
            }
            else
            {
                status = Classify(planned, observed.Value);
                // Adjusting while on track would just chase noise.
                if (status != ProgressStatus.OnTrack)
                {
                    var change = target - input.AppliedKcal.Value;
                    // Exact, so "aim for X" and "change by Y" always agree.
                    if (Math.Abs(change) >= 100) suggestion = change;
                }
            }
        }

        double? remainingKg = null;
        var reached = false;
        DateOnly? eta = null;
        if (input.Goal != GoalType.Maintain && input.TargetWeightKg is { } goalWeight)
        {
            remainingKg = Math.Round(goalWeight - weight, 1);
            reached = input.Goal == GoalType.Lose ? weight <= goalWeight : weight >= goalWeight;

            // Prefer the measured pace once there is one, as long as it is heading the right way.
            var pace = observed is { } o && Math.Sign(o) == Math.Sign(planned) && Math.Abs(o) > 0.02 ? o : planned;
            if (!reached && Math.Abs(pace) > 0.02 && Math.Sign(goalWeight - weight) == Math.Sign(pace))
            {
                var days = (goalWeight - weight) / pace * 7;
                if (days is > 0 and <= 365 * 3) eta = input.Today.AddDays((int)Math.Ceiling(days));
            }
        }

        return new ProgressDto(
            status,
            observed is null ? null : Math.Round(observed.Value, 2),
            Math.Round(planned, 2),
            count,
            daysOfData,
            daysUntilCheck,
            suggestion,
            remainingKg,
            reached,
            eta);
    }

    private static ProgressStatus Classify(double planned, double observed)
    {
        var tolerance = Math.Max(0.1, 0.25 * Math.Abs(planned));
        var difference = observed - planned;
        if (Math.Abs(difference) <= tolerance) return ProgressStatus.OnTrack;

        if (planned == 0) return difference > 0 ? ProgressStatus.DriftingUp : ProgressStatus.DriftingDown;

        var direction = Math.Sign(planned);
        if (Math.Sign(observed) == -direction && Math.Abs(observed) > 0.1) return ProgressStatus.WrongDirection;
        return difference * direction > 0 ? ProgressStatus.FasterThanPlanned : ProgressStatus.SlowerThanPlanned;
    }

    /// <summary>Protein scales with body weight (this is a lifting app), fat is a quarter of calories, carbs fill the rest.</summary>
    private static MacrosDto Macros(double weightKg, int targetKcal)
    {
        var protein = MinProteinKgFactor * weightKg;
        protein = Math.Min(protein, 0.4 * targetKcal / 4);
        var fat = 0.25 * targetKcal / 9;
        var carbs = Math.Max((targetKcal - protein * 4 - fat * 9) / 4, 0);
        return new MacrosDto((int)Math.Round(protein), (int)Math.Round(fat), (int)Math.Round(carbs));
    }

    private static int RoundTo(double value, int step) => (int)(Math.Round(value / step, MidpointRounding.AwayFromZero) * step);
}
