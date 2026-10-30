using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Proxus.Drivers.OpcUa.SelfTests")]
[assembly: InternalsVisibleTo("Proxus.Drivers.OpcUa.SubscriptionRecoveryInterop")]
[assembly: InternalsVisibleTo("Proxus.Drivers.OpcUa.SubscriptionSoak")]
[assembly: InternalsVisibleTo("Proxus.Drivers.OpcUa.SecurityAotProbe")]
// Corpus-seeded fuzz harnesses call the internal decoders directly (tools/Proxus.Drivers.CorpusFuzz).
[assembly: InternalsVisibleTo("Proxus.Drivers.CorpusFuzz")]
