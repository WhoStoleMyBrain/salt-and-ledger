using System;

namespace SaltAndLedger.Simulation
{
    public enum ProductionPriority { Low, Normal, High }

    /// <summary>Complete-batch admission, independent of material/payroll readiness. No missed-batch backlog.</summary>
    internal static class FactoryProduction
    {
        internal static void Validate(int operatingRate, ProductionPriority priority, int credit = 0)
        {
            if (operatingRate < 0 || operatingRate > 100) throw new ArgumentOutOfRangeException(nameof(operatingRate));
            if (!Enum.IsDefined(typeof(ProductionPriority), priority)) throw new ArgumentOutOfRangeException(nameof(priority));
            if (credit < 0 || credit > 199) throw new ArgumentOutOfRangeException(nameof(credit));
        }

        internal static int Admit(int rate, int credit) => credit >= 100 ? credit : checked(credit + rate);

        internal static long Opportunities(long firstDay, long lastDay, int interval, int rate, int credit)
        {
            if (rate == 0 || firstDay > lastDay) return 0;
            var slots = 1 + (lastDay - firstDay) / interval;
            // A retained admission is consumed by the first successful batch without earning another rate increment.
            if (credit >= 100) return checked(1 + ((credit - 100L) + checked((slots - 1) * rate)) / 100);
            return checked((credit + checked(slots * rate)) / 100);
        }

        internal static long? NextAdmittedDay(long firstDay, int interval, bool enabled, int rate, int credit)
        {
            if (!enabled || rate == 0) return null;
            var skipped = credit >= 100 ? 0 : Math.Max(0, (100 - credit + rate - 1) / rate - 1);
            return checked(firstDay + checked((long)skipped * interval));
        }
    }
}
