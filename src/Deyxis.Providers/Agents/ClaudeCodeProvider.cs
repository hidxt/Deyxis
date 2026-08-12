using Deyxis.Core.Events;

namespace Deyxis.Providers.Agents;

public sealed class ClaudeCodeProvider(IEventBus eventBus)
    : AgentProviderBase(eventBus, AgentProviderKind.ClaudeCode);
