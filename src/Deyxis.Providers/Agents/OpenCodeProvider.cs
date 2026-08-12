using Deyxis.Core.Events;

namespace Deyxis.Providers.Agents;

public sealed class OpenCodeProvider(IEventBus eventBus)
    : AgentProviderBase(eventBus, AgentProviderKind.OpenCode);
