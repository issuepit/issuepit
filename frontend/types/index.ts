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
  maxConcurrentRunners: number
  createdAt: string
  updatedAt: string
}

export interface User {
  id: string
  tenantId: string
  username: string
  email: string
  createdAt: string
}

export interface GitHubIdentity {
  id: string
  userId: string
  name?: string
  gitHubId: string
  gitHubUsername: string
  gitHubEmail?: string
  agentId?: string
  agentName?: string
  createdAt: string
  updatedAt: string
  projects: Array<{ projectId: string; name: string }>
  orgs: Array<{ orgId: string; name: string }>
}

export interface AuthUser {
  id: string
  username: string
  email: string
  isAdmin: boolean
  createdAt: string
}

export enum OrgRole {
  Member = 0,
  Admin = 1,
  Owner = 2
}

export const OrgRoleLabels: Record<OrgRole, string> = {
  [OrgRole.Member]: 'Member',
  [OrgRole.Admin]: 'Admin',
  [OrgRole.Owner]: 'Owner',
}

export enum ProjectPermission {
  None = 0,
  Read = 1,
  Write = 2,
  CommentPrs = 4,
  MoveKanban = 8,
  Milestones = 16,
  Labels = 32,
  ProjectAdmin = 64,
}

export const ProjectPermissionLabels: Record<number, string> = {
  [ProjectPermission.Read]: 'Read',
  [ProjectPermission.Write]: 'Write',
  [ProjectPermission.CommentPrs]: 'Comment PRs',
  [ProjectPermission.MoveKanban]: 'Move Kanban',
  [ProjectPermission.Milestones]: 'Milestones',
  [ProjectPermission.Labels]: 'Labels',
  [ProjectPermission.ProjectAdmin]: 'Project Admin',
}

export interface Team {
  id: string
  orgId: string
  name: string
  slug: string
  createdAt: string
}

export interface TeamMember {
  teamId: string
  userId: string
  user: User
}

export interface OrganizationMember {
  orgId: string
  userId: string
  user: User
  role: OrgRole
}

export interface ProjectMember {
  id: string
  projectId: string
  userId?: string
  teamId?: string
  user?: User
  team?: Team
  project?: Project
  permissions: ProjectPermission
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
  orgId: string
  name: string
  slug: string
  description?: string
  gitHubRepo?: string
  icon?: string
  color?: string
  isPrivate: boolean
  issueCount: number
  memberCount: number
  mountRepositoryInDocker: boolean
  maxConcurrentRunners: number
  createdAt: string
  updatedAt: string
}

export interface IssueAssignee {
  id: string
  issueId: string
  userId?: string
  user?: User
  agentId?: string
  agent?: Agent
}

export interface IssueComment {
  id: string
  issueId: string
  userId?: string
  user?: User
  body: string
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
  assignees: IssueAssignee[]
  labels: Label[]
  milestoneId?: string
  parentIssueId?: string
  parentIssue?: Issue
  dueDate?: string
  estimate?: number
  createdAt: string
  updatedAt: string
  subIssues?: Issue[]
}

export interface IssueTask {
  id: string
  issueId: string
  title: string
  body?: string
  status: IssueStatus
  createdAt: string
  updatedAt: string
}

export interface McpServer {
  id: string
  orgId: string
  name: string
  description?: string
  url: string
  configuration: string
  allowedTools: string[]
  createdAt: string
  linkedAgents?: Array<{ agentId: string; name: string }>
  linkedProjects?: Array<{ projectId: string; name: string }>
  secrets?: McpServerSecret[]
}

export interface McpServerSecret {
  id: string
  key: string
  scope?: string
  scopeId?: string
  createdAt: string
}

export interface AgentLinkedMcpServer {
  id: string
  name: string
  url: string
  description?: string
  allowedTools: string
}

export interface Agent {
  id: string
  name: string
  description?: string
  systemPrompt: string
  dockerImage: string
  allowedTools: string | string[]
  mcpServers: string[]
  linkedMcpServers?: AgentLinkedMcpServer[]
  isActive: boolean
  runnerType?: RunnerType
  model?: string
  createdAt: string
  updatedAt: string
}

export interface AgentProject {
  agentId: string
  name: string
  isDisabled: boolean
  source: 'project' | 'org'
}

export interface AgentOrg {
  agentId: string
  name: string
  isActive: boolean
}

export interface ProjectMcpServer {
  mcpServerId: string
  name: string
  description?: string
  url: string
  configuration: string
  allowedTools: string
  orgId: string
  createdAt: string
  enabledAgents: Array<{ agentId: string; name: string }>
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

export enum RunnerType {
  OpenCode = 0,
  Codex = 1,
  GitHubCopilotCli = 2,
}

export const RunnerTypeLabels: Record<RunnerType, string> = {
  [RunnerType.OpenCode]: 'OpenCode',
  [RunnerType.Codex]: 'Codex CLI',
  [RunnerType.GitHubCopilotCli]: 'GitHub Copilot CLI',
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

export interface GitRepository {
  id: string
  projectId: string
  remoteUrl: string
  defaultBranch: string
  hasAuth: boolean
  createdAt: string
  lastFetchedAt?: string
  status: 'Active' | 'Disabled' | 'Throttled'
  statusMessage?: string
  throttledUntil?: string
}

export interface GitBranch {
  name: string
  isRemote: boolean
  sha: string
  commitDate?: string
}

export interface GitCommit {
  sha: string
  messageShort: string
  message: string
  authorName: string
  authorEmail: string
  date: string
  parentShas: string[]
}

export interface GitTreeEntry {
  name: string
  path: string
  type: 'tree' | 'blob'
  size: number
}

export interface GitBlob {
  path: string
  size: number
  isBinary: boolean
  content: string
}

export interface GitDiffLine {
  oldLineNumber: number | null
  newLineNumber: number | null
  content: string
  lineType: 'added' | 'removed' | 'context'
}

export interface GitDiffHunk {
  oldStart: number
  oldCount: number
  newStart: number
  newCount: number
  header: string
  lines: GitDiffLine[]
}

export interface GitDiffFile {
  oldPath: string
  newPath: string
  status: string
  addedLines: number
  removedLines: number
  isBinary: boolean
  isTooLarge: boolean
  hunks: GitDiffHunk[]
}

export enum TelegramNotificationEvent {
  IssueCreated = 1,
  IssueUpdated = 2,
  IssueAssigned = 4,
  AgentStarted = 8,
  AgentCompleted = 16,
  AgentFailed = 32,
}

export const TelegramNotificationEventLabels: Record<TelegramNotificationEvent, string> = {
  [TelegramNotificationEvent.IssueCreated]: 'Issue Created',
  [TelegramNotificationEvent.IssueUpdated]: 'Issue Updated',
  [TelegramNotificationEvent.IssueAssigned]: 'Issue Assigned',
  [TelegramNotificationEvent.AgentStarted]: 'Agent Started',
  [TelegramNotificationEvent.AgentCompleted]: 'Agent Completed',
  [TelegramNotificationEvent.AgentFailed]: 'Agent Failed',
}

export interface TelegramBot {
  id: string
  orgId?: string
  projectId?: string
  name: string
  chatId: string
  events: number
  isSilent: boolean
  createdAt: string
}

export enum CiCdRunStatus {
  Pending = 'pending',
  Running = 'running',
  Succeeded = 'succeeded',
  Failed = 'failed',
  Cancelled = 'cancelled',
}

export const CiCdRunStatusLabels: Record<CiCdRunStatus, string> = {
  [CiCdRunStatus.Pending]: 'Pending',
  [CiCdRunStatus.Running]: 'Running',
  [CiCdRunStatus.Succeeded]: 'Succeeded',
  [CiCdRunStatus.Failed]: 'Failed',
  [CiCdRunStatus.Cancelled]: 'Cancelled',
}

export interface CiCdRun {
  id: string
  projectId: string
  agentSessionId?: string
  commitSha: string
  branch?: string
  workflow?: string
  status: CiCdRunStatus
  statusName: string
  startedAt: string
  endedAt?: string
  externalSource?: string
  externalRunId?: string
  workspacePath?: string
}

export enum AgentSessionStatus {
  Pending = 0,
  Running = 1,
  Succeeded = 2,
  Failed = 3,
  Cancelled = 4,
}

export const AgentSessionStatusLabels: Record<AgentSessionStatus, string> = {
  [AgentSessionStatus.Pending]: 'Pending',
  [AgentSessionStatus.Running]: 'Running',
  [AgentSessionStatus.Succeeded]: 'Succeeded',
  [AgentSessionStatus.Failed]: 'Failed',
  [AgentSessionStatus.Cancelled]: 'Cancelled',
}

export interface AgentSession {
  id: string
  agentId: string
  agentName: string
  issueId: string
  issueTitle: string
  issueNumber: number
  commitSha?: string
  gitBranch?: string
  status: AgentSessionStatus
  statusName: string
  startedAt: string
  endedAt?: string
}

export interface DashboardAgentSession extends AgentSession {
  projectId: string
  projectName: string
}

export interface CiCdRunLog {
  id: string
  line: string
  stream: string
  streamName: string
  timestamp: string
}

export interface AgentSessionDetail extends AgentSession {
  projectId: string
  projectName: string
  ciCdRuns: CiCdRun[]
}

export interface IssueHistoryEntry {
  date: string
  open: number
  inProgress: number
  done: number
}

export interface ProjectMetricSnapshot {
  recordedAt: string
  openIssues: number
  inProgressIssues: number
  doneIssues: number
  totalAgentRuns: number
  totalCiCdRuns: number
}
