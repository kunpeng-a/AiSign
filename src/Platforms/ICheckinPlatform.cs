using System;
using System.Collections.Generic;
using AiSign.Core;

namespace AiSign.Platforms
{
    internal interface ICheckinPlatform
    {
        string Id { get; }
        string DisplayName { get; }
        bool SupportsMultiAccount { get; }
        bool SupportsOAuth { get; }

        List<AccountDraft> ScanLocal();
        AccountDraft ValidatePastedToken(string raw);

        CheckinOutcome Run(Account account, SecretPayload secret, Action<SecretPayload> persist, bool dryRun);

        bool NeedsRefresh(SecretPayload secret);
        SecretPayload Refresh(SecretPayload secret);
    }
}
