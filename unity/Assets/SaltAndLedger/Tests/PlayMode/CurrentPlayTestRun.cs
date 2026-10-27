using System.Linq;
using SaltAndLedger.Simulation;

namespace SaltAndLedger.UnityHost.Tests
{
    public static class CurrentPlayTestRun
    {
        public static RunIdentity Commission(string charter = RunScenario.MerchantCharter)
        {
            var draft = GrandCommissionDraft.Generate(99);
            return new RunIdentity(GrandCommission.ChallengeId, charter, seed: 99,
                commission: draft.Resolve(draft.Rounds.Select(round => round.Offers[0].TemplateId)));
        }
    }
}
