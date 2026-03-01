// IssuePit Domain Types

export enum IssueStatus {
  Backlog = 'backlog',
  Todo = 'todo',
  InProgress = 'in_progress',
  InReview = 'in_review',
  Done = 'done',
  Cancelled = 'cancelled'
}

export enum IssuePriority {
  NoPriority = 'no_priority',
  Urgent = 'urgent',
  High = 'high',
  Medium = 'medium',
  Low = 'low'
}

export enum IssueType {
  Issue = 'issue',
  Bug = 'bug',
  Feature = 'feature',
  Task = 'task',
  Epic = 'epic'
}

export interface Tenant {
  id: string
  name: string
  hostname: string
  databaseConnectionString?: string
  createdAt: string
}

export interface Organization {
  id: string
  tenantId: string
  name: string
  slug: string
  description?: string
  createdAt: string
  updatedAt: string
}

export interface Label {
  id: string
  projectId: string
  name: string
  color: string
  description?: string
}

export interface Milestone {
  id: string
  projectId: string
  title: string
  description?: string
  dueDate?: string
  status: 'open' | 'closed'
  createdAt: string
  updatedAt: string
}

export interface Project {
  id: string
  organizationId: string
  name: string
  slug: string
  description?: string
  icon?: string
  color?: string
  isPrivate: boolean
  issueCount: number
  memberCount: number
  createdAt: string
  updatedAt: string
}

export interface Issue {
  id: string
  projectId: string
  number: number
  title: string
  body?: string
  status: IssueStatus
  priority: IssuePriority
  type: IssueType
  assigneeIds: string[]
  labelIds: string[]
  milestoneId?: string
  parentIssueId?: string
  dueDate?: string
  estimate?: number
  createdAt: string
  updatedAt: string
}

export interface IssueTask {
  id: string
  issueId: string
  title: string
  completed: boolean
  order: number
}

export interface McpServer {
  id: string
  orgId: string
  name: string
  url: string
  configuration: string
  createdAt: string
}

export interface Agent {
  id: string
  name: string
  description?: string
  systemPrompt: string
  dockerImage: string
  allowedTools: string[]
  mcpServers: string[]
  isActive: boolean
  createdAt: string
  updatedAt: string
}

export interface KanbanTransition {
  id: string
  boardId: string
  fromColumnId: string
  toColumnId: string
  name: string
  isAuto: boolean
  agentId?: string
  createdAt: string
}

export interface KanbanColumn {
  id: string
  boardId: string
  name: string
  issueStatus: IssueStatus
  position: number
}

export interface KanbanBoard {
  id: string
  projectId: string
  name: string
  columns: KanbanColumn[]
  createdAt: string
}

export interface PaginatedResponse<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}

export enum ApiKeyProvider {
  Hetzner = 0,
  OpenAi = 1,
  Anthropic = 2,
  GitHub = 3,
  GitLab = 4,
  AzureOpenAi = 5,
  Google = 6,
  Custom = 7,
}

export const ApiKeyProviderLabels: Record<ApiKeyProvider, string> = {
  [ApiKeyProvider.Hetzner]: 'Hetzner',
  [ApiKeyProvider.OpenAi]: 'OpenAI',
  [ApiKeyProvider.Anthropic]: 'Anthropic',
  [ApiKeyProvider.GitHub]: 'GitHub',
  [ApiKeyProvider.GitLab]: 'GitLab',
  [ApiKeyProvider.AzureOpenAi]: 'Azure OpenAI',
  [ApiKeyProvider.Google]: 'Google',
  [ApiKeyProvider.Custom]: 'Custom',
}

export enum RuntimeType {
  Native = 0,
  Docker = 1,
  Ssh = 2,
  HetznerSsh = 3,
  OpenSandbox = 4,
}

export const RuntimeTypeLabels: Record<RuntimeType, string> = {
  [RuntimeType.Native]: 'Native / Bare Metal',
  [RuntimeType.Docker]: 'Docker (DinD)',
  [RuntimeType.Ssh]: 'SSH',
  [RuntimeType.HetznerSsh]: 'Hetzner + Terraform + SSH',
  [RuntimeType.OpenSandbox]: 'OpenSandbox',
}

export interface ApiKey {
  id: string
  orgId: string
  name: string
  provider: ApiKeyProvider
  providerName: string
  createdAt: string
  expiresAt?: string
}

export interface RuntimeConfiguration {
  id: string
  orgId: string
  name: string
  type: RuntimeType
  typeName: string
  configuration: string
  isDefault: boolean
  createdAt: string
}
