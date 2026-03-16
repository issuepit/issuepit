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
  Low = 'low',
  VeryHigh = 'very_high',
  Unknown = 'unknown'
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
  concurrentJobs?: number | null
  actRunnerImage?: string
  actEnv?: string
  actSecrets?: string
  actionCachePath?: string | null
  useNewActionCache?: boolean
  actionOfflineMode?: boolean
  localRepositories?: string | null
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
  startDate?: string
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
  isAgenda: boolean
  issueCount: number
  memberCount: number
  mountRepositoryInDocker: boolean
  maxConcurrentRunners: number
  concurrentJobs?: number | null
  actEnv?: string
  actSecrets?: string
  actRunnerImage?: string
  actionCachePath?: string | null
  useNewActionCache?: boolean | null
  actionOfflineMode?: boolean | null
  localRepositories?: string | null
  requiresRunApproval: boolean
  openMergeRequestCount: number
  /** Short project key used as prefix for issue IDs in the UI (e.g. "IP" yields "IP-123"). */
  issueKey?: string | null
  /** Offset added to issue numbers when displayed in the UI. Defaults to 0. */
  issueNumberOffset: number
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

export enum IssueLinkType {
  LinkedTo = 'linked_to',
  Blocks = 'blocks',
  BlockedBy = 'blocked_by',
  Causes = 'causes',
  CausedBy = 'caused_by',
  Solves = 'solves',
  Duplicates = 'duplicates',
  DuplicatedBy = 'duplicated_by',
  Requires = 'requires',
  RequiredBy = 'required_by',
  Implements = 'implements',
  ImplementedBy = 'implemented_by',
}

export const IssueLinkTypeLabels: Record<IssueLinkType, string> = {
  [IssueLinkType.LinkedTo]: 'linked to',
  [IssueLinkType.Blocks]: 'blocks',
  [IssueLinkType.BlockedBy]: 'blocked by',
  [IssueLinkType.Causes]: 'causes',
  [IssueLinkType.CausedBy]: 'caused by',
  [IssueLinkType.Solves]: 'solves',
  [IssueLinkType.Duplicates]: 'duplicates',
  [IssueLinkType.DuplicatedBy]: 'duplicated by',
  [IssueLinkType.Requires]: 'requires',
  [IssueLinkType.RequiredBy]: 'required by',
  [IssueLinkType.Implements]: 'implements',
  [IssueLinkType.ImplementedBy]: 'implemented by',
}

export interface IssueLink {
  id: string
  issueId: string
  targetIssueId: string
  targetIssue?: Issue
  linkType: IssueLinkType
  createdAt: string
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

export interface IssueAttachment {
  id: string
  issueId: string
  commentId?: string
  userId?: string
  user?: User
  fileName: string
  fileUrl: string
  contentType: string
  fileSize: number
  isVoiceFile: boolean
  isPublic: boolean
  createdAt: string
}

export interface CodeReviewComment {
  id: string
  issueId: string
  filePath: string
  startLine: number
  endLine: number
  sha: string
  /** The actual lines that were commented on (startLine..endLine). Review target for agentic tools. */
  snippet?: string
  /** Context-only lines immediately before the commented block (informational, not directly commented on). */
  contextBefore?: string
  /** Context-only lines immediately after the commented block (informational, not directly commented on). */
  contextAfter?: string
  body: string
  createdAt: string
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
  kanbanRank: number
  gitHubIssueNumber?: number
  gitHubIssueUrl?: string
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
  agentType?: OpenCodeAgentType
  useHttpServer?: boolean
  hasHttpServerPassword?: boolean
  parentAgentId?: string
  childAgents?: AgentChild[]
  createdAt: string
  updatedAt: string
}

export interface AgentChild {
  id: string
  name: string
  model?: string
  systemPrompt: string
  agentType?: OpenCodeAgentType
  isActive: boolean
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

export enum KanbanLaneProperty {
  Status = 'status',
  Priority = 'priority',
  Label = 'label',
  Type = 'type',
  Agent = 'agent',
  Milestone = 'milestone',
}

export interface KanbanColumn {
  id: string
  boardId: string
  name: string
  issueStatus: IssueStatus
  position: number
  /** Value identifying which issues belong to this column for non-Status lane properties. */
  laneValue?: string | null
}

export interface KanbanBoard {
  id: string
  projectId: string
  name: string
  /** The issue property used to determine which lane an issue belongs to. Defaults to Status (0). */
  laneProperty: KanbanLaneProperty
  columns: KanbanColumn[]
  createdAt: string
}

export enum ProjectPropertyType {
  Text = 'text',
  Enum = 'enum',
  Number = 'number',
  Date = 'date',
  Person = 'person',
  Agent = 'agent',
  Bool = 'bool',
}

export interface ProjectProperty {
  id: string
  projectId: string
  name: string
  type: ProjectPropertyType
  isRequired: boolean
  defaultValue?: string | null
  allowedValues?: string | null
  position: number
  createdAt: string
}

export interface IssuePropertyValue {
  id: string
  issueId: string
  propertyId: string
  property: ProjectProperty
  value?: string | null
  createdAt: string
  updatedAt: string
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

export enum OpenCodeAgentType {
  SubAgent = 0,
  Primary = 1,
  All = 2,
}

export const OpenCodeAgentTypeLabels: Record<OpenCodeAgentType, string> = {
  [OpenCodeAgentType.SubAgent]: 'Subagent',
  [OpenCodeAgentType.Primary]: 'Primary',
  [OpenCodeAgentType.All]: 'All (default)',
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
  maxConcurrentAgents: number
  createdAt: string
}

export interface AgentPoolStatus {
  runtimeConfigId: string | null
  runtimeName: string
  maxConcurrentAgents: number
  runningAgents: number
  pendingAgents: number
}

export interface CiCdPoolStatus {
  orgId: string
  orgName: string
  maxConcurrentRunners: number
  runningCiCdRuns: number
  pendingCiCdRuns: number
}

export interface PoolStatus {
  agentPools: AgentPoolStatus[]
  cicdPools: CiCdPoolStatus[]
}

export type GitOriginMode = 'ReadOnly' | 'Working' | 'Release'

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
  mode: GitOriginMode
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

export enum DigestInterval {
  Immediate = 0,
  Hourly = 1,
  Daily = 2,
}

export const DigestIntervalLabels: Record<DigestInterval, string> = {
  [DigestInterval.Immediate]: 'Immediate',
  [DigestInterval.Hourly]: 'Hourly digest',
  [DigestInterval.Daily]: 'Daily digest',
}

export interface TelegramBot {
  id: string
  orgId?: string
  projectId?: string
  name: string
  chatId: string
  events: number
  isSilent: boolean
  digestInterval: DigestInterval
  createdAt: string
}

export enum CiCdRunStatus {
  Pending = 'pending',
  Running = 'running',
  Succeeded = 'succeeded',
  Failed = 'failed',
  Cancelled = 'cancelled',
  WaitingForApproval = 'waiting_for_approval',
  SucceededWithWarnings = 'succeeded_with_warnings',
}

export const CiCdRunStatusLabels: Record<CiCdRunStatus, string> = {
  [CiCdRunStatus.Pending]: 'Pending',
  [CiCdRunStatus.Running]: 'Running',
  [CiCdRunStatus.Succeeded]: 'Succeeded',
  [CiCdRunStatus.Failed]: 'Failed',
  [CiCdRunStatus.Cancelled]: 'Cancelled',
  [CiCdRunStatus.WaitingForApproval]: 'Waiting for Approval',
  [CiCdRunStatus.SucceededWithWarnings]: 'Succeeded with Warnings',
}

export interface CiCdRun {
  id: string
  projectId: string
  projectName?: string
  agentSessionId?: string
  retryOfRunId?: string
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
  eventName?: string
  inputsJson?: string
}

export type LinkedRunType = 'retry-of' | 'retry' | 'agent-triggered' | 'same-sha'

export interface LinkedCiCdRun {
  id: string
  projectId: string
  commitSha?: string
  branch?: string
  workflow?: string
  status: CiCdRunStatus | AgentSessionStatus
  statusName: string
  startedAt: string
  endedAt?: string
  linkType: LinkedRunType
  linkLabel: string
  /** Only for agent-triggered links */
  issueTitle?: string
  issueNumber?: number
  gitBranch?: string
}

export enum AgentSessionStatus {
  Pending = 'pending',
  Running = 'running',
  Succeeded = 'succeeded',
  Failed = 'failed',
  Cancelled = 'cancelled',
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
  /** The opencode session ID captured from the agent run. */
  openCodeSessionId?: string | null
  /** URL of the opencode web UI (set during HTTP server mode runs, cleared after session ends). */
  serverWebUiUrl?: string | null
  /** S3 URL of the preserved opencode DB snapshot for this session. */
  openCodeDbS3Url?: string | null
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
  jobId?: string
  stepId?: string
  timestamp: string
}

export interface CiCdTestCase {
  id: string
  fullName: string
  className?: string
  methodName?: string
  outcome: number
  outcomeName: string
  durationMs: number
  errorMessage?: string
  stackTrace?: string
}

export interface CiCdTestSuite {
  id: string
  artifactName: string
  totalTests: number
  passedTests: number
  failedTests: number
  skippedTests: number
  durationMs: number
  createdAt: string
  testCases: CiCdTestCase[]
}

export interface CiCdArtifact {
  id: string
  name: string
  sizeBytes: number
  fileCount: number
  storageKey?: string
  isTestResultArtifact: boolean
  createdAt: string
}

export interface TestRunSummary {
  runId: string
  commitSha?: string
  branch?: string
  startedAt: string
  endedAt?: string
  statusName: string
  totalTests: number
  passedTests: number
  failedTests: number
  skippedTests: number
  durationMs: number
  suiteCount: number
}

export interface TestStats {
  fullName: string
  className?: string
  methodName?: string
  totalRuns: number
  passedRuns: number
  failedRuns: number
  skippedRuns: number
  avgDurationMs: number
  lastOutcome: number
  lastOutcomeName: string
  lastRunAt: string
}

export interface TestCaseHistoryEntry {
  id: string
  outcome: number
  outcomeName: string
  durationMs: number
  errorMessage?: string
  stackTrace?: string
  runId: string
  commitSha?: string
  branch?: string
  runAt: string
  artifactName: string
}

export interface TestRunCompareResult {
  runA: { id: string; commitSha: string; branch?: string; startedAt: string; testCount: number }
  runB: { id: string; commitSha: string; branch?: string; startedAt: string; testCount: number }
  added: { fullName: string; outcomeName: string; durationMs: number }[]
  removed: { fullName: string; outcomeName: string; durationMs: number }[]
  fixed_: { fullName: string; durationMsA: number; durationMsB: number }[]
  regressed: { fullName: string; durationMsA: number; durationMsB: number; errorMessage?: string }[]
  slowedDown: { fullName: string; durationMsA: number; durationMsB: number; deltaMs: number }[]
  summary: { addedCount: number; removedCount: number; fixedCount: number; regressedCount: number; slowedDownCount: number }
}

export interface WorkflowJobNode {
  id: string
  name: string
  runsOn?: string
  needs: string[]
  workflowFile?: string
  callerWorkflowFile?: string
}

export interface WorkflowEdge {
  from: string
  to: string
}

export interface WorkflowGraph {
  jobs: WorkflowJobNode[]
  edges: WorkflowEdge[]
  warnings: string[]
  workflowTriggers?: Record<string, string[]>
}

export interface WorkflowInput {
  name: string
  description?: string
  default?: string
  required: boolean
  type: string
  options?: string[]
}

export interface WorkflowInfo {
  fileName: string
  triggers: string[]
  dispatchInputs: WorkflowInput[]
}

export interface AgentSessionLog {
  id: string
  line: string
  stream: string
  streamName: string
  timestamp: string
  section?: string
  sectionIndex: number
}

export interface AgentSessionDetail extends AgentSession {
  projectId: string
  projectName: string
  ciCdRuns: CiCdRun[]
  /** JSON-serialised string array of warnings (e.g. truncated comments). Null when no warnings. */
  warnings?: string | null
}

export interface IssueAgentSession {
  id: string
  agentId: string
  agentName: string
  issueId: string
  commitSha?: string
  gitBranch?: string
  status: AgentSessionStatus
  statusName: string
  startedAt: string
  endedAt?: string
  ciCdRuns: CiCdRun[]
  /** The opencode session ID captured from the agent run. */
  openCodeSessionId?: string | null
  /** URL of the opencode web UI (set during HTTP server mode runs, cleared after session ends). */
  serverWebUiUrl?: string | null
}

export interface IssueRuns {
  agentSessions: IssueAgentSession[]
}

export interface IssueHistoryEntry {
  date: string
  open: number
  inProgress: number
  done: number
}

export enum IssueEventType {
  Created = 'created',
  TitleChanged = 'title_changed',
  DescriptionChanged = 'description_changed',
  StatusChanged = 'status_changed',
  PriorityChanged = 'priority_changed',
  TypeChanged = 'type_changed',
  LabelAdded = 'label_added',
  LabelRemoved = 'label_removed',
  AssigneeAdded = 'assignee_added',
  AssigneeRemoved = 'assignee_removed',
  MilestoneSet = 'milestone_set',
  MilestoneCleared = 'milestone_cleared',
  PropertyChanged = 'property_changed',
}

export const IssueEventTypeLabels: Record<IssueEventType, string> = {
  [IssueEventType.Created]: 'created',
  [IssueEventType.TitleChanged]: 'changed title',
  [IssueEventType.DescriptionChanged]: 'updated description',
  [IssueEventType.StatusChanged]: 'changed status',
  [IssueEventType.PriorityChanged]: 'changed priority',
  [IssueEventType.TypeChanged]: 'changed type',
  [IssueEventType.LabelAdded]: 'added label',
  [IssueEventType.LabelRemoved]: 'removed label',
  [IssueEventType.AssigneeAdded]: 'assigned',
  [IssueEventType.AssigneeRemoved]: 'unassigned',
  [IssueEventType.MilestoneSet]: 'set milestone',
  [IssueEventType.MilestoneCleared]: 'cleared milestone',
  [IssueEventType.PropertyChanged]: 'changed property',
}

export interface IssueEvent {
  id: string
  issueId: string
  eventType: IssueEventType
  oldValue?: string
  newValue?: string
  actorUserId?: string
  actorUser?: User
  actorAgentId?: string
  actorAgent?: Agent
  createdAt: string
}

export interface IssueGitMapping {
  id: string
  issueId: string
  repositoryId: string
  repositoryUrl: string
  branchName?: string
  commitSha?: string
  source: 'BranchName' | 'CommitMessage'
  detectedAt: string
}

export interface ProjectMetricSnapshot {
  recordedAt: string
  openIssues: number
  inProgressIssues: number
  doneIssues: number
  totalAgentRuns: number
  totalCiCdRuns: number
}

export enum SkillSyncStatus {
  None = 0,
  Synced = 1,
  Ahead = 2,
  Behind = 3,
  Error = 4,
}

export const SkillSyncStatusLabels: Record<SkillSyncStatus, string> = {
  [SkillSyncStatus.None]: 'No Repo',
  [SkillSyncStatus.Synced]: 'Synced',
  [SkillSyncStatus.Ahead]: 'Ahead',
  [SkillSyncStatus.Behind]: 'Behind',
  [SkillSyncStatus.Error]: 'Error',
}

export interface Skill {
  id: string
  orgId: string
  name: string
  description?: string
  content?: string
  gitRepoUrl?: string
  gitSubDir?: string
  gitAuthUsername?: string
  syncStatus: SkillSyncStatus
  syncStatusName: string
  syncMessage?: string
  lastSyncedAt?: string
  createdAt: string
  updatedAt: string
}

export enum TodoPriority {
  NoPriority = 'noPriority',
  Low = 'low',
  Medium = 'medium',
  High = 'high',
  Urgent = 'urgent',
}

export const TodoPriorityLabels: Record<TodoPriority, string> = {
  [TodoPriority.NoPriority]: 'No Priority',
  [TodoPriority.Low]: 'Low',
  [TodoPriority.Medium]: 'Medium',
  [TodoPriority.High]: 'High',
  [TodoPriority.Urgent]: 'Urgent',
}

export enum TodoRecurringInterval {
  None = 'none',
  Daily = 'daily',
  Weekly = 'weekly',
  Monthly = 'monthly',
  Yearly = 'yearly',
}

export const TodoRecurringIntervalLabels: Record<TodoRecurringInterval, string> = {
  [TodoRecurringInterval.None]: 'No Repeat',
  [TodoRecurringInterval.Daily]: 'Daily',
  [TodoRecurringInterval.Weekly]: 'Weekly',
  [TodoRecurringInterval.Monthly]: 'Monthly',
  [TodoRecurringInterval.Yearly]: 'Yearly',
}

export interface TodoCategory {
  id: string
  boardId: string
  name: string
  color: string
  position: number
}

export interface TodoBoard {
  id: string
  tenantId: string
  name: string
  description?: string
  createdAt: string
  categories: TodoCategory[]
}

export interface TodoBoardMembership {
  todoId: string
  boardId: string
  board: TodoBoard
}

export interface TodoCategoryMembership {
  todoId: string
  categoryId: string
  category: TodoCategory
}

export interface Todo {
  id: string
  tenantId: string
  title: string
  body?: string
  isCompleted: boolean
  priority: TodoPriority
  dueDate?: string
  startDate?: string
  recurringInterval: TodoRecurringInterval
  createdAt: string
  updatedAt: string
  boardMemberships: TodoBoardMembership[]
  categoryMemberships: TodoCategoryMembership[]
}

// ──────────────────────────────────────────────────────────────────────────────
// GitHub Sync
// ──────────────────────────────────────────────────────────────────────────────

export enum GitHubSyncTriggerMode {
  Off = 0,
  Manual = 1,
  Auto = 2,
}

export const GitHubSyncTriggerModeLabels: Record<GitHubSyncTriggerMode, string> = {
  [GitHubSyncTriggerMode.Off]: 'Off',
  [GitHubSyncTriggerMode.Manual]: 'Manual',
  [GitHubSyncTriggerMode.Auto]: 'Auto',
}

export enum GitHubSyncMode {
  Import = 0,
  TwoWay = 1,
  CreateOnGitHub = 2,
}

export const GitHubSyncModeLabels: Record<GitHubSyncMode, string> = {
  [GitHubSyncMode.Import]: 'Import (GitHub → IssuePit)',
  [GitHubSyncMode.TwoWay]: 'Two-Way (GitHub ↔ IssuePit)',
  [GitHubSyncMode.CreateOnGitHub]: 'Create on GitHub (IssuePit → GitHub)',
}

export const GitHubSyncModeDescriptions: Record<GitHubSyncMode, string> = {
  [GitHubSyncMode.Import]: 'Imports issues from GitHub into IssuePit. Existing IssuePit issues are not modified on GitHub.',
  [GitHubSyncMode.TwoWay]: 'Imports from GitHub and pushes recent IssuePit changes back to GitHub.',
  [GitHubSyncMode.CreateOnGitHub]: 'When a new issue is created in IssuePit it is automatically pushed to GitHub.',
}

export enum GitHubSyncRunStatus {
  Pending = 0,
  Running = 1,
  Succeeded = 2,
  Failed = 3,
}

export enum GitHubSyncLogLevel {
  Info = 0,
  Warn = 1,
  Error = 2,
}

export interface GitHubSyncConfig {
  id?: string
  projectId: string
  gitHubIdentityId?: string
  gitHubIdentityName?: string
  gitHubRepo?: string
  triggerMode: GitHubSyncTriggerMode
  syncMode: GitHubSyncMode
  createdAt?: string
  updatedAt?: string
}

export interface GitHubSyncRun {
  id: string
  projectId: string
  status: GitHubSyncRunStatus
  summary?: string
  startedAt: string
  completedAt?: string
}

export interface GitHubSyncRunLog {
  id: string
  level: GitHubSyncLogLevel
  message: string
  timestamp: string
}

export interface GitHubSyncRunDetail extends GitHubSyncRun {
  logs: GitHubSyncRunLog[]
}

export interface GitHubConflict {
  issueId: string
  issueNumber: number
  gitHubIssueNumber: number
  localTitle: string
  gitHubTitle: string
  localBody?: string
  gitHubBody?: string
  gitHubUrl: string
  titleDiffers: boolean
  bodyDiffers: boolean
}

// ──────────────────────────────────────────────────────────────────────────────
// Scheduled Tasks (cross-project view)
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Type discriminator for scheduled task runs.</summary>
export type ScheduledTaskType = 'GitHubSync' | 'BranchDetection'

export interface ScheduledTaskRun {
  id: string
  projectId: string
  projectName: string
  type: ScheduledTaskType
  status: GitHubSyncRunStatus
  summary?: string
  startedAt: string
  completedAt?: string
}

export interface ScheduledTaskProject {
  projectId: string
  name: string
}



// ──────────────────────────────────────────────────────────────────────────────
// MCP Access Tokens
// ──────────────────────────────────────────────────────────────────────────────

export interface McpAccessToken {
  id: string
  tenantId?: string
  orgId?: string
  projectId?: string
  userId?: string
  name: string
  isReadOnly: boolean
  createdAt: string
  expiresAt?: string
}
