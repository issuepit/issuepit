<template>
  <div class="p-6">
    <!-- Loading -->
    <div v-if="store.loading && !store.currentIssue" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <template v-else-if="store.currentIssue">
      <!-- Breadcrumb -->
      <div class="flex items-center gap-2 text-sm text-gray-500 mb-5">
        <NuxtLink :to="`/projects/${id}`" class="hover:text-gray-300">Project</NuxtLink>
        <span>/</span>
        <NuxtLink :to="`/projects/${id}/issues`" class="hover:text-gray-300">Issues</NuxtLink>
        <template v-if="store.currentIssue.parentIssue">
          <span>/</span>
          <NuxtLink :to="`/projects/${id}/issues/${store.currentIssue.parentIssue.id}`" class="hover:text-gray-300">
            #{{ store.currentIssue.parentIssue.number }} {{ store.currentIssue.parentIssue.title }}
          </NuxtLink>
        </template>
        <span>/</span>
        <span class="text-gray-400">#{{ store.currentIssue.number }}</span>
      </div>

      <div class="flex gap-6">
        <!-- Main Content -->
        <div class="flex-1 min-w-0 space-y-5">
          <!-- Title -->
          <div class="flex items-start gap-3">
            <span :class="statusColor(store.currentIssue.status)" class="w-3 h-3 rounded-full mt-2 shrink-0"></span>
            <div class="flex-1">
              <h1 v-if="!editingTitle" @click="editingTitle = true"
                class="text-2xl font-bold text-white cursor-text hover:text-brand-300 transition-colors leading-tight">
                {{ store.currentIssue.title }}
              </h1>
              <input v-else v-model="titleEdit" @blur="saveTitle" @keyup.enter="saveTitle"
                class="w-full text-2xl font-bold bg-transparent border-b border-brand-500 text-white focus:outline-none pb-0.5" />
            </div>
          </div>

          <!-- Description -->
          <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
            <h2 class="text-xs font-medium text-gray-500 uppercase tracking-wide mb-3">Description</h2>
            <div v-if="!editingBody" @click="startEditingBody"
              class="text-sm text-gray-300 cursor-text min-h-12">
              <div v-if="store.currentIssue.body"
                class="prose prose-invert prose-sm max-w-none"
                v-html="renderedBody"></div>
              <span v-else class="text-gray-600">Click to add description...</span>
            </div>
            <div v-else>
              <div class="flex gap-2 mb-2 border-b border-gray-800 pb-2">
                <button @click="descTab = 'write'"
                  class="text-xs px-2.5 py-1 rounded transition-colors"
                  :class="descTab === 'write' ? 'bg-gray-700 text-white' : 'text-gray-500 hover:text-gray-300'">Write</button>
                <button @click="descTab = 'preview'"
                  class="text-xs px-2.5 py-1 rounded transition-colors"
                  :class="descTab === 'preview' ? 'bg-gray-700 text-white' : 'text-gray-500 hover:text-gray-300'">Preview</button>
              </div>
              <textarea v-if="descTab === 'write'" v-model="bodyEdit" rows="15" autofocus
                class="w-full bg-transparent text-sm text-gray-300 focus:outline-none resize-y font-mono min-h-[15rem]"
                placeholder="Describe this issue... (Markdown supported)"></textarea>
              <div v-else class="prose prose-invert prose-sm max-w-none min-h-16 text-sm"
                v-html="renderedBodyEdit"></div>
              <div class="flex gap-2 mt-3">
                <button @click="saveBody"
                  class="text-xs bg-brand-600 hover:bg-brand-700 text-white px-3 py-1 rounded-md transition-colors">Save</button>
                <button @click="editingBody = false"
                  class="text-xs bg-gray-800 hover:bg-gray-700 text-gray-300 px-3 py-1 rounded-md transition-colors">Cancel</button>
              </div>
            </div>
          </div>

          <!-- Tasks -->
          <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
            <div class="flex items-center justify-between mb-3">
              <h2 class="text-xs font-medium text-gray-500 uppercase tracking-wide">
                Tasks
                <span v-if="store.currentTasks.length" class="ml-1 text-gray-600">
                  {{ store.currentTasks.filter(t => t.status === 'done').length }}/{{ store.currentTasks.length }}
                </span>
              </h2>
            </div>
            <div v-if="store.currentTasks.length" class="space-y-2 mb-3">
              <div v-for="task in store.currentTasks" :key="task.id"
                class="flex items-center gap-2 group">
                <button @click="store.toggleTask(issueId, task.id, task.status !== 'done')"
                  class="w-4 h-4 rounded border shrink-0 flex items-center justify-center transition-colors"
                  :class="task.status === 'done' ? 'bg-brand-600 border-brand-600 text-white' : 'border-gray-600 hover:border-brand-500'">
                  <svg v-if="task.status === 'done'" class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="3" d="M5 13l4 4L19 7" />
                  </svg>
                </button>
                <span class="text-sm flex-1" :class="task.status === 'done' ? 'line-through text-gray-500' : 'text-gray-300'">
                  {{ task.title }}
                </span>
                <button @click="store.deleteTask(issueId, task.id)"
                  class="opacity-0 group-hover:opacity-100 text-gray-600 hover:text-red-400 transition-all">
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                  </svg>
                </button>
              </div>
            </div>
            <div class="flex gap-2">
              <input v-model="newTaskTitle" @keyup.enter="addTask" type="text" placeholder="Add a task..."
                class="flex-1 bg-gray-800 border border-gray-700 rounded px-2.5 py-1.5 text-xs text-white placeholder-gray-600 focus:outline-none focus:ring-1 focus:ring-brand-500" />
              <button @click="addTask" :disabled="!newTaskTitle.trim()"
                class="text-xs bg-brand-600 hover:bg-brand-700 disabled:opacity-40 text-white px-2.5 py-1.5 rounded transition-colors">Add</button>
            </div>
          </div>

          <!-- Sub-Issues -->
          <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
            <h2 class="text-xs font-medium text-gray-500 uppercase tracking-wide mb-3">Sub-Issues</h2>
            <div v-if="store.currentIssue.subIssues?.length" class="space-y-1.5 mb-3">
              <NuxtLink v-for="sub in store.currentIssue.subIssues" :key="sub.id"
                :to="`/projects/${id}/issues/${sub.id}`"
                class="flex items-center gap-2 text-sm text-gray-300 hover:text-white group py-1 px-2 rounded-lg hover:bg-gray-800/60 transition-colors">
                <span :class="statusColor(sub.status)" class="w-2.5 h-2.5 rounded-full shrink-0"></span>
                <span class="text-xs text-gray-600 shrink-0">#{{ sub.number }}</span>
                <span>{{ sub.title }}</span>
              </NuxtLink>
            </div>
            <!-- Create sub-issue -->
            <div v-if="!creatingSubIssue">
              <button @click="creatingSubIssue = true"
                class="text-xs text-gray-500 hover:text-brand-400 transition-colors flex items-center gap-1">
                <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
                </svg>
                Add sub-issue
              </button>
            </div>
            <div v-else class="flex gap-2">
              <input v-model="newSubIssueTitle" @keyup.enter="createSubIssue" @keyup.escape="creatingSubIssue = false"
                type="text" placeholder="Sub-issue title..." autofocus
                class="flex-1 bg-gray-800 border border-gray-700 rounded px-2.5 py-1.5 text-xs text-white placeholder-gray-600 focus:outline-none focus:ring-1 focus:ring-brand-500" />
              <button @click="createSubIssue" :disabled="!newSubIssueTitle.trim()"
                class="text-xs bg-brand-600 hover:bg-brand-700 disabled:opacity-40 text-white px-2.5 py-1.5 rounded transition-colors">Create</button>
              <button @click="creatingSubIssue = false"
                class="text-xs bg-gray-800 hover:bg-gray-700 text-gray-300 px-2.5 py-1.5 rounded transition-colors">Cancel</button>
            </div>
          </div>

          <!-- Linked Issues -->
          <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
            <h2 class="text-xs font-medium text-gray-500 uppercase tracking-wide mb-3">Linked Issues</h2>
            <div v-if="store.currentLinks.length" class="space-y-1.5 mb-3">
              <div v-for="link in store.currentLinks" :key="link.id"
                class="flex items-center gap-2 group py-1 px-2 rounded-lg hover:bg-gray-800/60 transition-colors">
                <span class="text-xs text-brand-400 shrink-0 min-w-[70px]">{{ IssueLinkTypeLabels[link.linkType] }}</span>
                <NuxtLink :to="`/projects/${link.targetIssue?.projectId ?? id}/issues/${link.targetIssueId}`"
                  class="flex items-center gap-1.5 text-sm text-gray-300 hover:text-white flex-1 min-w-0">
                  <span class="text-xs text-gray-600 shrink-0">#{{ link.targetIssue?.number }}</span>
                  <span class="truncate">{{ link.targetIssue?.title }}</span>
                  <span v-if="link.targetIssue?.projectId && link.targetIssue.projectId !== id" class="text-xs text-gray-600 shrink-0 ml-1">↗ cross-project</span>
                </NuxtLink>
                <button @click="store.removeLink(issueId, link.id)"
                  class="opacity-0 group-hover:opacity-100 text-gray-600 hover:text-red-400 transition-all">
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                  </svg>
                </button>
              </div>
            </div>
            <div v-if="!addingLink">
              <button @click="addingLink = true"
                class="text-xs text-gray-500 hover:text-brand-400 transition-colors flex items-center gap-1">
                <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
                </svg>
                Add link
              </button>
            </div>
            <div v-else class="space-y-2">
              <select v-model="linkType"
                class="w-full bg-gray-800 border border-gray-700 rounded px-2.5 py-1.5 text-xs text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
                <option v-for="(label, val) in IssueLinkTypeLabels" :key="val" :value="val">{{ label }}</option>
              </select>
              <select v-model="linkTargetIssueId"
                class="w-full bg-gray-800 border border-gray-700 rounded px-2.5 py-1.5 text-xs text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
                <option value="">Select issue...</option>
                <option v-for="i in allOrgIssues" :key="i.id" :value="i.id">{{ i.projectName ? `[${i.projectName}] ` : '' }}#{{ i.number }} {{ i.title }}</option>
              </select>
              <div class="flex gap-2">
                <button @click="submitAddLink" :disabled="!linkTargetIssueId"
                  class="text-xs bg-brand-600 hover:bg-brand-700 disabled:opacity-40 text-white px-2.5 py-1.5 rounded transition-colors">Add</button>
                <button @click="addingLink = false; linkTargetIssueId = ''"
                  class="text-xs bg-gray-800 hover:bg-gray-700 text-gray-300 px-2.5 py-1.5 rounded transition-colors">Cancel</button>
              </div>
            </div>
          </div>

          <!-- Comments -->
          <div class="bg-gray-900 border border-gray-800 rounded-xl p-5">
            <h2 class="text-xs font-medium text-gray-500 uppercase tracking-wide mb-4">
              Comments
              <span v-if="store.currentComments.length" class="ml-1 text-gray-600">{{ store.currentComments.length }}</span>
            </h2>

            <div v-if="store.currentComments.length" class="space-y-4 mb-5">
              <div v-for="comment in store.currentComments" :key="comment.id"
                class="flex gap-3 group">
                <div class="w-7 h-7 rounded-full bg-brand-700 flex items-center justify-center text-xs font-bold text-white shrink-0 mt-0.5">
                  {{ (comment.user?.username ?? '?').charAt(0).toUpperCase() }}
                </div>
                <div class="flex-1 min-w-0">
                  <div class="flex items-center gap-2 mb-1">
                    <span class="text-xs font-medium text-gray-300">{{ comment.user?.username ?? 'Unknown' }}</span>
                    <span class="text-xs text-gray-600">{{ formatDate(comment.createdAt) }}</span>
                    <div class="ml-auto flex gap-2 opacity-0 group-hover:opacity-100 transition-all">
                      <button @click="startEditingComment(comment)"
                        class="text-gray-600 hover:text-brand-400 text-xs">Edit</button>
                      <button @click="store.deleteComment(issueId, comment.id)"
                        class="text-gray-600 hover:text-red-400 text-xs">Delete</button>
                    </div>
                  </div>
                  <template v-if="editingCommentId === comment.id">
                    <textarea v-model="commentEdit" rows="3"
                      class="w-full bg-gray-800 border border-gray-700 rounded px-2.5 py-2 text-sm text-white focus:outline-none resize-none focus:ring-1 focus:ring-brand-500" />
                    <div class="flex gap-2 mt-2">
                      <button @click="saveComment(comment.id)" :disabled="!commentEdit.trim()"
                        class="text-xs bg-brand-600 hover:bg-brand-700 disabled:opacity-40 text-white px-3 py-1 rounded-md transition-colors">Save</button>
                      <button @click="editingCommentId = null"
                        class="text-xs bg-gray-800 hover:bg-gray-700 text-gray-300 px-3 py-1 rounded-md transition-colors">Cancel</button>
                    </div>
                  </template>
                  <div v-else class="prose prose-invert prose-sm max-w-none text-sm"
                    v-html="parseMarkdown(comment.body)"></div>
                </div>
              </div>
            </div>
            <p v-else class="text-sm text-gray-600 mb-4">No comments yet.</p>

            <!-- Code Review Comments -->
            <div v-if="store.currentCodeReviewComments.length" class="mb-5">
              <h3 class="text-xs font-medium text-gray-500 uppercase tracking-wide mb-3">
                Code Review
                <span class="ml-1 text-gray-600">{{ store.currentCodeReviewComments.length }}</span>
              </h3>
              <div class="space-y-3 pl-3 border-l-2 border-brand-800">
                <div v-for="rc in store.currentCodeReviewComments" :key="rc.id"
                  class="bg-gray-800/60 rounded-lg overflow-hidden">
                  <div class="flex items-center gap-2 px-3 py-1.5 bg-gray-800 border-b border-gray-700/50">
                    <svg class="w-3.5 h-3.5 text-gray-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                        d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
                    </svg>
                    <span class="text-xs text-brand-400 font-mono truncate">{{ rc.filePath }}</span>
                    <span class="text-xs text-gray-500 shrink-0">
                      L{{ rc.startLine }}{{ rc.startLine !== rc.endLine ? `–${rc.endLine}` : '' }}
                    </span>
                    <code class="text-xs text-gray-600 font-mono shrink-0">{{ rc.sha }}</code>
                  </div>
                  <!-- Context before (informational lines, not directly commented on) -->
                  <pre v-if="rc.contextBefore" class="text-xs font-mono text-gray-500 px-3 py-1 overflow-x-auto bg-gray-900/30 opacity-70">{{ rc.contextBefore }}</pre>
                  <!-- Snippet: the actual commented lines -->
                  <pre v-if="rc.snippet" class="text-xs font-mono text-gray-200 px-3 py-2 overflow-x-auto bg-gray-900/70 border-l-2 border-brand-600">{{ rc.snippet }}</pre>
                  <!-- Context after (informational lines, not directly commented on) -->
                  <pre v-if="rc.contextAfter" class="text-xs font-mono text-gray-500 px-3 py-1 overflow-x-auto bg-gray-900/30 opacity-70">{{ rc.contextAfter }}</pre>
                  <div class="px-3 py-2 text-sm text-gray-300 border-t border-gray-700/50">{{ rc.body }}</div>
                </div>
              </div>
            </div>

            <!-- Add comment -->
            <div class="border border-gray-700 rounded-lg overflow-hidden">
              <textarea v-model="newComment" rows="3" placeholder="Leave a comment..."
                class="w-full bg-gray-800 px-3 py-2.5 text-sm text-white placeholder-gray-600 focus:outline-none resize-none" />
              <div class="flex justify-end bg-gray-800/50 px-3 py-2 border-t border-gray-700">
                <button @click="submitComment" :disabled="!newComment.trim()"
                  class="text-xs bg-brand-600 hover:bg-brand-700 disabled:opacity-40 text-white px-3 py-1.5 rounded transition-colors">
                  Comment
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- Sidebar -->
        <div class="w-60 shrink-0 space-y-3">
          <div class="bg-gray-900 border border-gray-800 rounded-xl p-4 space-y-4">

            <!-- Status -->
            <div>
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Status</p>
              <select :value="store.currentIssue.status" @change="updateStatus($event)"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-2.5 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
                <option value="backlog">Backlog</option>
                <option value="todo">Todo</option>
                <option value="in_progress">In Progress</option>
                <option value="in_review">In Review</option>
                <option value="done">Done</option>
                <option value="cancelled">Cancelled</option>
              </select>
            </div>

            <!-- Priority -->
            <div>
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Priority</p>
              <select :value="store.currentIssue.priority" @change="updatePriority($event)"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-2.5 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
                <option value="urgent">🔴 Urgent</option>
                <option value="high">🟠 High</option>
                <option value="medium">🟡 Medium</option>
                <option value="low">🔵 Low</option>
                <option value="no_priority">⚪ No Priority</option>
              </select>
            </div>

            <!-- Type -->
            <div>
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Type</p>
              <select :value="store.currentIssue.type" @change="updateType($event)"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-2.5 py-1.5 text-sm text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
                <option value="issue">📋 Issue</option>
                <option value="bug">🐛 Bug</option>
                <option value="feature">✨ Feature</option>
                <option value="task">✅ Task</option>
                <option value="epic">⚡ Epic</option>
              </select>
            </div>

            <!-- Milestone -->
            <div>
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Milestone</p>
              <div v-if="store.currentIssue.milestoneId" class="flex items-center gap-1 mb-1.5">
                <span class="text-xs text-indigo-300 bg-indigo-900/30 px-2 py-0.5 rounded-full flex items-center gap-1">
                  🏁 {{ milestonesStore.milestones.find(m => m.id === store.currentIssue!.milestoneId)?.title ?? 'Milestone' }}
                  <button @click="updateMilestone(null)" class="hover:opacity-70 ml-0.5">×</button>
                </span>
              </div>
              <select @change="onSetMilestone($event)"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-2.5 py-1.5 text-xs text-gray-400 focus:outline-none focus:ring-1 focus:ring-brand-500">
                <option value="">{{ store.currentIssue.milestoneId ? 'Change milestone' : '+ Set milestone' }}</option>
                <option v-for="m in milestonesStore.milestones" :key="m.id" :value="m.id">{{ m.title }}</option>
              </select>
            </div>

            <!-- Labels -->
            <div>
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Labels</p>
              <div class="flex flex-wrap gap-1 mb-2">
                <span v-for="label in store.currentIssue.labels" :key="label.id"
                  class="flex items-center gap-1 text-xs px-1.5 py-0.5 rounded-full font-medium"
                  :style="{ backgroundColor: label.color + '33', color: label.color }">
                  {{ label.name }}
                  <button @click="store.removeIssueLabel(issueId, label.id)" class="hover:opacity-70">×</button>
                </span>
              </div>
              <div class="relative">
                <select v-if="availableLabels.length" @change="onAddLabel($event)"
                  class="w-full bg-gray-800 border border-gray-700 rounded-lg px-2.5 py-1.5 text-xs text-gray-400 focus:outline-none focus:ring-1 focus:ring-brand-500">
                  <option value="">+ Add label</option>
                  <option v-for="l in availableLabels" :key="l.id" :value="l.id">{{ l.name }}</option>
                </select>
                <p v-else-if="labelsStore.labels.length === 0" class="text-xs text-gray-600">No labels defined</p>
              </div>
            </div>

            <!-- Assignees -->
            <div>
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Assignees</p>
              <div class="space-y-1.5 mb-2">
                <div v-for="a in store.currentIssue.assignees" :key="a.id"
                  class="flex items-center gap-2 group">
                  <div class="w-5 h-5 rounded-full bg-brand-700 flex items-center justify-center text-xs font-bold text-white shrink-0">
                    {{ assigneeInitial(a) }}
                  </div>
                  <span class="text-xs text-gray-300 flex-1 truncate">{{ assigneeName(a) }}</span>
                  <button @click="store.removeAssignee(issueId, a.id)"
                    class="opacity-0 group-hover:opacity-100 text-gray-600 hover:text-red-400 transition-all text-xs">×</button>
                </div>
              </div>
              <!-- Add user assignee -->
              <select v-if="availableUsers.length" @change="onAddUserAssignee($event)"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-2.5 py-1.5 text-xs text-gray-400 focus:outline-none focus:ring-1 focus:ring-brand-500 mb-1">
                <option value="">+ Assign user</option>
                <option v-for="u in availableUsers" :key="u.id" :value="u.id">{{ u.username }}</option>
              </select>
              <!-- Add agent assignee -->
              <select v-if="agentsStore.agents.length" @change="onAddAgentAssignee($event)"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-2.5 py-1.5 text-xs text-gray-400 focus:outline-none focus:ring-1 focus:ring-brand-500">
                <option value="">+ Assign agent</option>
                <option v-for="a in availableAgents" :key="a.id" :value="a.id">🤖 {{ a.name }}</option>
              </select>
            </div>

            <!-- Dates -->
            <div>
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1">Created</p>
              <p class="text-xs text-gray-400">{{ formatDate(store.currentIssue.createdAt) }}</p>
            </div>
            <div v-if="store.currentIssue.dueDate">
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1">Due</p>
              <p class="text-xs text-gray-400">{{ formatDate(store.currentIssue.dueDate) }}</p>
            </div>
          </div>

          <!-- Delete -->
          <button @click="deleteAndGoBack"
            class="w-full text-xs text-red-400 hover:text-red-300 hover:bg-red-900/20 border border-red-900/30 rounded-lg py-2 transition-colors">
            Delete Issue
          </button>
        </div>
      </div>
    </template>

    <div v-else class="flex flex-col items-center justify-center py-20 text-center">
      <p class="text-gray-400">Issue not found</p>
      <NuxtLink :to="`/projects/${id}/issues`" class="mt-3 text-brand-400 hover:text-brand-300 text-sm">← Back to Issues</NuxtLink>
    </div>
  </div>
</template>

<script setup lang="ts">
import { marked } from 'marked'
import DOMPurify from 'dompurify'
import { IssueStatus, IssueType, IssueLinkType, IssueLinkTypeLabels } from '~/types'
import type { IssuePriority } from '~/types'
import { useIssuesStore } from '~/stores/issues'
import { useLabelsStore } from '~/stores/labels'
import { useAgentsStore } from '~/stores/agents'
import { useMilestonesStore } from '~/stores/milestones'
import { useProjectsStore } from '~/stores/projects'

const route = useRoute()
const router = useRouter()
const id = route.params.id as string
const issueId = route.params.issueId as string
const store = useIssuesStore()
const labelsStore = useLabelsStore()
const agentsStore = useAgentsStore()
const milestonesStore = useMilestonesStore()
const projectsStore = useProjectsStore()
const api = useApi()

const editingTitle = ref(false)
const editingBody = ref(false)
const descTab = ref<'write' | 'preview'>('write')
const titleEdit = ref('')
const bodyEdit = ref('')
const newComment = ref('')
const newTaskTitle = ref('')
const creatingSubIssue = ref(false)
const newSubIssueTitle = ref('')
const editingCommentId = ref<string | null>(null)
const commentEdit = ref('')

// Links
const addingLink = ref(false)
const linkTargetIssueId = ref('')
const linkType = ref<IssueLinkType>(IssueLinkType.LinkedTo)
const allOrgIssues = ref<Array<{ id: string; number: number; title: string; projectId: string; projectName?: string }>>([])

// All tenant users for assignee browsing
const tenantUsers = ref<Array<{ id: string; username: string }>>([])

async function fetchTenantUsers() {
  try {
    tenantUsers.value = await api.get<Array<{ id: string; username: string }>>('/api/users/search')
  } catch (e) {
    console.error('Failed to fetch tenant users for assignee selection', e)
    tenantUsers.value = []
  }
}

function parseMarkdown(text: string): string {
  return DOMPurify.sanitize(marked(text, { async: false }))
}

function startEditingBody() {
  bodyEdit.value = store.currentIssue?.body ?? ''
  descTab.value = 'write'
  editingBody.value = true
}

const renderedBody = computed(() => {
  return store.currentIssue?.body ? parseMarkdown(store.currentIssue.body) : ''
})

const renderedBodyEdit = computed(() => {
  return bodyEdit.value ? parseMarkdown(bodyEdit.value) : '<span class="text-gray-600">Nothing to preview.</span>'
})

onMounted(async () => {
  await store.fetchIssue(id, issueId)
  if (store.currentIssue) {
    titleEdit.value = store.currentIssue.title
    bodyEdit.value = store.currentIssue.body ?? ''
  }
  await Promise.all([
    store.fetchComments(issueId),
    store.fetchCodeReviewComments(issueId),
    store.fetchTasks(issueId),
    store.fetchLinks(issueId),
    labelsStore.fetchLabels(id),
    agentsStore.fetchAgents(),
    fetchTenantUsers(),
    milestonesStore.fetchMilestones(id),
    projectsStore.fetchProject(id),
  ])
  // Fetch all issues in this org for link target selection (excluding current), with project name for cross-project display
  try {
    const currentProject = projectsStore.currentProject
    const orgId = currentProject?.orgId
    const [orgIssues, orgProjects] = await Promise.all([
      api.get<Array<{ id: string; number: number; title: string; projectId: string }>>(
        '/api/issues',
        { params: { ...(orgId ? { orgId } : { projectId: id }) } }
      ),
      orgId
        ? api.get<Array<{ id: string; name: string }>>(`/api/orgs/${orgId}/projects`)
        : Promise.resolve([] as Array<{ id: string; name: string }>),
    ])
    const projectNameMap = Object.fromEntries(orgProjects.map((p: { id: string; name: string }) => [p.id, p.name]))
    allOrgIssues.value = orgIssues
      .filter((i: { id: string }) => i.id !== issueId)
      .map((i: { id: string; number: number; title: string; projectId: string }) => ({
        ...i,
        projectName: projectNameMap[i.projectId],
      }))
  } catch (e) {
    console.error('Failed to fetch org issues for link selector', e)
  }
})

const availableLabels = computed(() => {
  const assigned = new Set(store.currentIssue?.labels.map(l => l.id) ?? [])
  return labelsStore.labels.filter(l => !assigned.has(l.id))
})

const availableUsers = computed(() => {
  const assigned = new Set(store.currentIssue?.assignees.filter(a => a.userId).map(a => a.userId) ?? [])
  return tenantUsers.value.filter(u => !assigned.has(u.id))
})

const availableAgents = computed(() => {
  const assigned = new Set(store.currentIssue?.assignees.filter(a => a.agentId).map(a => a.agentId) ?? [])
  return agentsStore.agents.filter(a => !assigned.has(a.id))
})

async function saveTitle() {
  editingTitle.value = false
  if (titleEdit.value && titleEdit.value !== store.currentIssue?.title) {
    await store.updateIssue(id, issueId, { title: titleEdit.value })
  }
}

async function saveBody() {
  editingBody.value = false
  await store.updateIssue(id, issueId, { body: bodyEdit.value })
}

async function updateStatus(e: Event) {
  const val = (e.target as HTMLSelectElement).value as IssueStatus
  await store.updateIssue(id, issueId, { status: val })
}

async function updatePriority(e: Event) {
  const val = (e.target as HTMLSelectElement).value as IssuePriority
  await store.updateIssue(id, issueId, { priority: val })
}

async function updateType(e: Event) {
  const val = (e.target as HTMLSelectElement).value as IssueType
  await store.updateIssue(id, issueId, { type: val })
}

async function updateMilestone(milestoneId: string | null) {
  if (milestoneId) {
    await store.updateIssue(id, issueId, { milestoneId })
  } else {
    await store.clearIssueMilestone(id, issueId)
  }
}

async function onSetMilestone(e: Event) {
  const val = (e.target as HTMLSelectElement).value
  await updateMilestone(val || null)
  ;(e.target as HTMLSelectElement).value = ''
}

async function deleteAndGoBack() {
  await store.deleteIssue(id, issueId)
  router.push(`/projects/${id}/issues`)
}

async function submitComment() {
  if (!newComment.value.trim()) return
  await store.addComment(issueId, newComment.value.trim())
  newComment.value = ''
}

function startEditingComment(comment: { id: string; body: string }) {
  editingCommentId.value = comment.id
  commentEdit.value = comment.body
}

async function saveComment(commentId: string) {
  if (!commentEdit.value.trim()) return
  await store.updateComment(issueId, commentId, commentEdit.value.trim())
  editingCommentId.value = null
  commentEdit.value = ''
}

async function addTask() {
  if (!newTaskTitle.value.trim()) return
  await store.createTask(issueId, newTaskTitle.value.trim())
  newTaskTitle.value = ''
}

async function submitAddLink() {
  if (!linkTargetIssueId.value) return
  await store.addLink(issueId, linkTargetIssueId.value, linkType.value)
  addingLink.value = false
  linkTargetIssueId.value = ''
  linkType.value = IssueLinkType.LinkedTo
}

async function createSubIssue() {  if (!newSubIssueTitle.value.trim() || !store.currentIssue) return
  await store.createIssue(id, {
    title: newSubIssueTitle.value.trim(),
    status: IssueStatus.Todo,
    parentIssueId: issueId,
    type: IssueType.Task,
  })
  creatingSubIssue.value = false
  newSubIssueTitle.value = ''
  await store.fetchIssue(id, issueId)
}

async function onAddLabel(e: Event) {
  const sel = e.target as HTMLSelectElement
  const labelId = sel.value
  if (!labelId) return
  await store.addIssueLabel(issueId, labelId)
  sel.value = ''
}

async function onAddUserAssignee(e: Event) {
  const sel = e.target as HTMLSelectElement
  const userId = sel.value
  if (!userId) return
  await store.addAssignee(issueId, { userId })
  sel.value = ''
}

async function onAddAgentAssignee(e: Event) {
  const sel = e.target as HTMLSelectElement
  const agentId = sel.value
  if (!agentId) return
  await store.addAssignee(issueId, { agentId })
  sel.value = ''
}

function assigneeName(a: { userId?: string; agentId?: string; user?: { username: string } | null; agent?: { name: string } | null }) {
  return a.user?.username ?? a.agent?.name ?? 'Unknown'
}

function assigneeInitial(a: { userId?: string; agentId?: string; user?: { username: string } | null; agent?: { name: string } | null }) {
  const name = assigneeName(a)
  return name.charAt(0).toUpperCase()
}

function statusColor(status: IssueStatus) {
  const map: Record<IssueStatus, string> = {
    [IssueStatus.Backlog]: 'bg-gray-500',
    [IssueStatus.Todo]: 'bg-blue-400',
    [IssueStatus.InProgress]: 'bg-yellow-400',
    [IssueStatus.InReview]: 'bg-purple-400',
    [IssueStatus.Done]: 'bg-green-400',
    [IssueStatus.Cancelled]: 'bg-red-400'
  }
  return map[status] ?? 'bg-gray-500'
}

function formatDate(d: string) {
  return new Date(d).toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric' })
}
</script>
