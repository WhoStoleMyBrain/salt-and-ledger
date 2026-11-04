using UnityEngine.UIElements;

namespace SaltAndLedger.UnityHost
{
    public sealed partial class ArchipelagoLabController
    {
        private void ApplyMaritimeKit()
        {
            Root.AddToClassList("sl-theme");
            foreach (var name in new[] { "objectiveCards", "itineraryScroll", "cityEconomyScroll", "commissionMapInspector" })
                Root.Q(name)?.AddToClassList("sl-paper");
        }
    }
}
