namespace IssuePit.Core.Enums;

public enum ApiKeyProvider
{
    Hetzner,
    OpenAi,
    Anthropic,
    GitHub,
    GitLab,
    AzureOpenAi,
    Google,
    Custom,
    /// <summary>OpenRouter — unified API gateway for multiple LLM providers. See https://openrouter.ai/docs</summary>
    OpenRouter,
}
