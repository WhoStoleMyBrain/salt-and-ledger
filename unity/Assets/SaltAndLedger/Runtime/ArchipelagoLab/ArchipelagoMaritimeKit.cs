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

            var harbor = new MaritimeIllustration { name = "harborIllustration", Kind = "harbor" };
            harbor.AddToClassList("sl-illustration-small");
            Root.Q("tradeDialog").Q(className: "modal-heading").Insert(0, harbor);
            var industry = new MaritimeIllustration { name = "industryIllustration", Kind = "bakery" };
            industry.AddToClassList("sl-illustration-small");
            Root.Q("industryDialog").Q(className: "modal-heading").Insert(0, industry);
            var fleet = new MaritimeIllustration { name = "fleetIllustration", Kind = "ship" };
            fleet.AddToClassList("sl-illustration-banner");
            Root.Q("fleetCommandScroll").Insert(0, fleet);
        }
    }
}
