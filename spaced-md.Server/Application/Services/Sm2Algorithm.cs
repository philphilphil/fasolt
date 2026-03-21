namespace SpacedMd.Server.Application.Services;

public record Sm2Result(double EaseFactor, int Interval, int Repetitions, string State);

public static class Sm2Algorithm
{
    private const double MinEaseFactor = 1.3;

    public static Sm2Result Calculate(double easeFactor, int interval, int repetitions, int quality)
    {
        var newEf = easeFactor + (0.1 - (5 - quality) * (0.08 + (5 - quality) * 0.02));
        if (newEf < MinEaseFactor) newEf = MinEaseFactor;

        int newInterval;
        int newReps;

        if (quality < 3)
        {
            newReps = 0;
            newInterval = quality == 0 ? 0 : 1;
        }
        else
        {
            newReps = repetitions + 1;
            newInterval = newReps switch
            {
                1 => 1,
                2 => 6,
                _ => (int)Math.Round(interval * newEf),
            };
            if (quality == 5)
                newInterval = (int)Math.Round(newInterval * 1.3);
        }

        var state = (newReps >= 3 && newEf >= 2.0) ? "mature" : "learning";
        return new Sm2Result(newEf, newInterval, newReps, state);
    }
}
