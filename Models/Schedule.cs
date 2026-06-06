using System;
using System.Collections.Generic;
using System.Text;

namespace ThirdStart.Models
{
    public class Schedule
    {
        // Marks if the task is currently considered handled. When DueDate is in the past
        // and IsComplete is false, the task is overdue.
        // When completed, set IsComplete = true, update LastCompleted, then call EvaluateSchedule.
        // EvaluateSchedule will set IsComplete = false if DueDate is past and the period wasn't completed.
        public bool IsComplete { get; set; }

        // How the due date is calculated
        public ScheduleMode Mode { get; set; }

        // Used when Mode is FixedInterval or RelativeToCompletion
        public TimeSpan Interval { get; set; }

        // Used when Mode is SpecificDayOfWeek.
        // Nullable to allow for tasks that don't need to use it.
        public DayOfWeek? RelativeToDay { get; set; }

        // Used when Mode is SpecificDayOfWeek and a monthly cadence is desired.
        // Specifies WHICH occurrence of RelativeToDay within the month to target
        // (e.g., First Tuesday, Third Friday, Last Monday).
        // When null, falls back to simple weekly behavior — the next occurrence of that weekday.
        public WeekOfMonth? OccurrenceInMonth { get; set; }

        // Used when Mode is SpecificDayOfWeek and OccurrenceInMonth is set.
        // Controls how many months to skip between occurrences.
        // 1 = every month, 2 = every other month, 3 = quarterly, etc.
        // Ignored when OccurrenceInMonth is null (weekly mode doesn't skip months).
        public int MonthInterval { get; set; } = 1;

        // The next time this task is due, calculated when EvaluateSchedule is called.
        // Should be recalculated when a new record gets submitted and merged by the host.
        public DateTimeOffset DueDate { get; set; }

        // The last time the task was completed (used to calculate next DueDate)
        public DateTimeOffset? LastCompleted { get; set; }

        // Recalculates DueDate based on the current Mode.
        // Call this after marking a task complete (with LastCompleted already updated),
        // or during a periodic refresh to detect when a new period begins.
        public void EvaluateSchedule()
        {
            var now = DateTimeOffset.UtcNow;

            // Only act if the task is marked complete AND the DueDate has already passed.
            // - IsComplete=false: DueDate already represents the upcoming/overdue date, no change needed.
            // - IsComplete=true, DueDate in future: task is done for this period, nothing to do yet.
            if (IsComplete && DueDate <= now)
            {
                // Check if the completion actually belongs to the current period.
                // If LastCompleted predates DueDate, the task was completed in a prior period —
                // the current period was never handled, so the task is now overdue.
                if (!LastCompleted.HasValue || LastCompleted.Value < DueDate)
                {
                    IsComplete = false;
                    // Leave DueDate as-is; it already reflects when the task became overdue.
                    return;
                }

                // LastCompleted >= DueDate: task was genuinely completed for this period.
                // Advance DueDate to the next scheduled occurrence.
                switch (Mode)
                {
                    case ScheduleMode.FixedInterval:
                        // Guard against zero/negative intervals which would cause an infinite loop.
                        if (Interval <= TimeSpan.Zero)
                            break;

                        // Advance from the current DueDate by Interval.
                        // Looping handles catch-up if evaluation was delayed across multiple periods
                        // (e.g., device was offline). Preserves the original cadence anchor.
                        do
                        {
                            DueDate = DueDate.Add(Interval);
                        }
                        while (DueDate <= now);
                        break;

                    case ScheduleMode.RelativeToCompletion:
                        // Next due date is relative to actual completion time, not the prior DueDate.
                        // This "drifts" naturally with real-world usage rather than a rigid calendar.
                        DueDate = LastCompleted!.Value.Add(Interval);
                        break;

                    case ScheduleMode.SpecificDayOfWeek:
                        if (!RelativeToDay.HasValue)
                            break;

                        if (OccurrenceInMonth.HasValue)
                        {
                            // --- Monthly cadence ---
                            // Example: "First Tuesday every month" or "Third Friday every 2 months"
                            // MonthInterval controls how many months to advance each time.
                            int effectiveInterval = Math.Max(1, MonthInterval);

                            // Anchor the search to MonthInterval months after the current DueDate.
                            // Using DueDate (not now) as the anchor preserves the intended rhythm
                            // even when evaluation is delayed across several months.
                            var searchMonth = AddMonths(DueDate, effectiveInterval);
                            DateTimeOffset next = DateTimeOffset.MinValue;

                            // Search forward until we land on a valid future occurrence.
                            // Capped at 120 months (~10 years) to guard against degenerate inputs.
                            int attempts = 0;
                            while (attempts++ < 120)
                            {
                                next = GetNthDayOfWeekInMonth(
                                    searchMonth.Year, searchMonth.Month,
                                    RelativeToDay.Value, OccurrenceInMonth.Value,
                                    DueDate.Offset);

                                if (next > now)
                                    break; // Found a valid future date — done.

                                if (next == DateTimeOffset.MinValue)
                                    // The requested occurrence doesn't exist in this specific month
                                    // (rare edge case, e.g. a 4th Saturday in a very short month).
                                    // Try the next consecutive month without resetting the interval.
                                    searchMonth = AddMonths(searchMonth, 1);
                                else
                                    // Found a valid occurrence but it's still in the past
                                    // (e.g., evaluation skipped several months). Jump by full interval.
                                    searchMonth = AddMonths(searchMonth, effectiveInterval);
                            }

                            // Only commit if a valid future date was actually found.
                            if (next > now)
                                DueDate = next;
                        }
                        else
                        {
                            // --- Simple weekly fallback ---
                            // OccurrenceInMonth not set: just find the next occurrence of the weekday.
                            DueDate = GetNextDayOfWeek(now, RelativeToDay.Value);
                        }
                        break;
                }

                // Final safety check: if DueDate is still in the past after all advances
                // (e.g., bad Interval, or logic couldn't find a future date), mark as overdue.
                if (DueDate <= now)
                    IsComplete = false;
            }
        }

        /// <summary>
        /// Returns the next occurrence of <paramref name="targetDay"/> strictly after
        /// <paramref name="reference"/>, at midnight (preserving the UTC offset).
        /// </summary>
        private static DateTimeOffset GetNextDayOfWeek(DateTimeOffset reference, DayOfWeek targetDay)
        {
            int daysUntil = ((int)targetDay - (int)reference.DayOfWeek + 7) % 7;

            // If today is already the target day, push to next week.
            if (daysUntil == 0)
                daysUntil = 7;

            var target = reference.AddDays(daysUntil);
            return new DateTimeOffset(target.Year, target.Month, target.Day,
                                      0, 0, 0, reference.Offset);
        }

        /// <summary>
        /// Returns the date of the Nth <paramref name="targetDay"/> within the given month,
        /// at midnight using <paramref name="utcOffset"/>.
        /// Returns <see cref="DateTimeOffset.MinValue"/> if that occurrence doesn't exist
        /// in the given month (e.g., a 4th Saturday when the month ends too early).
        /// </summary>
        private static DateTimeOffset GetNthDayOfWeekInMonth(
            int year, int month, DayOfWeek targetDay, WeekOfMonth occurrence, TimeSpan utcOffset)
        {
            if (occurrence == WeekOfMonth.Last)
            {
                // Walk backwards from the last day of the month to find the final occurrence.
                var date = new DateTime(year, month, DateTime.DaysInMonth(year, month));
                while (date.DayOfWeek != targetDay)
                    date = date.AddDays(-1);
                return new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, utcOffset);
            }

            // Find the first occurrence of targetDay in the month, then jump forward by weeks.
            var firstOfMonth = new DateTime(year, month, 1);
            int daysUntilFirst = ((int)targetDay - (int)firstOfMonth.DayOfWeek + 7) % 7;

            // occurrence is 1-based (First=1, Second=2, etc.), so subtract 1 to get week offset.
            var nthDate = firstOfMonth.AddDays(daysUntilFirst + ((int)occurrence - 1) * 7);

            // If adding weeks pushed past the end of the month, the occurrence doesn't exist.
            if (nthDate.Month != month)
                return DateTimeOffset.MinValue;

            return new DateTimeOffset(nthDate.Year, nthDate.Month, nthDate.Day, 0, 0, 0, utcOffset);
        }

        /// <summary>
        /// Adds calendar months to a <see cref="DateTimeOffset"/>, preserving the UTC offset.
        /// Uses BCL month arithmetic which correctly handles month-end clamping (e.g., Jan 31 + 1 month = Feb 28).
        /// </summary>
        private static DateTimeOffset AddMonths(DateTimeOffset date, int months)
        {
            var shifted = date.DateTime.AddMonths(months);
            return new DateTimeOffset(shifted, date.Offset);
        }
    }

    public enum ScheduleMode { FixedInterval, RelativeToCompletion, SpecificDayOfWeek }

    /// <summary>
    /// Specifies which occurrence of a weekday within a calendar month to target.
    /// Used with <see cref="Schedule.OccurrenceInMonth"/> when
    /// <see cref="Schedule.Mode"/> is <see cref="ScheduleMode.SpecificDayOfWeek"/>.
    /// </summary>
    public enum WeekOfMonth
    {
        First  = 1,
        Second = 2,
        Third  = 3,
        Fourth = 4,
        Last   = 0   // Handled separately — walks back from end of month
    }
}
