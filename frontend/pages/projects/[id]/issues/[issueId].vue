<template>
  <div class="p-6">
    <!-- Loading -->
    <div v-if="store.loading && !store.currentIssue" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <template v-else-if="store.currentIssue">
      <!-- Breadcrumb + action buttons -->
      <div class="flex items-center justify-between mb-5">
        <div class="flex items-center gap-2">
          <NuxtLink :to="`/projects/${id}`" class="text-xl font-bold text-gray-500 hover:text-gray-300 transition-colors">{{ projectsStore.currentProject?.name || 'Project' }}</NuxtLink>
          <span class="text-gray-600">/</span>
          <NuxtLink :to="`/projects/${id}/issues`" class="text-xl font-bold text-gray-500 hover:text-gray-300 transition-colors">Issues</NuxtLink>
          <template v-if="store.currentIssue.parentIssue">
            <span class="text-gray-600">/</span>
            <NuxtLink :to="`/projects/${id}/issues/${store.currentIssue.parentIssue.number}`" class="text-xl font-bold text-gray-500 hover:text-gray-300 transition-colors">
              {{ formatIssueId(store.currentIssue.parentIssue.number, projectsStore.currentProject) }} {{ store.currentIssue.parentIssue.title }}
            </NuxtLink>
          </template>
          <span class="text-gray-600">/</span>
          <NuxtLink :to="`/projects/${id}/issues/${store.currentIssue.number}`" class="text-xl font-bold text-white">{{ formatIssueId(store.currentIssue.number, projectsStore.currentProject) }}</NuxtLink>
        </div>
        <!-- Issue creation buttons -->
        <div class="flex items-center gap-2">
          <button @click="showVoiceCreate = true"
            class="flex items-center gap-2 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium px-3 py-1.5 rounded-lg transition-colors"
            title="Create issue from voice">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 016 0v6a3 3 0 01-3 3z" />
            </svg>
            Voice
          </button>
          <button @click="showCreate = true"
            class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-3 py-1.5 rounded-lg transition-colors">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            New Issue
          </button>
        </div>
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
                placeholder="Describe this issue... (Markdown supported)"
                @paste="e => handleImagePaste(e, md => bodyEdit += md)"></textarea>
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

          <!-- Tab Bar -->
          <div class="flex flex-wrap gap-0.5 border-b border-gray-800 pb-0">
            <button
              v-for="tab in allTabs"
              :key="tab.id"
              @click="toggleTab(tab.id, $event)"
              class="flex items-center gap-1.5 text-xs px-3 py-2 rounded-t-md transition-colors border-b-2 -mb-px"
              :class="isTabActive(tab.id)
                ? 'text-white border-brand-500 bg-gray-900'
                : 'text-gray-500 border-transparent hover:text-gray-300 hover:bg-gray-800/50'"
            >
              {{ tab.label }}
              <span v-if="tab.count > 0"
                class="rounded-full px-1.5 py-0.5 text-xs leading-none"
                :class="isTabActive(tab.id) ? 'bg-brand-600 text-white' : 'bg-gray-700 text-gray-400'">
                {{ tab.count }}
              </span>
            </button>
          </div>

          <!-- Tasks -->
          <div v-show="isTabActive('tasks')" class="bg-gray-900 border border-gray-800 rounded-xl p-5">
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
                <button @click="store.toggleTask(resolvedIssueId, task.id, task.status !== 'done')"
                  class="w-4 h-4 rounded border shrink-0 flex items-center justify-center transition-colors"
                  :class="task.status === 'done' ? 'bg-brand-600 border-brand-600 text-white' : 'border-gray-600 hover:border-brand-500'">
                  <svg v-if="task.status === 'done'" class="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="3" d="M5 13l4 4L19 7" />
                  </svg>
                </button>
                <span class="text-sm flex-1" :class="task.status === 'done' ? 'line-through text-gray-500' : 'text-gray-300'">
                  {{ task.title }}
                </span>
                <button @click="store.deleteTask(resolvedIssueId, task.id)"
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
          <div v-show="isTabActive('subissues')" class="bg-gray-900 border border-gray-800 rounded-xl p-5">
            <h2 class="text-xs font-medium text-gray-500 uppercase tracking-wide mb-3">Sub-Issues</h2>
            <div v-if="store.currentIssue.subIssues?.length" class="space-y-1.5 mb-3">
              <NuxtLink v-for="sub in store.currentIssue.subIssues" :key="sub.id"
                :to="`/projects/${id}/issues/${sub.number}`"
                class="flex items-center gap-2 text-sm text-gray-300 hover:text-white group py-1 px-2 rounded-lg hover:bg-gray-800/60 transition-colors">
                <span :class="statusColor(sub.status)" class="w-2.5 h-2.5 rounded-full shrink-0"></span>
                <span class="text-xs text-gray-600 shrink-0">{{ formatIssueId(sub.number, projectsStore.currentProject) }}</span>
                <span>{{ sub.title }}</span>
              </NuxtLink>
            </div>
            <!-- Add sub-issue actions -->
            <div v-if="!creatingSubIssue && !linkingSubIssue" class="flex gap-3">
              <button @click="creatingSubIssue = true"
                class="text-xs text-gray-500 hover:text-brand-400 transition-colors flex items-center gap-1">
                <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
                </svg>
                Create sub-issue
              </button>
              <button @click="linkingSubIssue = true; subIssueSearch = ''"
                class="text-xs text-gray-500 hover:text-brand-400 transition-colors flex items-center gap-1">
                <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13.828 10.172a4 4 0 00-5.656 0l-4 4a4 4 0 105.656 5.656l1.102-1.101m-.758-4.899a4 4 0 005.656 0l4-4a4 4 0 00-5.656-5.656l-1.1 1.1" />
                </svg>
                Link existing issue
              </button>
            </div>
            <div v-else-if="creatingSubIssue" class="flex gap-2">
              <input v-model="newSubIssueTitle" @keyup.enter="createSubIssue" @keyup.escape="creatingSubIssue = false"
                type="text" placeholder="Sub-issue title..." autofocus
                class="flex-1 bg-gray-800 border border-gray-700 rounded px-2.5 py-1.5 text-xs text-white placeholder-gray-600 focus:outline-none focus:ring-1 focus:ring-brand-500" />
              <button @click="createSubIssue" :disabled="!newSubIssueTitle.trim()"
                class="text-xs bg-brand-600 hover:bg-brand-700 disabled:opacity-40 text-white px-2.5 py-1.5 rounded transition-colors">Create</button>
              <button @click="creatingSubIssue = false"
                class="text-xs bg-gray-800 hover:bg-gray-700 text-gray-300 px-2.5 py-1.5 rounded transition-colors">Cancel</button>
            </div>
            <!-- Link existing issue as sub-issue -->
            <div v-else-if="linkingSubIssue" class="space-y-2">
              <IssueSearchPicker
                v-model="subIssueSearch"
                :issues="allOrgIssues"
                :current-project-id="actualProjectId"
                placeholder="Search issue to link as sub-issue..."
                @select="linkAsSubIssue"
                @cancel="linkingSubIssue = false; subIssueSearch = ''"
              />
            </div>
          </div>

          <!-- Linked Issues -->
          <div v-show="isTabActive('linked')" class="bg-gray-900 border border-gray-800 rounded-xl p-5">
            <h2 class="text-xs font-medium text-gray-500 uppercase tracking-wide mb-3">Linked Issues</h2>
            <div v-if="store.currentLinks.length" class="space-y-1.5 mb-3">
              <div v-for="link in store.currentLinks" :key="link.id"
                class="flex items-center gap-2 group py-1 px-2 rounded-lg hover:bg-gray-800/60 transition-colors">
                <span class="text-xs text-brand-400 shrink-0 min-w-[70px]">{{ IssueLinkTypeLabels[link.linkType] }}</span>
                <NuxtLink :to="`/projects/${link.targetIssue?.projectId ?? id}/issues/${link.targetIssue?.number ?? link.targetIssueId}`"
                  class="flex items-center gap-1.5 text-sm text-gray-300 hover:text-white flex-1 min-w-0">
                  <span class="text-xs text-gray-600 shrink-0">{{ link.targetIssue?.number != null ? formatIssueId(link.targetIssue.number, projectsStore.projects.find(p => p.id === link.targetIssue?.projectId)) : '' }}</span>
                  <span class="truncate">{{ link.targetIssue?.title }}</span>
                  <span v-if="link.targetIssue?.projectId && link.targetIssue.projectId !== actualProjectId" class="text-xs text-gray-600 shrink-0 ml-1">↗ cross-project</span>
                </NuxtLink>
                <button @click="store.removeLink(resolvedIssueId, link.id)"
                  class="opacity-0 group-hover:opacity-100 text-gray-600 hover:text-red-400 transition-all">
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12" />
                  </svg>
                </button>
              </div>
            </div>
            <div v-if="!addingLink">
              <button @click="addingLink = true; linkSearch = ''"
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
              <IssueSearchPicker
                v-model="linkSearch"
                :issues="allOrgIssues"
                :current-project-id="actualProjectId"
                placeholder="Search issue to link..."
                @select="onLinkIssueSelected"
                @cancel="addingLink = false; linkTargetIssueId = ''; linkSearch = ''"
              />
            </div>
          </div>

          <!-- History -->
          <div v-show="isTabActive('history')" class="bg-gray-900 border border-gray-800 rounded-xl p-5">
            <h2 class="text-xs font-medium text-gray-500 uppercase tracking-wide mb-3">History</h2>
            <div v-if="store.currentHistory.length" class="relative">
              <div class="absolute left-2 top-0 bottom-0 w-px bg-gray-800"></div>
              <div class="space-y-3 pl-7">
                <div v-for="event in store.currentHistory" :key="event.id" class="relative">
                  <span class="absolute -left-5 top-1.5 w-2 h-2 rounded-full bg-gray-700 border border-gray-600"></span>
                  <div class="flex items-start gap-1.5 flex-wrap">
                    <span class="text-xs font-medium text-gray-300">
                      {{ event.actorUser?.username ?? event.actorAgent?.name ?? 'System' }}
                    </span>
                    <span class="text-xs text-gray-500">{{ IssueEventTypeLabels[event.eventType] }}</span>
                    <template v-if="event.oldValue && event.newValue">
                      <span class="text-xs text-gray-600 line-through">{{ event.oldValue }}</span>
                      <span class="text-xs text-gray-500">→</span>
                      <span class="text-xs text-gray-300">{{ event.newValue }}</span>
                    </template>
                    <template v-else-if="event.newValue">
                      <span class="text-xs text-brand-400 font-medium">{{ event.newValue }}</span>
                    </template>
                    <template v-else-if="event.oldValue">
                      <span class="text-xs text-gray-600 line-through">{{ event.oldValue }}</span>
                    </template>
                    <span class="text-xs text-gray-600 ml-auto shrink-0">{{ formatDate(event.createdAt) }}</span>
                  </div>
                </div>
              </div>
            </div>
            <p v-else class="text-xs text-gray-600">No history yet.</p>
          </div>

          <!-- Comments -->
          <div v-show="isTabActive('comments')" class="bg-gray-900 border border-gray-800 rounded-xl p-5">
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
                      <button @click="store.deleteComment(resolvedIssueId, comment.id)"
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
                class="w-full bg-gray-800 px-3 py-2.5 text-sm text-white placeholder-gray-600 focus:outline-none resize-none"
                @paste="e => handleImagePaste(e, md => newComment += md)" />
              <div class="flex justify-end bg-gray-800/50 px-3 py-2 border-t border-gray-700 gap-2">
                <p v-if="uploadingImage" class="text-xs text-gray-400 mr-auto self-center">Uploading image…</p>
                <p v-else-if="uploadImageError" class="text-xs text-red-400 mr-auto self-center">{{ uploadImageError }}</p>
                <!-- File attachment for comment -->
                <label class="cursor-pointer text-xs text-gray-400 hover:text-gray-200 flex items-center gap-1 mr-auto self-center" title="Attach file">
                  <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15.172 7l-6.586 6.586a2 2 0 102.828 2.828l6.414-6.586a4 4 0 00-5.656-5.656l-6.415 6.585a6 6 0 108.486 8.486L20.5 13" />
                  </svg>
                  Attach
                  <input type="file" class="hidden" @change="handleCommentFileAttach" />
                </label>
                <button @click="submitComment" :disabled="!newComment.trim()"
                  class="text-xs bg-brand-600 hover:bg-brand-700 disabled:opacity-40 text-white px-3 py-1.5 rounded transition-colors">
                  Comment
                </button>
              </div>
            </div>
          </div>

          <!-- Attachments -->
          <div v-show="isTabActive('attachments')" class="bg-gray-900 border border-gray-800 rounded-xl p-5">
            <div class="flex items-center justify-between mb-4">
              <h2 class="text-xs font-medium text-gray-500 uppercase tracking-wide">
                Attachments
                <span v-if="store.currentAttachments.length" class="ml-1 text-gray-600">{{ store.currentAttachments.length }}</span>
              </h2>
              <label class="cursor-pointer flex items-center gap-1.5 text-xs bg-gray-800 hover:bg-gray-700 text-gray-300 px-3 py-1.5 rounded-lg transition-colors">
                <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
                </svg>
                Upload File
                <input type="file" class="hidden" @change="handleFileUpload" />
              </label>
            </div>
            <p v-if="uploadingAttachment" class="text-xs text-gray-400 mb-3">Uploading…</p>
            <p v-if="attachmentError" class="text-xs text-red-400 mb-3">{{ attachmentError }}</p>
            <div v-if="store.currentAttachments.length === 0 && !uploadingAttachment" class="text-sm text-gray-600">
              No attachments yet.
            </div>
            <div v-else class="space-y-2">
              <div v-for="att in store.currentAttachments" :key="att.id"
                class="flex items-center gap-3 p-3 bg-gray-800/50 rounded-lg group">
                <!-- File icon / voice icon -->
                <div class="shrink-0">
                  <svg v-if="att.isVoiceFile" class="w-5 h-5 text-brand-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 016 0v6a3 3 0 01-3 3z" />
                  </svg>
                  <svg v-else class="w-5 h-5 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                      d="M15.172 7l-6.586 6.586a2 2 0 102.828 2.828l6.414-6.586a4 4 0 00-5.656-5.656l-6.415 6.585a6 6 0 108.486 8.486L20.5 13" />
                  </svg>
                </div>
                <div class="flex-1 min-w-0">
                  <a :href="att.fileUrl" target="_blank" rel="noopener"
                    class="text-sm text-brand-400 hover:text-brand-300 truncate block">{{ att.fileName }}</a>
                  <p class="text-xs text-gray-500">
                    {{ formatFileSize(att.fileSize) }} · {{ att.contentType }}
                    <span v-if="!att.isPublic" class="ml-1 text-yellow-600" title="Private — only visible to you">🔒 Private</span>
                    · {{ formatDate(att.createdAt) }}
                  </p>
                </div>
                <div class="flex items-center gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
                  <button v-if="att.isVoiceFile" @click="retranscribeVoice(att.id)"
                    :disabled="retranscribingId === att.id"
                    class="text-xs text-brand-400 hover:text-brand-300 disabled:opacity-40 transition-colors"
                    title="Retry transcription">
                    {{ retranscribingId === att.id ? 'Transcribing…' : '🔄 Retranscribe' }}
                  </button>
                  <button @click="store.deleteAttachment(resolvedIssueId, att.id)"
                    class="text-xs text-gray-600 hover:text-red-400 transition-colors">Delete</button>
                </div>
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
                <option value="very_high">🟠 Very High</option>
                <option value="high">🟡 High</option>
                <option value="medium">🟢 Medium</option>
                <option value="low">🔵 Low</option>
                <option value="unknown">🟣 Unknown</option>
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
                  <button @click="store.removeIssueLabel(resolvedIssueId, label.id)" class="hover:opacity-70">×</button>
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
                  <button @click="store.removeAssignee(resolvedIssueId, a.id)"
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
          <button @click="requestDelete"
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

    <!-- Delete Confirmation Dialog -->
    <div v-if="showDeleteConfirm" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-sm p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-2">Delete Issue</h2>
        <p class="text-sm text-gray-400 mb-6">Are you sure you want to delete this issue? This action cannot be undone.</p>
        <div class="flex gap-3">
          <button @click="confirmDelete"
            class="flex-1 bg-red-600 hover:bg-red-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Delete
          </button>
          <button @click="showDeleteConfirm = false"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>

    <!-- Create Issue Modal -->
    <div v-if="showCreate" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">Create Issue</h2>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Title</label>
            <input v-model="createForm.title" type="text" placeholder="Issue title"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Description</label>
            <textarea v-model="createForm.body" rows="4" placeholder="Describe the issue..."
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none"></textarea>
          </div>
        </div>
        <div class="flex gap-3 mt-6">
          <button @click="submitCreate" :disabled="!createForm.title.trim()"
            class="flex-1 bg-brand-600 hover:bg-brand-700 disabled:opacity-40 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Create Issue
          </button>
          <button @click="showCreate = false"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>

    <!-- Voice Create Modal -->
    <div v-if="showVoiceCreate" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-lg p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">Create Issue from Voice</h2>

        <!-- Recording controls -->
        <div class="flex flex-col items-center gap-4 mb-5">
          <button
            v-if="!voice.recording.value && !voice.uploading.value && !voice.transcription.value"
            @click="startVoiceRecording"
            class="w-16 h-16 rounded-full bg-brand-600 hover:bg-brand-700 flex items-center justify-center transition-colors shadow-lg">
            <svg class="w-7 h-7 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 016 0v6a3 3 0 01-3 3z" />
            </svg>
          </button>
          <p v-if="!voice.recording.value && !voice.uploading.value && !voice.transcription.value"
            class="text-sm text-gray-400">Click to start recording</p>

          <div v-if="voice.recording.value" class="flex flex-col items-center gap-3">
            <div class="relative w-16 h-16">
              <div class="absolute inset-0 rounded-full bg-red-500/20 animate-ping"></div>
              <button @click="stopVoiceRecording"
                class="relative w-16 h-16 rounded-full bg-red-600 hover:bg-red-700 flex items-center justify-center transition-colors shadow-lg">
                <svg class="w-6 h-6 text-white" fill="currentColor" viewBox="0 0 24 24">
                  <rect x="6" y="6" width="12" height="12" rx="1" />
                </svg>
              </button>
            </div>
            <p class="text-sm text-red-400 font-medium">Recording… click to stop</p>
          </div>

          <div v-if="voice.uploading.value" class="flex flex-col items-center gap-2">
            <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
            <p class="text-sm text-gray-400">Transcribing…</p>
          </div>
        </div>

        <div v-if="voice.transcription.value || voiceRecordingDone" class="space-y-3 mb-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Transcription (editable)</label>
            <textarea v-model="voice.transcription.value" rows="4" placeholder="Transcription will appear here…"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500 resize-none"></textarea>
          </div>
        </div>

        <p v-if="voice.error.value" class="text-sm text-red-400 mb-4">{{ voice.error.value }}</p>

        <div class="flex gap-3">
          <button v-if="voice.transcription.value" @click="submitVoiceCreate"
            class="flex-1 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Create Issue
          </button>
          <button @click="closeVoiceModal"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { marked } from 'marked'
import DOMPurify from 'dompurify'
import { IssueStatus, IssueType, IssueLinkType, IssueLinkTypeLabels, IssueEventTypeLabels } from '~/types'
import type { IssuePriority } from '~/types'
import { useIssuesStore } from '~/stores/issues'
import { useLabelsStore } from '~/stores/labels'
import { useAgentsStore } from '~/stores/agents'
import { useMilestonesStore } from '~/stores/milestones'
import { useProjectsStore } from '~/stores/projects'
import { formatIssueId } from '~/composables/useIssueFormat'

const route = useRoute()
const router = useRouter()
const id = route.params.id as string
// The URL parameter: may be a numeric issue number (e.g. "42") or a GUID
const issueIdParam = route.params.issueId as string
// After fetch, this is resolved to the actual issue GUID for all API operations
const resolvedIssueId = ref(issueIdParam)
const store = useIssuesStore()
const labelsStore = useLabelsStore()
const agentsStore = useAgentsStore()
const milestonesStore = useMilestonesStore()
const projectsStore = useProjectsStore()
const api = useApi()
const { uploading: uploadingImage, uploadError: uploadImageError, handlePaste: handleImagePaste } = useImageUpload()

// Resolved project GUID (falls back to URL param before issue is loaded)
const actualProjectId = computed(() => store.currentIssue?.projectId ?? id)

const showDeleteConfirm = ref(false)

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

// Issue view tabs
type IssueTab = 'tasks' | 'subissues' | 'linked' | 'history' | 'comments' | 'attachments'
const TABS_STORAGE_KEY = 'issue-view-tabs'
const DEFAULT_TABS: IssueTab[] = ['tasks', 'comments']

function loadTabsFromStorage(): IssueTab[] {
  if (!import.meta.client) return DEFAULT_TABS
  const stored = sessionStorage.getItem(TABS_STORAGE_KEY) ?? localStorage.getItem(TABS_STORAGE_KEY)
  if (stored) {
    const parsed = JSON.parse(stored) as unknown
    if (Array.isArray(parsed) && parsed.length > 0) return parsed as IssueTab[]
  }
  return DEFAULT_TABS
}

const activeTabs = ref<IssueTab[]>(loadTabsFromStorage())

function isTabActive(tab: IssueTab): boolean {
  return activeTabs.value.includes(tab)
}

function toggleTab(tab: IssueTab, event: MouseEvent): void {
  if (event.ctrlKey || event.metaKey) {
    const idx = activeTabs.value.indexOf(tab)
    if (idx >= 0) {
      // Only deselect if more than one tab is active
      if (activeTabs.value.length > 1) {
        activeTabs.value = activeTabs.value.filter(t => t !== tab)
      }
    } else {
      activeTabs.value = [...activeTabs.value, tab]
    }
  } else {
    activeTabs.value = [tab]
  }
  if (import.meta.client) {
    sessionStorage.setItem(TABS_STORAGE_KEY, JSON.stringify(activeTabs.value))
    localStorage.setItem(TABS_STORAGE_KEY, JSON.stringify(activeTabs.value))
  }
}

const allTabs = computed(() => [
  { id: 'tasks' as IssueTab, label: 'Tasks', count: store.currentTasks.length },
  { id: 'subissues' as IssueTab, label: 'Sub-Issues', count: store.currentIssue?.subIssues?.length ?? 0 },
  { id: 'linked' as IssueTab, label: 'Linked Issues', count: store.currentLinks.length },
  { id: 'history' as IssueTab, label: 'History', count: store.currentHistory.length },
  { id: 'comments' as IssueTab, label: 'Comments', count: totalCommentsCount.value },
  { id: 'attachments' as IssueTab, label: 'Attachments', count: store.currentAttachments.length },
])

// Total comments includes regular and code review comments
const totalCommentsCount = computed(() =>
  store.currentComments.length + store.currentCodeReviewComments.length
)

// Links
const addingLink = ref(false)
const linkTargetIssueId = ref('')
const linkSearch = ref('')
const linkType = ref<IssueLinkType>(IssueLinkType.LinkedTo)
const allOrgIssues = ref<Array<{ id: string; number: number; title: string; projectId: string; projectName?: string }>>([])

// Sub-issue linking
const linkingSubIssue = ref(false)
const subIssueSearch = ref('')

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
  await store.fetchIssue(id, issueIdParam)
  if (store.currentIssue) {
    // Resolve to actual GUID so all subsequent operations use the canonical ID
    resolvedIssueId.value = store.currentIssue.id
    titleEdit.value = store.currentIssue.title
    bodyEdit.value = store.currentIssue.body ?? ''
  }
  await Promise.all([
    store.fetchComments(resolvedIssueId.value),
    store.fetchCodeReviewComments(resolvedIssueId.value),
    store.fetchAttachments(resolvedIssueId.value),
    store.fetchTasks(resolvedIssueId.value),
    store.fetchLinks(resolvedIssueId.value),
    store.fetchHistory(resolvedIssueId.value),
    labelsStore.fetchLabels(actualProjectId.value),
    agentsStore.fetchAgents(),
    fetchTenantUsers(),
    milestonesStore.fetchMilestones(actualProjectId.value),
    projectsStore.fetchProject(id),
  ])
  // Fetch all issues in this org for link target selection (excluding current), with project name for cross-project display
  try {
    const currentProject = projectsStore.currentProject
    const orgId = currentProject?.orgId
    const [orgIssues, orgProjects] = await Promise.all([
      api.get<Array<{ id: string; number: number; title: string; projectId: string }>>(
        '/api/issues',
        { params: { ...(orgId ? { orgId } : { projectId: actualProjectId.value }) } }
      ),
      orgId
        ? api.get<Array<{ id: string; name: string }>>(`/api/orgs/${orgId}/projects`)
        : Promise.resolve([] as Array<{ id: string; name: string }>),
    ])
    const projectNameMap = Object.fromEntries(orgProjects.map((p: { id: string; name: string }) => [p.id, p.name]))
    allOrgIssues.value = orgIssues
      .filter((i: { id: string }) => i.id !== resolvedIssueId.value)
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
    await store.updateIssue(id, resolvedIssueId.value, { title: titleEdit.value })
  }
}

async function saveBody() {
  editingBody.value = false
  await store.updateIssue(id, resolvedIssueId.value, { body: bodyEdit.value })
}

async function updateStatus(e: Event) {
  const val = (e.target as HTMLSelectElement).value as IssueStatus
  await store.updateIssue(id, resolvedIssueId.value, { status: val })
}

async function updatePriority(e: Event) {
  const val = (e.target as HTMLSelectElement).value as IssuePriority
  await store.updateIssue(id, resolvedIssueId.value, { priority: val })
}

async function updateType(e: Event) {
  const val = (e.target as HTMLSelectElement).value as IssueType
  await store.updateIssue(id, resolvedIssueId.value, { type: val })
}

async function updateMilestone(milestoneId: string | null) {
  if (milestoneId) {
    await store.updateIssue(id, resolvedIssueId.value, { milestoneId })
  } else {
    await store.clearIssueMilestone(id, resolvedIssueId.value)
  }
}

async function onSetMilestone(e: Event) {
  const val = (e.target as HTMLSelectElement).value
  await updateMilestone(val || null)
  ;(e.target as HTMLSelectElement).value = ''
}

function requestDelete() {
  showDeleteConfirm.value = true
}

async function confirmDelete() {
  showDeleteConfirm.value = false
  await store.deleteIssue(actualProjectId.value, resolvedIssueId.value)
  router.push(`/projects/${id}/issues`)
}

async function submitComment() {
  if (!newComment.value.trim()) return
  await store.addComment(resolvedIssueId.value, newComment.value.trim())
  newComment.value = ''
}

function startEditingComment(comment: { id: string; body: string }) {
  editingCommentId.value = comment.id
  commentEdit.value = comment.body
}

async function saveComment(commentId: string) {
  if (!commentEdit.value.trim()) return
  await store.updateComment(resolvedIssueId.value, commentId, commentEdit.value.trim())
  editingCommentId.value = null
  commentEdit.value = ''
}

async function addTask() {
  if (!newTaskTitle.value.trim()) return
  await store.createTask(resolvedIssueId.value, newTaskTitle.value.trim())
  newTaskTitle.value = ''
}

async function submitAddLink() {
  if (!linkTargetIssueId.value) return
  await store.addLink(resolvedIssueId.value, linkTargetIssueId.value, linkType.value)
  addingLink.value = false
  linkTargetIssueId.value = ''
  linkSearch.value = ''
  linkType.value = IssueLinkType.LinkedTo
}

async function onLinkIssueSelected(issue: { id: string }) {
  linkTargetIssueId.value = issue.id
  await submitAddLink()
}

async function linkAsSubIssue(issue: { id: string }) {
  await store.updateIssue(id, issue.id, { parentIssueId: resolvedIssueId.value })
  linkingSubIssue.value = false
  subIssueSearch.value = ''
  await store.fetchIssue(id, resolvedIssueId.value)
}

async function createSubIssue() {  if (!newSubIssueTitle.value.trim() || !store.currentIssue) return
  await store.createIssue(actualProjectId.value, {
    title: newSubIssueTitle.value.trim(),
    status: IssueStatus.Todo,
    parentIssueId: resolvedIssueId.value,
    type: IssueType.Task,
  })
  creatingSubIssue.value = false
  newSubIssueTitle.value = ''
  await store.fetchIssue(id, resolvedIssueId.value)
}

async function onAddLabel(e: Event) {
  const sel = e.target as HTMLSelectElement
  const labelId = sel.value
  if (!labelId) return
  await store.addIssueLabel(resolvedIssueId.value, labelId)
  sel.value = ''
}

async function onAddUserAssignee(e: Event) {
  const sel = e.target as HTMLSelectElement
  const userId = sel.value
  if (!userId) return
  await store.addAssignee(resolvedIssueId.value, { userId })
  sel.value = ''
}

async function onAddAgentAssignee(e: Event) {
  const sel = e.target as HTMLSelectElement
  const agentId = sel.value
  if (!agentId) return
  await store.addAssignee(resolvedIssueId.value, { agentId })
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

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
}

// Attachment upload state
const uploadingAttachment = ref(false)
const attachmentError = ref<string | null>(null)
const retranscribingId = ref<string | null>(null)

async function handleFileUpload(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (!file) return
  uploadingAttachment.value = true
  attachmentError.value = null
  try {
    await store.addAttachment(resolvedIssueId.value, file, false, true)
  } catch (err: unknown) {
    attachmentError.value = err instanceof Error ? err.message : 'Upload failed'
  } finally {
    uploadingAttachment.value = false
    ;(e.target as HTMLInputElement).value = ''
  }
}

async function handleCommentFileAttach(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (!file) return
  try {
    const config = useRuntimeConfig()
    const baseURL = config.public.apiBase as string
    const body = new FormData()
    body.append('file', file)
    const result = await $fetch<{ url: string }>('/api/uploads/file', {
      baseURL,
      method: 'POST',
      body,
      credentials: 'include',
    })
    newComment.value += (newComment.value ? '\n' : '') + `[${file.name}](${result.url})`
  } catch (err: unknown) {
    console.error('Comment file attach failed', err)
  } finally {
    ;(e.target as HTMLInputElement).value = ''
  }
}

async function retranscribeVoice(attachmentId: string) {
  retranscribingId.value = attachmentId
  try {
    await store.retranscribeAttachment(resolvedIssueId.value, attachmentId)
  } finally {
    retranscribingId.value = null
  }
}

// New issue creation from issue detail page
const showCreate = ref(false)
const showVoiceCreate = ref(false)
const voiceRecordingDone = ref(false)
const voice = useVoiceRecorder()
const createForm = reactive({
  title: '',
  body: '',
  status: IssueStatus.Todo,
  priority: 'medium' as string,
  type: IssueType.Issue
})

async function submitCreate() {
  if (!createForm.title.trim()) return
  await store.createIssue(actualProjectId.value, {
    title: createForm.title.trim(),
    body: createForm.body || undefined,
    status: createForm.status,
    type: createForm.type,
  })
  showCreate.value = false
  createForm.title = ''
  createForm.body = ''
}

async function startVoiceRecording() {
  voiceRecordingDone.value = false
  await voice.startRecording()
}

async function stopVoiceRecording() {
  const wavBlob = voice.stopRecording()
  voiceRecordingDone.value = true
  if (wavBlob) {
    await voice.uploadRecording(wavBlob)
  }
}

async function submitVoiceCreate() {
  const title = `Voice Issue - ${new Date().toLocaleString('en-US', { month: 'short', day: 'numeric', year: 'numeric', hour: '2-digit', minute: '2-digit' })}`
  const newIssue = await store.createIssue(actualProjectId.value, {
    title,
    body: voice.transcription.value,
    status: IssueStatus.Todo,
    type: IssueType.Issue,
  })
  // Attach the voice file (private — only visible to the creator)
  if (newIssue && voice.lastWavBlob.value) {
    try {
      const audioFile = new File([voice.lastWavBlob.value], 'recording.wav', { type: 'audio/wav' })
      await store.addAttachment(newIssue.id, audioFile, true, false)
    } catch (e) {
      console.warn('Could not attach voice file to new issue', e)
    }
  }
  closeVoiceModal()
}

function closeVoiceModal() {
  voice.reset()
  voiceRecordingDone.value = false
  showVoiceCreate.value = false
}
</script>
