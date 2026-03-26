---
title: Configuration
layout: default
nav_order: 5
---

# Configuration

This page covers the settings available under **Configuration** in the IssuePit UI.

---

## API Keys
{: #api-keys }

![API Keys configuration page]({{ '/assets/screenshots/api-keys.png' | relative_url }})

API keys let IssuePit authenticate with external services such as GitHub, AI providers, and cloud platforms.

1. Go to **Configuration → API Keys**.
2. Click **Add Key**.
3. Select the **Provider**:

   | Provider | Used for |
   |----------|----------|
   | **GitHub** | Importing issues, creating branches/PRs |
   | **GitLab** | Same as GitHub, but for GitLab |
   | **Jira** | Importing issues from a Jira project (see [Jira Sync](/projects#jira-sync)) |
   | **OpenAI** | AI agent completions (GPT-4, o3, etc.) |
   | **Anthropic** | AI agent completions (Claude models) |
   | **Azure OpenAI** | Azure-hosted OpenAI models |
   | **Google** | Gemini models |
   | **Hetzner** | Provisioning cloud VMs for agent runtimes |

4. Enter the **Key Value** (the secret token from the provider).
5. Click **Save**.

> **Tip:** GitHub tokens need at minimum `repo` scope to import issues and create branches.

---

## Agent Runtimes

Agent runtimes define where and how agent containers are executed.

1. Go to **Configuration → Agent Runtimes**.
2. Click **New Runtime**.
3. Choose the **Type**:
   - `Docker` / `Podman` — local container runtime
   - `SSH` — remote machine via SSH
   - `Hetzner` — auto-provisioned Hetzner VM
   - `OpenSandbox` — sandbox environment
4. Fill in the connection details (host, credentials, image registry, etc.).
5. Click **Save**.

---

## MCP Servers

MCP (Model Context Protocol) servers extend agents with external tools and data sources.

1. Go to **Configuration → MCP Servers**.
2. Click **Add Server**.
3. Fill in:
   - **Name** — display name (e.g. `GitHub MCP`)
   - **URL** — the MCP server endpoint (e.g. `https://mcp.example.com`)
   - **Configuration** — optional JSON configuration passed to the server
4. Click **Save**.

After adding a server, you can link it to specific agents in the agent settings.

---

## Telegram Bots

IssuePit can send notifications about issue events and agent run results to Telegram.

1. Go to **Configuration → Telegram Bots**.
2. Click **Add Bot**.
3. Fill in:
   - **Name** — display name for this bot configuration
   - **Bot Token** — the token obtained from [@BotFather](https://t.me/BotFather)
   - **Chat ID** — the Telegram chat or channel ID where notifications are sent
4. Click **Save**.

Once configured, IssuePit dispatches notifications whenever issues are created, updated, or assigned, and when agent runs complete.

---

## Voice Input

IssuePit supports creating issues via voice using [Vosk](https://alphacephei.com/vosk/) for offline speech recognition.

When voice input is enabled, a microphone icon appears on the issue creation form. Click it to start recording — spoken text is transcribed and inserted into the issue title and description fields automatically.

To enable voice recognition, the Vosk model must be available to the API service. Set the path via the `Vosk__ModelPath` environment variable in your `docker-compose.yml`:

```yaml
environment:
  Vosk__ModelPath: /models/vosk-model-small-en-us-0.15
volumes:
  - ./models:/models:ro
```

Download a Vosk model from [alphacephei.com/vosk/models](https://alphacephei.com/vosk/models) and place it at the configured path.

---

## Settings

The **Settings** page (gear icon at the bottom of the sidebar) shows:

- **Backend API URL** — the URL the frontend uses to reach the API. Set via the `NUXT_PUBLIC_API_BASE` environment variable.
- **Appearance** — dark mode is the only supported theme.
