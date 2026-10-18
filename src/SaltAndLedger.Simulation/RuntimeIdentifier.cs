using System;

namespace SaltAndLedger.Simulation
{
    internal static class RuntimeIdentifier
    {
        internal static void Validate(string value, string parameterName)
        {
            if (value == null) throw new ArgumentNullException(parameterName);
            if (value.Length == 0 || value[0] < 'a' || value[0] > 'z')
                throw Invalid(parameterName);
            foreach (var character in value)
                if (!(character >= 'a' && character <= 'z') &&
                    !(character >= '0' && character <= '9') && character != '_')
                    throw Invalid(parameterName);
        }

        private static ArgumentException Invalid(string parameterName) =>
            new ArgumentException("Identifier must match [a-z][a-z0-9_]* in full.", parameterName);
    }
}
