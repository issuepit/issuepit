<template>
  <div class="p-8">
    <!-- Loading -->
    <div v-if="store.loading && !store.currentIssue" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin"></div>
    </div>

    <template v-else-if="store.currentIssue">
      <!-- Breadcrumb + action buttons -->
      <div class="flex items-center justify-between mb-5">
        <PageBreadcrumb :items="issueBreadcrumbItems" />
        <!-- Issue action buttons -->
        <div class="flex items-center gap-2">
          <button @click="startEditTitle"
            class="flex items-center gap-2 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium px-3 py-1.5 rounded-lg transition-colors"
            title="Rename issue">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
            </svg>
          </button>
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
          <button @click="triggerSimilarIssues"
            :disabled="similarIssuesTriggeringRun"
            class="flex items-center gap-2 bg-gray-800 hover:bg-gray-700 disabled:opacity-50 text-gray-300 text-sm font-medium px-3 py-1.5 rounded-lg transition-colors"
            title="Find similar issues">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
            </svg>
            {{ similarIssuesTriggeringRun ? 'Searching…' : 'Find Similar' }}
          </button>
        </div>
      </div>

      <!-- Inline title edit -->
      <div v-if="editingTitle" class="mb-5">
        <input v-model="titleEdit" @blur="saveTitle" @keyup.enter="saveTitle" autofocus
          class="w-full bg-gray-800 border border-brand-500 rounded-lg px-3 py-2 text-base font-semibold text-white focus:outline-none focus:ring-2 focus:ring-brand-500" />
      </div>

      <div class="flex gap-6">
        <!-- Main Content -->
        <div class="flex-1 min-w-0 space-y-5">
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
                class="w-full bg-transparent text-sm text-gray-300 focus:outline-none resize-y font-mono min-h-[15rem] transition-colors"
                :class="{ 'ring-2 ring-brand-500 bg-brand-500/10': descDragOver }"
                placeholder="Describe this issue... (Markdown supported)"
                @paste="e => handleImagePaste(e, md => bodyEdit += md)"
                @dragover.prevent="descDragOver = true"
                @dragleave="descDragOver = false"
                @drop.prevent="e => { descDragOver = false; handleDropAttach(e, md => bodyEdit += md) }"></textarea>
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
                <span class="text-xs text-gray-600 shrink-0">{{ formatIssueId(sub.number, projectsStore.currentProject, sub.externalId, sub.externalSource) }}</span>
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
                  <span class="text-xs text-gray-600 shrink-0">{{ link.targetIssue?.number != null ? formatLinkedIssueId(link.targetIssue.number, link.targetIssue.projectId, link.targetIssue.externalId, link.targetIssue.externalSource) : '' }}</span>
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
                      <span class="text-xs text-gray-600 line-through">{{ resolveEventValue(event.eventType, event.oldValue) }}</span>
                      <span class="text-xs text-gray-500">→</span>
                      <span class="text-xs text-gray-300">{{ resolveEventValue(event.eventType, event.newValue) }}</span>
                    </template>
                    <template v-else-if="event.newValue">
                      <span class="text-xs text-brand-400 font-medium">{{ resolveEventValue(event.eventType, event.newValue) }}</span>
                    </template>
                    <template v-else-if="event.oldValue">
                      <span class="text-xs text-gray-600 line-through">{{ resolveEventValue(event.eventType, event.oldValue) }}</span>
                    </template>
                    <span class="text-xs text-gray-600 ml-auto shrink-0"><DateDisplay :date="event.createdAt" mode="relative" resolution="datetime" /></span>
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
                    <span class="text-xs text-gray-600"><DateDisplay :date="comment.createdAt" mode="relative" resolution="datetime" /></span>
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
            <div class="border border-gray-700 rounded-lg"
              :class="{ 'ring-2 ring-brand-500': commentDragOver }">
              <div class="relative">
                <textarea v-model="newComment" rows="3" placeholder="Leave a comment… (type @ to mention an agent or user)"
                  class="w-full bg-gray-800 rounded-t-lg px-3 py-2.5 text-sm text-white placeholder-gray-600 focus:outline-none resize-none transition-colors"
                  :class="{ 'bg-brand-500/10': commentDragOver }"
                  v-bind="commentMention.textareaBindings"
                  @paste="e => handleImagePaste(e, md => newComment += md)"
                  @dragover.prevent="commentDragOver = true"
                  @dragleave="commentDragOver = false"
                  @drop.prevent="e => { commentDragOver = false; handleDropAttach(e, md => newComment += md) }" />
                <!-- @/# Mention dropdown -->
                <div v-if="commentMention.isOpen.value && commentMention.items.value.length"
                  class="absolute left-0 bottom-full mb-1 z-50 bg-gray-900 border border-gray-700 rounded-lg shadow-xl overflow-hidden min-w-48 max-h-52 overflow-y-auto">
                  <button
                    v-for="(item, idx) in commentMention.items.value"
                    :key="item.value"
                    type="button"
                    class="w-full flex items-center gap-2 px-3 py-2 text-sm text-left transition-colors"
                    :class="idx === commentMention.activeIndex.value ? 'bg-brand-700/40 text-white' : 'text-gray-300 hover:bg-gray-700'"
                    @mousedown.prevent="commentMention.confirmSelection(item)">
                    <span class="w-5 h-5 rounded-full flex items-center justify-center text-xs shrink-0"
                      :class="item.type === 'agent' ? 'bg-brand-700 text-brand-200' : 'bg-gray-700 text-gray-300'">
                      {{ item.type === 'agent' ? '🤖' : item.type === 'user' ? '👤' : '#' }}
                    </span>
                    <span class="truncate">{{ item.label || item.value }}</span>
                  </button>
                </div>
              </div>
              <div class="flex justify-end bg-gray-800/50 rounded-b-lg px-3 py-2 border-t border-gray-700 gap-2">
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
                <!-- Branch selector: shown when comment contains an @agent mention -->
                <div v-if="commentHasAgentMention" class="flex items-center gap-1 self-center">
                  <svg class="w-3.5 h-3.5 text-gray-500 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19c-5 1.5-5-2.5-7-3m14 6v-3.87a3.37 3.37 0 0 0-.94-2.61c3.14-.35 6.44-1.54 6.44-7A5.44 5.44 0 0 0 20 4.77 5.07 5.07 0 0 0 19.91 1S18.73.65 16 2.48a13.38 13.38 0 0 0-7 0C6.27.65 5.09 1 5.09 1A5.07 5.07 0 0 0 5 4.77a5.44 5.44 0 0 0-1.5 3.78c0 5.42 3.3 6.61 6.44 7A3.37 3.37 0 0 0 9 18.13V22" />
                  </svg>
                  <input
                    v-model="commentBranch"
                    type="text"
                    list="comment-branch-list"
                    placeholder="branch (optional)"
                    class="bg-gray-800 border border-gray-700 rounded px-2 py-1 text-xs text-white placeholder-gray-600 focus:outline-none focus:ring-1 focus:ring-brand-500 font-mono w-36"
                    title="Branch the agent will start from"
                  />
                  <datalist id="comment-branch-list">
                    <option v-for="b in gitStore.branches" :key="b.name" :value="b.name" />
                  </datalist>
                </div>
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
                class="p-3 bg-gray-800/50 rounded-lg group">
                <div class="flex items-center gap-3">
                  <!-- File icon / voice icon -->
                  <div class="shrink-0">
                    <svg v-if="att.isVoiceFile || att.contentType?.startsWith('audio/')" class="w-5 h-5 text-brand-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
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
                      · <DateDisplay :date="att.createdAt" mode="auto" resolution="date" />
                    </p>
                  </div>
                  <div class="flex items-center gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
                    <button v-if="att.isVoiceFile || att.contentType?.startsWith('audio/')" @click="retranscribeVoice(att.id)"
                      :disabled="retranscribingId === att.id"
                      class="text-xs text-brand-400 hover:text-brand-300 disabled:opacity-40 transition-colors"
                      title="Retry transcription">
                      {{ retranscribingId === att.id ? 'Transcribing…' : '🔄 Retranscribe' }}
                    </button>
                    <button @click="store.updateAttachment(resolvedIssueId, att.id, { isPublic: !att.isPublic })"
                      class="text-xs text-gray-400 hover:text-gray-200 transition-colors"
                      :title="att.isPublic ? 'Make private' : 'Make public'">
                      {{ att.isPublic ? '🌐 Public' : '🔒 Private' }}
                    </button>
                    <button @click="requestDeleteAttachment(att.id, att.fileName)"
                      class="text-xs text-gray-600 hover:text-red-400 transition-colors">Delete</button>
                  </div>
                </div>
                <!-- Inline audio player for audio/voice files -->
                <div v-if="att.isVoiceFile || att.contentType?.startsWith('audio/')" class="mt-2 pl-8">
                  <audio :src="att.fileUrl" controls class="w-full h-8 accent-brand-400" preload="none" :aria-label="'Play ' + att.fileName" />
                </div>
              </div>
            </div>
          </div>

          <!-- Runs -->
          <div v-show="isTabActive('runs')" class="bg-gray-900 border border-gray-800 rounded-xl p-5">
            <h2 class="text-xs font-medium text-gray-500 uppercase tracking-wide mb-4">Runs</h2>

            <!-- Agent Sessions -->
            <div v-if="store.currentRuns && store.currentRuns.agentSessions.length">
              <h3 class="text-xs font-medium text-gray-400 uppercase tracking-wide mb-3">Agent Runs</h3>
              <div class="rounded-lg border border-gray-800 overflow-hidden mb-5">
                <table class="w-full text-sm">
                  <thead class="bg-gray-800/50">
                    <tr>
                      <th class="text-left px-3 py-2 text-gray-400 font-medium text-xs">Status</th>
                      <th class="text-left px-3 py-2 text-gray-400 font-medium text-xs">Agent</th>
                      <th class="text-left px-3 py-2 text-gray-400 font-medium text-xs">Branch</th>
                      <th class="text-left px-3 py-2 text-gray-400 font-medium text-xs">Started</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-gray-800">
                    <tr v-for="session in store.currentRuns.agentSessions" :key="session.id"
                      class="hover:bg-gray-800/30 transition-colors cursor-pointer"
                      @click="navigateTo(`/projects/${actualProjectId}/runs/agent-sessions/${session.id}`)">
                      <td class="px-3 py-2">
                        <AgentSessionStatusChip :session="session" />
                      </td>
                      <td class="px-3 py-2 text-gray-300 text-xs">{{ session.agentName }}</td>
                      <td class="px-3 py-2 text-gray-400 font-mono text-xs">{{ session.gitBranch || '—' }}</td>
                      <td class="px-3 py-2 text-gray-400 text-xs"><DateDisplay :date="session.startedAt" mode="auto" /></td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>

            <!-- CI/CD Runs (from agent sessions) -->
            <template v-if="store.currentRuns">
              <div v-if="allCiCdRuns.length">
                <h3 class="text-xs font-medium text-gray-400 uppercase tracking-wide mb-3">CI/CD Runs</h3>
                <div class="rounded-lg border border-gray-800 overflow-hidden">
                  <table class="w-full text-sm">
                    <thead class="bg-gray-800/50">
                      <tr>
                        <th class="text-left px-3 py-2 text-gray-400 font-medium text-xs">Status</th>
                        <th class="text-left px-3 py-2 text-gray-400 font-medium text-xs">Workflow</th>
                        <th class="text-left px-3 py-2 text-gray-400 font-medium text-xs">Branch</th>
                        <th class="text-left px-3 py-2 text-gray-400 font-medium text-xs">Commit</th>
                        <th class="text-left px-3 py-2 text-gray-400 font-medium text-xs">Started</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-gray-800">
                      <tr v-for="run in allCiCdRuns" :key="run.id"
                        class="hover:bg-gray-800/30 transition-colors cursor-pointer"
                        @click="navigateTo(`/projects/${run.projectId}/runs/cicd/${run.id}`)">
                        <td class="px-3 py-2">
                          <CiCdStatusChip :runs="[run]" />
                        </td>
                        <td class="px-3 py-2 text-gray-300 text-xs">{{ run.workflow || '—' }}</td>
                        <td class="px-3 py-2 text-gray-400 font-mono text-xs">{{ run.branch || '—' }}</td>
                        <td class="px-3 py-2 text-gray-400 font-mono text-xs">{{ run.commitSha?.slice(0, 7) || '—' }}</td>
                        <td class="px-3 py-2 text-gray-400 text-xs"><DateDisplay :date="run.startedAt" mode="auto" /></td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>
              <div v-if="!store.currentRuns.agentSessions.length && !allCiCdRuns.length" class="text-sm text-gray-600">
                No runs yet.
              </div>
            </template>
            <div v-else class="text-sm text-gray-600">No runs yet.</div>
          </div>

          <!-- Similar Issues -->
          <div v-show="isTabActive('similar')" class="bg-gray-900 border border-gray-800 rounded-xl p-5">
            <div class="flex items-center justify-between mb-4">
              <h2 class="text-xs font-medium text-gray-500 uppercase tracking-wide">Similar Issues</h2>
              <div class="flex items-center gap-2">
                <!-- Run status badge -->
                <span v-if="similarIssuesRunStatus"
                  :class="similarIssuesRunStatusClass"
                  class="text-xs px-2 py-0.5 rounded-full font-medium">
                  {{ similarIssuesRunStatus }}
                </span>
                <!-- View logs button -->
                <button v-if="similarIssuesRunId"
                  class="text-xs text-gray-500 hover:text-gray-300 transition-colors"
                  @click="openSimilarIssuesLogs">
                  View logs
                </button>
                <button @click="triggerSimilarIssues" :disabled="similarIssuesTriggeringRun"
                  class="text-xs text-brand-400 hover:text-brand-300 disabled:opacity-50 transition-colors">
                  {{ similarIssuesTriggeringRun ? 'Searching…' : 'Refresh' }}
                </button>
              </div>
            </div>
            <div v-if="similarIssuesLoading" class="text-sm text-gray-600">Loading…</div>
            <div v-else-if="similarIssues.length" class="space-y-3">
              <div v-for="item in similarIssues" :key="item.similarIssueId"
                class="flex items-start gap-3 p-3 bg-gray-800/50 rounded-lg hover:bg-gray-800 transition-colors">
                <div class="flex-1 min-w-0">
                  <NuxtLink :to="`/projects/${route.params.id}/issues/${item.similarIssueId}`"
                    class="text-sm text-white hover:text-brand-300 transition-colors font-medium truncate block">
                    #{{ item.number }} {{ item.title }}
                  </NuxtLink>
                  <p v-if="item.reason" class="text-xs text-gray-400 mt-1">{{ item.reason }}</p>
                </div>
                <span class="shrink-0 text-xs font-medium px-2 py-0.5 rounded-full bg-brand-900/50 text-brand-300">
                  {{ Math.round(item.score * 100) }}%
                </span>
              </div>
            </div>
            <div v-else class="text-sm text-gray-600">
              No similar issues found. Click "Find Similar" to run detection.
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
                <option value="ready_to_merge">Ready to Merge</option>
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

            <!-- Agent Protection -->
            <div>
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-2">Agent Protection</p>
              <div class="space-y-2">
                <label class="flex items-center justify-between gap-2 cursor-pointer group">
                  <span class="text-xs text-gray-400 group-hover:text-gray-300 leading-tight">Prevent agent move</span>
                  <button
                    type="button"
                    :class="[
                      'relative inline-flex h-5 w-9 shrink-0 rounded-full border-2 border-transparent transition-colors focus:outline-none focus:ring-2 focus:ring-brand-500 focus:ring-offset-2 focus:ring-offset-gray-900',
                      store.currentIssue.preventAgentMove ? 'bg-brand-600' : 'bg-gray-700'
                    ]"
                    @click="togglePreventAgentMove">
                    <span
                      :class="[
                        'pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow ring-0 transition-transform',
                        store.currentIssue.preventAgentMove ? 'translate-x-4' : 'translate-x-0'
                      ]" />
                  </button>
                </label>
                <label class="flex items-center justify-between gap-2 cursor-pointer group">
                  <span class="text-xs text-gray-400 group-hover:text-gray-300 leading-tight">Hide from agents</span>
                  <button
                    type="button"
                    :class="[
                      'relative inline-flex h-5 w-9 shrink-0 rounded-full border-2 border-transparent transition-colors focus:outline-none focus:ring-2 focus:ring-brand-500 focus:ring-offset-2 focus:ring-offset-gray-900',
                      store.currentIssue.hideFromAgents ? 'bg-brand-600' : 'bg-gray-700'
                    ]"
                    @click="toggleHideFromAgents">
                    <span
                      :class="[
                        'pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow ring-0 transition-transform',
                        store.currentIssue.hideFromAgents ? 'translate-x-4' : 'translate-x-0'
                      ]" />
                  </button>
                </label>
              </div>
            </div>

            <!-- Milestone -->
            <div>
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Milestone</p>
              <div v-if="store.currentIssue.milestoneId" class="flex items-center gap-1 mb-1.5">
                <button
                  class="text-xs text-indigo-300 bg-indigo-900/30 hover:bg-indigo-900/50 px-2 py-0.5 rounded-full flex items-center gap-1 transition-colors"
                  @click="navigateTo(`/projects/${actualProjectId}/milestones/${store.currentIssue.milestoneId}`)">
                  🏁 {{ milestonesStore.milestones.find(m => m.id === store.currentIssue!.milestoneId)?.title ?? 'Milestone' }}
                </button>
                <button @click="updateMilestone(null)" class="text-xs text-gray-500 hover:text-gray-300 hover:opacity-70">×</button>
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
              <select v-if="agentsStore.agents.length" @change="onSelectAgentForAssignment($event)"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-2.5 py-1.5 text-xs text-gray-400 focus:outline-none focus:ring-1 focus:ring-brand-500">
                <option value="">+ Assign agent</option>
                <option v-for="a in availableAgents" :key="a.id" :value="a.id">🤖 {{ a.name }}</option>
              </select>
            </div>

            <!-- Dates -->
            <div>
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1">Created</p>
              <p class="text-xs text-gray-400"><DateDisplay :date="store.currentIssue.createdAt" mode="absolute" resolution="datetime" /></p>
            </div>
            <div v-if="store.currentIssue.dueDate">
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1">Due</p>
              <p class="text-xs text-gray-400"><DateDisplay :date="store.currentIssue.dueDate" mode="absolute" resolution="date" /></p>
            </div>

            <!-- External Issue (GitHub, Jira, etc.) -->
            <div v-if="store.currentIssue.externalId || store.currentIssue.gitHubIssueUrl || store.currentIssue.gitHubIssueNumber">
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1">
                {{ store.currentIssue.externalSource?.type === 'github' || store.currentIssue.gitHubIssueNumber ? 'GitHub Issue' : store.currentIssue.externalSource ? store.currentIssue.externalSource.type.toUpperCase() + ' Issue' : 'External Issue' }}
              </p>
              <a
                v-if="store.currentIssue.gitHubIssueUrl || (store.currentIssue.externalSource?.url && store.currentIssue.externalId)"
                :href="store.currentIssue.gitHubIssueUrl ?? (store.currentIssue.externalSource!.url + '/issues/' + store.currentIssue.externalId)"
                target="_blank"
                rel="noopener noreferrer"
                class="text-xs text-brand-400 hover:text-brand-300 flex items-center gap-1"
              >
                <svg class="w-3 h-3" fill="currentColor" viewBox="0 0 24 24">
                  <path d="M12 0C5.374 0 0 5.373 0 12c0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23A11.509 11.509 0 0112 5.803c1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576C20.566 21.797 24 17.3 24 12c0-6.627-5.373-12-12-12z" />
                </svg>
                #{{ store.currentIssue.externalId ?? store.currentIssue.gitHubIssueNumber }}
              </a>
              <span v-else class="text-xs text-gray-400">#{{ store.currentIssue.externalId ?? store.currentIssue.gitHubIssueNumber }}</span>
            </div>

            <!-- Issue Branch -->
            <div v-if="store.currentIssue.gitBranch">
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1">Branch</p>
              <div class="flex items-center gap-1.5">
                <svg class="w-3 h-3 text-green-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                    d="M9 19c-5 1.5-5-2.5-7-3m14 6v-3.87a3.37 3.37 0 0 0-.94-2.61c3.14-.35 6.44-1.54 6.44-7A5.44 5.44 0 0 0 20 4.77 5.07 5.07 0 0 0 19.91 1S18.73.65 16 2.48a13.38 13.38 0 0 0-7 0C6.27.65 5.09 1 5.09 1A5.07 5.07 0 0 0 5 4.77a5.44 5.44 0 0 0-1.5 3.78c0 5.42 3.3 6.61 6.44 7A3.37 3.37 0 0 0 9 18.13V22" />
                </svg>
                <span class="text-xs font-mono text-green-300 truncate" :title="store.currentIssue.gitBranch">{{ store.currentIssue.gitBranch }}</span>
              </div>
            </div>

            <!-- Linked Branches / Commits -->
            <div v-if="store.currentGitMappings.length">
              <p class="text-xs text-gray-500 uppercase tracking-wide mb-1.5">Linked Branches</p>
              <div class="space-y-1.5">
                <div v-for="m in store.currentGitMappings" :key="m.id"
                  class="flex items-center gap-1.5 text-xs">
                  <!-- Branch mapping -->
                  <template v-if="m.source === 'BranchName'">
                    <svg class="w-3 h-3 text-green-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                        d="M10 20l4-16m4 4l4 4-4 4M6 16l-4-4 4-4" />
                    </svg>
                    <span class="font-mono text-green-300 truncate" :title="m.branchName">{{ m.branchName }}</span>
                  </template>
                  <!-- Commit mapping -->
                  <template v-else>
                    <svg class="w-3 h-3 text-yellow-400 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                        d="M8 12h.01M12 12h.01M16 12h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                    </svg>
                    <span class="font-mono text-yellow-300" :title="m.commitSha">{{ m.commitSha?.slice(0, 7) }}</span>
                  </template>
                </div>
              </div>
            </div>

            <!-- Custom Properties -->
            <template v-if="propsStore.properties.length">
              <div v-for="prop in propsStore.properties" :key="prop.id">
                <p class="text-xs text-gray-500 uppercase tracking-wide mb-1">{{ prop.name }}</p>
                <!-- Enum: dropdown of allowed values -->
                <select v-if="prop.type === ProjectPropertyType.Enum"
                  :value="getPropertyValue(prop.id)"
                  @change="onSetPropertyValue(prop.id, ($event.target as HTMLSelectElement).value)"
                  class="w-full bg-gray-800 border border-gray-700 rounded-lg px-2.5 py-1.5 text-xs text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
                  <option value="">— not set —</option>
                  <option v-for="v in parseEnumValues(prop.allowedValues)" :key="v" :value="v">{{ v }}</option>
                </select>
                <!-- Bool: yes/no/not-set -->
                <select v-else-if="prop.type === ProjectPropertyType.Bool"
                  :value="getPropertyValue(prop.id)"
                  @change="onSetPropertyValue(prop.id, ($event.target as HTMLSelectElement).value)"
                  class="w-full bg-gray-800 border border-gray-700 rounded-lg px-2.5 py-1.5 text-xs text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
                  <option value="">— not set —</option>
                  <option value="true">Yes</option>
                  <option value="false">No</option>
                </select>
                <!-- Date: ISO format YYYY-MM-DD -->
                <input v-else-if="prop.type === ProjectPropertyType.Date"
                  type="text"
                  placeholder="YYYY-MM-DD"
                  pattern="\d{4}-\d{2}-\d{2}"
                  :value="getPropertyValue(prop.id)"
                  @change="onSetPropertyValue(prop.id, ($event.target as HTMLInputElement).value)"
                  class="w-full bg-gray-800 border border-gray-700 rounded-lg px-2.5 py-1.5 text-xs text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500"
                />
                <!-- Number -->
                <input v-else-if="prop.type === ProjectPropertyType.Number"
                  type="number"
                  :value="getPropertyValue(prop.id)"
                  @change="onSetPropertyValue(prop.id, ($event.target as HTMLInputElement).value)"
                  class="w-full bg-gray-800 border border-gray-700 rounded-lg px-2.5 py-1.5 text-xs text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500"
                />
                <!-- Person: user search select -->
                <select v-else-if="prop.type === ProjectPropertyType.Person"
                  :value="getPropertyValue(prop.id)"
                  @change="onSetPropertyValue(prop.id, ($event.target as HTMLSelectElement).value)"
                  class="w-full bg-gray-800 border border-gray-700 rounded-lg px-2.5 py-1.5 text-xs text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500">
                  <option value="">— not set —</option>
                  <option v-for="u in tenantUsers" :key="u.id" :value="u.username">{{ u.username }}</option>
                </select>
                <!-- Text / Agent: plain text -->
                <input v-else
                  type="text"
                  :value="getPropertyValue(prop.id)"
                  @change="onSetPropertyValue(prop.id, ($event.target as HTMLInputElement).value)"
                  class="w-full bg-gray-800 border border-gray-700 rounded-lg px-2.5 py-1.5 text-xs text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500"
                />
              </div>
            </template>
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

    <!-- Delete Attachment Confirmation Dialog -->
    <div v-if="deleteAttachmentTarget" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-sm p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-2">Delete Attachment</h2>
        <p class="text-sm text-gray-400 mb-6">Are you sure you want to delete <span class="text-white font-medium">{{ deleteAttachmentTarget.fileName }}</span>? This action cannot be undone.</p>
        <div class="flex gap-3">
          <button @click="confirmDeleteAttachment"
            class="flex-1 bg-red-600 hover:bg-red-700 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            Delete
          </button>
          <button @click="deleteAttachmentTarget = null"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>

    <!-- Agent Assignment Modal -->
    <div v-if="assignAgentModal.agentId" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-1">Assign Agent</h2>
        <p class="text-sm text-gray-400 mb-4">
          Assign <span class="text-brand-300 font-medium">{{ assignAgentModal.agentName }}</span> to this issue.
          Optionally add a comment to give the agent a specific task.
        </p>
        <div class="mb-4">
          <label class="block text-xs font-medium text-gray-400 mb-1.5">Task / Comment <span class="text-gray-600">(optional)</span></label>
          <div class="relative">
            <textarea
              v-model="assignAgentModal.comment"
              :ref="setAssignAgentModalRef"
              rows="4"
              :placeholder="`@${assignAgentModal.agentName} `"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2.5 text-sm text-white placeholder-gray-600 focus:outline-none focus:ring-1 focus:ring-brand-500 resize-none"
              v-bind="assignModalMention.textareaBindings"
            />
            <!-- Mention dropdown for assignment modal textarea -->
            <div v-if="assignModalMention.isOpen.value && assignModalMention.items.value.length"
              class="absolute left-0 bottom-full mb-1 z-50 bg-gray-900 border border-gray-700 rounded-lg shadow-xl overflow-hidden min-w-48 max-h-52 overflow-y-auto">
              <button
                v-for="(item, idx) in assignModalMention.items.value"
                :key="item.value"
                type="button"
                class="w-full flex items-center gap-2 px-3 py-2 text-sm text-left transition-colors"
                :class="idx === assignModalMention.activeIndex.value ? 'bg-brand-700/40 text-white' : 'text-gray-300 hover:bg-gray-700'"
                @mousedown.prevent="assignModalMention.confirmSelection(item)">
                <span class="w-5 h-5 rounded-full flex items-center justify-center text-xs shrink-0"
                  :class="item.type === 'agent' ? 'bg-brand-700 text-brand-200' : 'bg-gray-700 text-gray-300'">
                  {{ item.type === 'agent' ? '🤖' : item.type === 'user' ? '👤' : '#' }}
                </span>
                <span class="truncate">{{ item.label || item.value }}</span>
              </button>
            </div>
          </div>
          <p class="text-xs text-gray-500 mt-1.5">Tip: type <code class="bg-gray-800 px-1 rounded">@agent-name</code> to trigger the agent when posting the comment.</p>
        </div>
        <!-- Branch selector -->
        <div class="mb-5">
          <div class="flex items-center justify-between mb-1.5">
            <label class="flex items-center gap-1.5 text-xs font-medium text-gray-400 cursor-pointer select-none">
              <svg class="w-3.5 h-3.5 -mt-0.5 shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19c-5 1.5-5-2.5-7-3m14 6v-3.87a3.37 3.37 0 0 0-.94-2.61c3.14-.35 6.44-1.54 6.44-7A5.44 5.44 0 0 0 20 4.77 5.07 5.07 0 0 0 19.91 1S18.73.65 16 2.48a13.38 13.38 0 0 0-7 0C6.27.65 5.09 1 5.09 1A5.07 5.07 0 0 0 5 4.77a5.44 5.44 0 0 0-1.5 3.78c0 5.42 3.3 6.61 6.44 7A3.37 3.37 0 0 0 9 18.13V22" />
              </svg>
              Branch
            </label>
            <label class="flex items-center gap-1.5 text-xs text-gray-400 cursor-pointer select-none">
              <input
                v-model="assignAgentModal.createNewBranch"
                type="checkbox"
                class="w-3.5 h-3.5 rounded accent-brand-500"
              />
              Create new branch
            </label>
          </div>
          <div v-if="assignAgentModal.createNewBranch">
            <p class="text-xs text-gray-500">A unique feature branch will be created automatically for this agent run.</p>
          </div>
          <div v-else>
            <BranchSelect
              v-model="assignAgentModal.branch"
              :branches="gitStore.branches"
              :allow-free-form="true"
              :full="true"
              placeholder="select or type a branch"
            />
            <p v-if="assignBranchIsDefault" class="text-xs text-red-400 mt-1">
              <span aria-label="Warning">⚠</span> This is a default branch. Please create a new branch or select a feature branch instead.
            </p>
            <p v-else class="text-xs text-gray-600 mt-1">Agent will start from this branch.</p>
          </div>
        </div>
        <div class="flex gap-3">
          <button @click="confirmAssignAgent"
            :disabled="!assignAgentModal.createNewBranch && assignBranchIsDefault"
            class="flex-1 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium py-2 rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed">
            Assign{{ assignAgentModal.comment.trim() ? ' &amp; Comment' : '' }}
          </button>
          <button @click="cancelAssignAgent"
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
            v-if="!voice.recording.value && !voice.uploading.value && !voiceRecordingDone"
            @click="startVoiceRecording"
            class="w-16 h-16 rounded-full bg-brand-600 hover:bg-brand-700 flex items-center justify-center transition-colors shadow-lg">
            <svg class="w-7 h-7 text-white" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 016 0v6a3 3 0 01-3 3z" />
            </svg>
          </button>
          <p v-if="!voice.recording.value && !voice.uploading.value && !voiceRecordingDone"
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
          <!-- Transcription warning (no model configured, no speech detected, or error from backend) -->
          <p v-if="voice.transcriptionWarning.value && !voice.transcription.value"
            class="text-xs text-amber-400">
            {{ voice.transcriptionWarning.value }}
          </p>
        </div>

        <p v-if="voice.error.value" class="text-sm text-red-400 mb-4">{{ voice.error.value }}</p>

        <div class="flex gap-3">
          <button v-if="voiceRecordingDone && !voice.uploading.value" @click="submitVoiceCreate"
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

    <!-- Similar Issues Logs Modal -->
    <div v-if="similarIssuesLogsModal"
      class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4"
      @click.self="similarIssuesLogsModal = null">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-2xl shadow-xl flex flex-col max-h-[80vh]">
        <div class="flex items-center justify-between px-6 py-4 border-b border-gray-800">
          <div>
            <h2 class="text-base font-bold text-white">Similar Issues Run Logs</h2>
            <p class="text-xs text-gray-500 mt-0.5">
              <span :class="similarIssuesRunStatusClass" class="px-1.5 py-0.5 rounded-full font-medium">
                {{ similarIssuesLogsModal.status }}
              </span>
              <span v-if="similarIssuesLogsModal.startedAt" class="ml-2">
                <DateDisplay :date="similarIssuesLogsModal.startedAt" mode="auto" />
              </span>
              <span v-if="similarIssuesLogsModal.summary" class="ml-2">— {{ similarIssuesLogsModal.summary }}</span>
            </p>
          </div>
          <button class="text-gray-500 hover:text-gray-300 text-xl leading-none" @click="similarIssuesLogsModal = null">&times;</button>
        </div>
        <div class="overflow-y-auto p-4 font-mono text-xs space-y-0.5">
          <div v-if="similarIssuesLogsModalLoading" class="text-gray-600 text-center py-6">Loading…</div>
          <div v-else-if="!similarIssuesLogsModal.logs?.length" class="text-gray-600 text-center py-6">No log entries.</div>
          <div v-for="log in similarIssuesLogsModal.logs" :key="log.id"
            :class="similarIssuesLogLineClass(log.level)">
            <span class="text-gray-600 mr-2">{{ formatLogTime(log.timestamp) }}</span>
            <span :class="similarIssuesLogBadgeClass(log.level)" class="mr-2 text-xs px-1 rounded">
              [{{ log.level === 1 ? 'WARN' : log.level === 2 ? 'ERR' : 'INFO' }}]
            </span>
            {{ log.message }}
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { marked } from 'marked'
import DOMPurify from 'dompurify'
import { IssueStatus, IssueType, IssueLinkType, IssueLinkTypeLabels, IssueEventTypeLabels, IssueEventType, ProjectPropertyType } from '~/types'
import type { IssuePriority } from '~/types'
import { useIssuesStore } from '~/stores/issues'
import { useLabelsStore } from '~/stores/labels'
import { useAgentsStore } from '~/stores/agents'
import { useMilestonesStore } from '~/stores/milestones'
import { useProjectsStore } from '~/stores/projects'
import { useProjectPropertiesStore } from '~/stores/projectProperties'
import { useGitStore } from '~/stores/git'
import { formatIssueId } from '~/composables/useIssueFormat'
import { useMentionDropdown } from '~/composables/useMentionDropdown'
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
const propsStore = useProjectPropertiesStore()
const gitStore = useGitStore()
const api = useApi()
const { uploading: uploadingImage, uploadError: uploadImageError, handlePaste: handleImagePaste } = useImageUpload()

// SignalR: connect to project hub to receive RunsUpdated events (live agent session refresh)
const { connection: projectConnection, connect: connectProject } = useSignalR('/hubs/project')

const descDragOver = ref(false)
const commentDragOver = ref(false)

// Resolved project GUID (falls back to URL param before issue is loaded)
const actualProjectId = computed(() => store.currentIssue?.projectId ?? id)

function formatLinkedIssueId(number: number, projectId: string | undefined, externalId?: number | null, externalSource?: import('~/types').IssueExternalSource | null): string {
  const proj = projectsStore.projects.find(p => p.id === projectId)
  return formatIssueId(number, proj, externalId, externalSource)
}

const showDeleteConfirm = ref(false)

// Custom property values: map of propertyId → value string
const issuePropertyValues = ref<Record<string, string>>({})

function getPropertyValue(propertyId: string): string {
  return issuePropertyValues.value[propertyId] ?? ''
}

function loadPropertyValues(vals: import('~/types').IssuePropertyValue[]) {
  const map: Record<string, string> = {}
  for (const v of vals) map[v.propertyId] = v.value ?? ''
  issuePropertyValues.value = map
}

async function onSetPropertyValue(propertyId: string, value: string) {
  issuePropertyValues.value[propertyId] = value
  await propsStore.setIssuePropertyValue(actualProjectId.value, resolvedIssueId.value, propertyId, value || null)
}

function parseEnumValues(allowedValues: string | null | undefined): string[] {
  if (!allowedValues) return []
  try {
    const parsed: unknown = JSON.parse(allowedValues)
    return Array.isArray(parsed) ? (parsed as string[]) : []
  } catch {
    return []
  }
}

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
const commentBranch = ref('')

// Mention items for comment textarea — show all active agents available to this tenant,
// consistent with the "Assign agent" sidebar dropdown which uses agentsStore.agents.
const mentionAgents = computed(() =>
  agentsStore.agents
    .filter(a => a.isActive)
    .map(a => ({ value: a.name, label: a.name }))
)
const mentionUsers = computed(() =>
  tenantUsers.value.map(u => ({ value: u.username, label: u.username }))
)
const hashTokens = [
  { value: 'similar', label: '#similar — attach similar issues context' },
  { value: 'runs', label: '#runs — attach recent CI/CD run context' },
]

const commentMention = useMentionDropdown({
  agents: mentionAgents,
  users: mentionUsers,
  hashTokens,
})

// Whether the current comment text mentions at least one known agent (shows branch selector)
const commentHasAgentMention = computed(() => {
  if (!newComment.value) return false
  const agentNames = mentionAgents.value.map(a => a.value.toLowerCase())
  return agentNames.some(name => newComment.value.toLowerCase().includes(`@${name}`))
})

// Lazy-load branches when a comment mentions an agent (for the branch selector)
watch(commentHasAgentMention, (hasMention) => {
  if (hasMention && !gitStore.branches.length) {
    gitStore.fetchBranches(actualProjectId.value)
  }
})

// Agent assignment modal state
const assignAgentModal = reactive<{ agentId: string | null; agentName: string; comment: string; branch: string; createNewBranch: boolean }>({
  agentId: null,
  agentName: '',
  comment: '',
  branch: '',
  createNewBranch: true,
})
const assignAgentModalRef = ref<HTMLTextAreaElement | null>(null)
function setAssignAgentModalRef(el: unknown) {
  assignAgentModalRef.value = el as HTMLTextAreaElement | null
}
const assignModalMention = useMentionDropdown({
  agents: mentionAgents,
  users: mentionUsers,
  hashTokens,
})

// Issue view tabs
type IssueTab = 'tasks' | 'subissues' | 'linked' | 'history' | 'comments' | 'attachments' | 'runs' | 'similar'
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

const issueBreadcrumbItems = computed(() => {
  const items: { label: string, to: string, icon?: string, color?: string }[] = [
    { label: 'Projects', to: '/projects', icon: 'M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10' },
    { label: projectsStore.currentProject?.name || 'Project', to: `/projects/${id}`, color: projectsStore.currentProject?.color || '#4c6ef5' },
    { label: 'Issues', to: `/projects/${id}/issues`, icon: 'M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2' },
  ]
  if (store.currentIssue?.parentIssue) {
    items.push({ label: `#${store.currentIssue.parentIssue.number} ${store.currentIssue.parentIssue.title}`, to: `/projects/${id}/issues/${store.currentIssue.parentIssue.number}`, icon: 'M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2' })
  }
  if (store.currentIssue) {
    items.push({ label: `#${store.currentIssue.number} ${store.currentIssue.title}`, to: `/projects/${id}/issues/${store.currentIssue.number}`, icon: 'M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2' })
  }
  return items
})

const allTabs = computed(() => [
  { id: 'tasks' as IssueTab, label: 'Tasks', count: store.currentTasks.length },
  { id: 'subissues' as IssueTab, label: 'Sub-Issues', count: store.currentIssue?.subIssues?.length ?? 0 },
  { id: 'linked' as IssueTab, label: 'Linked Issues', count: store.currentLinks.length },
  { id: 'history' as IssueTab, label: 'History', count: store.currentHistory.length },
  { id: 'comments' as IssueTab, label: 'Comments', count: totalCommentsCount.value },
  { id: 'attachments' as IssueTab, label: 'Attachments', count: store.currentAttachments.length },
  { id: 'runs' as IssueTab, label: 'Runs', count: runsCount.value },
  { id: 'similar' as IssueTab, label: 'Similar Issues', count: similarIssues.value.length },
])

// Total comments includes regular and code review comments
const totalCommentsCount = computed(() =>
  store.currentComments.length + store.currentCodeReviewComments.length
)

// Runs - flatten all CI/CD runs from agent sessions
const allCiCdRuns = computed(() =>
  store.currentRuns?.agentSessions.flatMap(s => s.ciCdRuns) ?? []
)
const runsCount = computed(() =>
  (store.currentRuns?.agentSessions.length ?? 0) + allCiCdRuns.value.length
)

// Similar Issues
const SIMILAR_ISSUES_POLL_ATTEMPTS = 12
const SIMILAR_ISSUES_POLL_INTERVAL_MS = 2500
const similarIssues = ref<Array<{ similarIssueId: string; number: number; title: string; score: number; reason?: string; detectedAt: string }>>([])
const similarIssuesLoading = ref(false)
const similarIssuesTriggeringRun = ref(false)
const similarIssuesRunId = ref<string | null>(null)
const similarIssuesRunStatus = ref<string | null>(null)

const similarIssuesRunStatusClass = computed(() => {
  switch (similarIssuesRunStatus.value) {
    case 'succeeded': return 'bg-green-900/40 text-green-300'
    case 'failed': return 'bg-red-900/40 text-red-300'
    case 'running': return 'bg-blue-900/40 text-blue-300'
    case 'pending': return 'bg-gray-800 text-gray-400'
    default: return 'bg-gray-800 text-gray-400'
  }
})

interface SimilarIssueRunLog { id: string; level: number; message: string; timestamp: string }
interface SimilarIssueRunDetail {
  id: string
  status: string
  summary?: string
  startedAt: string
  completedAt?: string | null
  logs?: SimilarIssueRunLog[]
}

const similarIssuesLogsModal = ref<SimilarIssueRunDetail | null>(null)
const similarIssuesLogsModalLoading = ref(false)

function similarIssuesLogLineClass(level: number): string {
  if (level === 1) return 'text-yellow-300'
  if (level === 2) return 'text-red-400'
  return 'text-gray-300'
}

function similarIssuesLogBadgeClass(level: number): string {
  if (level === 1) return 'bg-yellow-900/40 text-yellow-300'
  if (level === 2) return 'bg-red-900/40 text-red-300'
  return 'bg-gray-800 text-gray-500'
}

function formatLogTime(iso: string): string {
  return new Date(iso).toLocaleTimeString('en-GB', { hour12: false })
}

async function openSimilarIssuesLogs() {
  if (!similarIssuesRunId.value) return
  similarIssuesLogsModalLoading.value = true
  similarIssuesLogsModal.value = { id: similarIssuesRunId.value, status: similarIssuesRunStatus.value ?? 'Pending', startedAt: '' }
  try {
    const { get } = useApi()
    similarIssuesLogsModal.value = await get<SimilarIssueRunDetail>(`/api/similar-issue-runs/${similarIssuesRunId.value}`)
  } finally {
    similarIssuesLogsModalLoading.value = false
  }
}

async function fetchSimilarIssues() {
  if (!resolvedIssueId.value) return
  similarIssuesLoading.value = true
  try {
    const { get } = useApi()
    similarIssues.value = await get(`/api/issues/${resolvedIssueId.value}/similar-issues`)
  } catch {
    // ignore
  } finally {
    similarIssuesLoading.value = false
  }
}

async function pollRunStatus(runId: string) {
  const { get } = useApi()
  for (let i = 0; i < SIMILAR_ISSUES_POLL_ATTEMPTS; i++) {
    await new Promise(resolve => setTimeout(resolve, SIMILAR_ISSUES_POLL_INTERVAL_MS))
    try {
      const run = await get<SimilarIssueRunDetail>(`/api/similar-issue-runs/${runId}`)
      similarIssuesRunStatus.value = run.status
      if (run.status === 'succeeded' || run.status === 'failed') {
        await fetchSimilarIssues()
        break
      }
    } catch {
      break
    }
  }
}

async function triggerSimilarIssues() {
  similarIssuesTriggeringRun.value = true
  similarIssuesRunStatus.value = 'pending'
  try {
    const { post } = useApi()
    const response = await post<{ runId: string; projectId: string }>(`/api/issues/${resolvedIssueId.value}/similar-issues/trigger`, {})
    if (response?.runId) {
      similarIssuesRunId.value = response.runId
      similarIssuesRunStatus.value = 'running'
      await pollRunStatus(response.runId)
    } else {
      // Fallback: poll for results directly (no runId available)
      for (let attempt = 0; attempt < SIMILAR_ISSUES_POLL_ATTEMPTS; attempt++) {
        await new Promise(resolve => setTimeout(resolve, SIMILAR_ISSUES_POLL_INTERVAL_MS))
        await fetchSimilarIssues()
      }
      similarIssuesRunStatus.value = null
    }
  } finally {
    similarIssuesTriggeringRun.value = false
  }
}

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
    store.fetchIssueRuns(resolvedIssueId.value),
    store.fetchGitMappings(resolvedIssueId.value),
    labelsStore.fetchLabels(actualProjectId.value),
    agentsStore.fetchAgents(),
    fetchTenantUsers(),
    milestonesStore.fetchMilestones(actualProjectId.value),
    projectsStore.fetchProject(id),
    propsStore.fetchProperties(actualProjectId.value),
    propsStore.fetchIssuePropertyValues(actualProjectId.value, resolvedIssueId.value).then(loadPropertyValues),
    fetchSimilarIssues(),
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

  // Connect to project hub for real-time agent session updates
  await connectProject()
  if (projectConnection.value) {
    await projectConnection.value.invoke('JoinProject', actualProjectId.value).catch((e: unknown) => {
      console.warn('Failed to join project group for issue page', e)
    })
    projectConnection.value.on('RunsUpdated', () => {
      store.fetchIssueRuns(resolvedIssueId.value)
    })
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

// Default branches from project git repositories (e.g. "main", "master")
const projectDefaultBranches = computed(() =>
  new Set(gitStore.repos.map(r => r.defaultBranch).filter(Boolean))
)

// True when the manually-selected branch is one of the project's default branches
const assignBranchIsDefault = computed(() => {
  if (assignAgentModal.createNewBranch) return false
  const b = assignAgentModal.branch.trim()
  return b.length > 0 && projectDefaultBranches.value.has(b)
})

async function saveTitle() {
  editingTitle.value = false
  if (titleEdit.value && titleEdit.value !== store.currentIssue?.title) {
    await store.updateIssue(id, resolvedIssueId.value, { title: titleEdit.value })
  }
}

function startEditTitle() {
  if (store.currentIssue) {
    titleEdit.value = store.currentIssue.title
    editingTitle.value = true
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

async function togglePreventAgentMove() {
  if (!store.currentIssue) return
  await store.updateIssue(id, resolvedIssueId.value, { preventAgentMove: !store.currentIssue.preventAgentMove })
}

async function toggleHideFromAgents() {
  if (!store.currentIssue) return
  await store.updateIssue(id, resolvedIssueId.value, { hideFromAgents: !store.currentIssue.hideFromAgents })
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
  const branch = commentBranch.value.trim() || undefined
  await store.addComment(resolvedIssueId.value, newComment.value.trim(), undefined, branch)
  newComment.value = ''
  commentBranch.value = ''
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

function onSelectAgentForAssignment(e: Event) {
  const sel = e.target as HTMLSelectElement
  const agentId = sel.value
  if (!agentId) return
  const agent = agentsStore.agents.find(a => a.id === agentId)
  if (!agent) return
  sel.value = ''
  // Open modal to optionally add a comment
  assignAgentModal.agentId = agentId
  assignAgentModal.agentName = agent.name
  assignAgentModal.comment = ''
  assignAgentModal.branch = ''
  assignAgentModal.createNewBranch = true
  // Ensure branches are loaded for the branch selector
  if (!gitStore.branches.length) gitStore.fetchBranches(actualProjectId.value)
  // Ensure repos are loaded so we can detect default branches
  if (!gitStore.repos.length) gitStore.fetchRepos(actualProjectId.value)
  nextTick(() => {
    assignAgentModalRef.value?.focus()
  })
}

async function confirmAssignAgent() {
  if (!assignAgentModal.agentId) return

  let branch: string | undefined
  if (assignAgentModal.createNewBranch) {
    // Generate a unique branch name combining a timestamp and random hex to avoid collisions
    const issueNum = store.currentIssue?.number ?? 'x'
    const tsPart = Date.now().toString(36)
    const randPart = Math.random().toString(36).slice(2, 7)
    branch = `agent/issue-${issueNum}-${tsPart}${randPart}`
  } else {
    branch = assignAgentModal.branch.trim() || undefined
    // Prevent assigning on a default branch (e.g. main, master)
    if (assignBranchIsDefault.value) return
  }

  // Assign the agent to the issue (with optional branch override)
  await store.addAssignee(resolvedIssueId.value, { agentId: assignAgentModal.agentId, branch })
  // If a comment was provided, post it (the backend will detect @mention and trigger the agent)
  if (assignAgentModal.comment.trim()) {
    await store.addComment(resolvedIssueId.value, assignAgentModal.comment.trim(), undefined, branch)
  }
  assignAgentModal.agentId = null
  assignAgentModal.agentName = ''
  assignAgentModal.comment = ''
  assignAgentModal.branch = ''
  assignAgentModal.createNewBranch = true
}

function cancelAssignAgent() {
  assignAgentModal.agentId = null
  assignAgentModal.agentName = ''
  assignAgentModal.comment = ''
  assignAgentModal.branch = ''
  assignAgentModal.createNewBranch = true
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
    [IssueStatus.ReadyToMerge]: 'bg-indigo-400',
    [IssueStatus.Done]: 'bg-green-400',
    [IssueStatus.Cancelled]: 'bg-red-400'
  }
  return map[status] ?? 'bg-gray-500'
}

// Resolve raw event values to human-readable form.
// For milestone events, the value may be a GUID (old records) or a title (new records).
function resolveEventValue(eventType: IssueEventType, value: string | undefined | null): string | undefined | null {
  if (!value) return value
  if (eventType === IssueEventType.MilestoneSet || eventType === IssueEventType.MilestoneCleared) {
    const milestone = milestonesStore.milestones.find(m => m.id === value)
    return milestone?.title ?? value
  }
  return value
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
const deleteAttachmentTarget = ref<{ id: string; fileName: string } | null>(null)

function requestDeleteAttachment(id: string, fileName: string) {
  deleteAttachmentTarget.value = { id, fileName }
}

async function confirmDeleteAttachment() {
  if (!deleteAttachmentTarget.value) return
  const { id } = deleteAttachmentTarget.value
  deleteAttachmentTarget.value = null
  await store.deleteAttachment(resolvedIssueId.value, id)
}

async function handleFileUpload(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (!file) return
  uploadingAttachment.value = true
  attachmentError.value = null
  const isVoiceFile = file.type.startsWith('audio/')
  try {
    await store.addAttachment(resolvedIssueId.value, file, isVoiceFile, true)
  } catch (err: unknown) {
    attachmentError.value = err instanceof Error ? err.message : 'Upload failed'
  } finally {
    uploadingAttachment.value = false
    ;(e.target as HTMLInputElement).value = ''
  }
}

async function uploadFileTo(endpoint: string, file: File): Promise<string> {
  const config = useRuntimeConfig()
  const baseURL = config.public.apiBase as string
  const body = new FormData()
  body.append('file', file)
  const result = await $fetch<{ url: string }>(endpoint, { baseURL, method: 'POST', body, credentials: 'include' })
  return result.url
}

async function handleDropAttach(e: DragEvent, insertText: (md: string) => void) {
  const file = e.dataTransfer?.files[0]
  if (!file) return
  try {
    if (file.type.startsWith('image/')) {
      const url = await uploadFileTo('/api/uploads/image', file)
      insertText(`![${file.name}](${url})`)
    } else if (file.type.startsWith('audio/')) {
      const att = await store.addAttachment(resolvedIssueId.value, file, /* isVoiceFile */ true, /* isPublic */ true)
      if (att?.fileUrl) insertText(`[${file.name}](${att.fileUrl})`)
    } else {
      const url = await uploadFileTo('/api/uploads/file', file)
      if (url) insertText(`[${file.name}](${url})`)
    }
  } catch (err: unknown) {
    console.error('Drop file attach failed', err)
  }
}

async function handleCommentFileAttach(e: Event) {
  const file = (e.target as HTMLInputElement).files?.[0]
  if (!file) return
  try {
    let url: string
    if (file.type.startsWith('audio/')) {
      // Audio files: store as a proper IssueAttachment so retranscription is available
      const att = await store.addAttachment(resolvedIssueId.value, file, /* isVoiceFile */ true, /* isPublic */ true)
      url = att?.fileUrl ?? ''
    } else {
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
      url = result.url
    }
    if (url) {
      newComment.value += (newComment.value ? '\n' : '') + `[${file.name}](${url})`
    } else {
      console.error('Audio attachment upload returned no URL')
    }
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
  const newIssue = await store.createIssue(actualProjectId.value, {
    title: createForm.title.trim(),
    body: createForm.body || undefined,
    status: createForm.status,
    type: createForm.type,
  })
  showCreate.value = false
  createForm.title = ''
  createForm.body = ''
  if (newIssue) {
    await navigateTo(`/projects/${actualProjectId.value}/issues/${newIssue.number}`)
  }
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
  const title = `Voice Issue - ${new Date().toLocaleString('de-DE', { day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false })}`
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
  if (newIssue) {
    await navigateTo(`/projects/${actualProjectId.value}/issues/${newIssue.number}`)
  }
}

function closeVoiceModal() {
  voice.reset()
  voiceRecordingDone.value = false
  showVoiceCreate.value = false
}
</script>
