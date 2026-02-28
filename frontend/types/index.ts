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
  slug: string
  createdAt: string
  updatedAt: string
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
  name: string
  url: string
  description?: string
  tools: string[]
  isActive: boolean
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

export interface KanbanColumn {
  id: string
  boardId: string
  title: string
  status: IssueStatus
  order: number
  issueIds: string[]
}

export interface KanbanBoard {
  id: string
  projectId: string
  title: string
  columns: KanbanColumn[]
  createdAt: string
  updatedAt: string
}

export interface PaginatedResponse<T> {
  items: T[]
  total: number
  page: number
  pageSize: number
}
