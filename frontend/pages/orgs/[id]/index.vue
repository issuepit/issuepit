<template>
  <div class="p-8">
    <!-- Loading -->
    <div v-if="orgsStore.loading && !orgsStore.currentOrg" class="flex items-center justify-center py-20">
      <div class="w-8 h-8 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
    </div>

    <template v-else-if="orgsStore.currentOrg">
      <!-- Header -->
      <div class="flex items-center gap-3 mb-6">
        <NuxtLink to="/orgs" class="text-gray-500 hover:text-gray-300 transition-colors">
          <svg class="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 19l-7-7 7-7" />
          </svg>
        </NuxtLink>
        <div>
          <h1 class="text-2xl font-bold text-white">{{ orgsStore.currentOrg.name }}</h1>
          <p class="text-gray-500 text-sm font-mono">{{ orgsStore.currentOrg.slug }}</p>
        </div>
      </div>

      <!-- Tabs -->
      <div class="flex gap-1 border-b border-gray-800 mb-6">
        <button
          v-for="tab in tabs"
          :key="tab.id"
          :class="[
            'px-4 py-2 text-sm font-medium transition-colors border-b-2 -mb-px',
            activeTab === tab.id
              ? 'text-white border-brand-500'
              : 'text-gray-400 hover:text-gray-200 border-transparent'
          ]"
          @click="setTab(tab.id)"
        >
          {{ tab.label }}
        </button>
      </div>

      <!-- Teams Tab -->
      <div v-if="activeTab === 'teams'">
        <div class="flex items-center justify-between mb-4">
          <p class="text-gray-400 text-sm">{{ teamsStore.teams.length }} team{{ teamsStore.teams.length === 1 ? '' : 's' }}</p>
          <button
            class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-3 py-1.5 rounded-lg transition-colors"
            @click="openCreateTeam"
          >
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            New Team
          </button>
        </div>

        <div v-if="teamsStore.loading" class="flex items-center justify-center py-12">
          <div class="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>

        <div v-else-if="teamsStore.teams.length" class="rounded-xl border border-gray-800 overflow-hidden">
          <table class="w-full text-sm">
            <thead class="bg-gray-900">
              <tr>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Name</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Slug</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Created</th>
                <th class="px-4 py-3" />
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-800">
              <tr
                v-for="team in teamsStore.teams"
                :key="team.id"
                class="hover:bg-gray-900/50 transition-colors cursor-pointer"
                @click="navigateTo(`/orgs/${orgId}/teams/${team.id}`)"
              >
                <td class="px-4 py-3 text-white font-medium">{{ team.name }}</td>
                <td class="px-4 py-3 text-gray-400 font-mono text-xs">{{ team.slug }}</td>
                <td class="px-4 py-3 text-gray-400">{{ formatDate(team.createdAt) }}</td>
                <td class="px-4 py-3 text-right" @click.stop>
                  <div class="flex items-center justify-end gap-2">
                    <button
                      class="text-xs text-gray-400 hover:text-gray-200 px-3 py-1.5 rounded-md border border-gray-700 hover:bg-gray-800 transition-colors"
                      @click="openEditTeam(team)"
                    >
                      Edit
                    </button>
                    <button
                      class="text-xs text-red-400 hover:text-red-300 px-3 py-1.5 rounded-md border border-red-900/30 hover:bg-red-900/20 transition-colors"
                      @click="confirmDeleteTeam(team.id, team.name)"
                    >
                      Delete
                    </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-else class="flex flex-col items-center justify-center py-16 text-center">
          <div class="w-12 h-12 bg-gray-800 rounded-full flex items-center justify-center mb-3">
            <svg class="w-6 h-6 text-gray-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1.5"
                d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0z" />
            </svg>
          </div>
          <p class="text-gray-400 font-medium">No teams yet</p>
          <p class="text-gray-600 text-sm mt-1">Create your first team</p>
        </div>
      </div>

      <!-- Members Tab -->
      <div v-if="activeTab === 'members'">
        <div class="flex items-center justify-between mb-4">
          <p class="text-gray-400 text-sm">{{ orgsStore.members.length }} member{{ orgsStore.members.length === 1 ? '' : 's' }}</p>
          <button
            class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-3 py-1.5 rounded-lg transition-colors"
            @click="showAddMember = true"
          >
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            Add Member
          </button>
        </div>

        <div v-if="orgsStore.loading" class="flex items-center justify-center py-12">
          <div class="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>

        <div v-else-if="orgsStore.members.length" class="rounded-xl border border-gray-800 overflow-hidden">
          <table class="w-full text-sm">
            <thead class="bg-gray-900">
              <tr>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">User</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Email</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Role</th>
                <th class="px-4 py-3" />
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-800">
              <tr v-for="member in orgsStore.members" :key="member.userId" class="hover:bg-gray-900/50 transition-colors">
                <td class="px-4 py-3 text-white font-medium">{{ member.user?.username }}</td>
                <td class="px-4 py-3 text-gray-400 text-xs">{{ member.user?.email }}</td>
                <td class="px-4 py-3">
                  <select
                    :value="member.role"
                    class="bg-gray-800 border border-gray-700 rounded px-2 py-1 text-xs text-gray-300 focus:outline-none focus:ring-1 focus:ring-brand-500"
                    @change="updateMemberRole(member.userId, Number(($event.target as HTMLSelectElement).value))"
                  >
                    <option v-for="(label, val) in OrgRoleLabels" :key="val" :value="Number(val)">{{ label }}</option>
                  </select>
                </td>
                <td class="px-4 py-3 text-right">
                  <button
                    class="text-xs text-red-400 hover:text-red-300 px-3 py-1.5 rounded-md border border-red-900/30 hover:bg-red-900/20 transition-colors"
                    @click="confirmRemoveMember(member.userId, member.user?.username || member.userId)"
                  >
                    Remove
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-else class="flex flex-col items-center justify-center py-16 text-center">
          <p class="text-gray-400 font-medium">No members yet</p>
        </div>
      </div>

      <!-- Projects Tab -->
      <div v-if="activeTab === 'projects'">
        <div class="flex items-center justify-between mb-4">
          <p class="text-gray-400 text-sm">{{ orgsStore.orgProjects.length }} project{{ orgsStore.orgProjects.length === 1 ? '' : 's' }}</p>
        </div>

        <div v-if="orgsStore.loading" class="flex items-center justify-center py-12">
          <div class="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>

        <div v-else-if="orgsStore.orgProjects.length" class="rounded-xl border border-gray-800 overflow-hidden">
          <table class="w-full text-sm">
            <thead class="bg-gray-900">
              <tr>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Name</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Slug</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Created</th>
                <th class="px-4 py-3" />
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-800">
              <tr
                v-for="project in orgsStore.orgProjects"
                :key="project.id"
                class="hover:bg-gray-900/50 transition-colors cursor-pointer"
                @click="navigateTo(`/projects/${project.id}`)"
              >
                <td class="px-4 py-3 text-white font-medium">{{ project.name }}</td>
                <td class="px-4 py-3 text-gray-400 font-mono text-xs">{{ project.slug }}</td>
                <td class="px-4 py-3 text-gray-400">{{ formatDate(project.createdAt) }}</td>
                <td class="px-4 py-3 text-right">
                  <NuxtLink
                    :to="`/projects/${project.id}`"
                    class="text-xs text-brand-400 hover:text-brand-300 px-3 py-1.5 rounded-md border border-brand-900/30 hover:bg-brand-900/20 transition-colors"
                    @click.stop
                  >
                    View
                  </NuxtLink>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-else class="flex flex-col items-center justify-center py-16 text-center">
          <p class="text-gray-400 font-medium">No projects yet</p>
        </div>
      </div>

      <!-- Agenda Tab -->
      <div v-if="activeTab === 'agenda'">
        <div class="mb-6">
          <p class="text-gray-400 text-sm mb-1">
            The <strong class="text-gray-200">common agenda</strong> is a shared goal tracker that spans all projects in this organization.
            Agenda projects track cross-cutting initiatives — such as adding AI skills to all repos, updating CI/CD pipelines, or switching libraries org-wide.
          </p>
          <p class="text-xs text-gray-500">
            To mark a project as the common agenda, go to the project's <strong class="text-gray-400">Settings → General → Common Agenda</strong>.
          </p>
        </div>

        <div v-if="loadingAgendaIssues" class="flex items-center justify-center py-12">
          <div class="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>

        <template v-else-if="agendaProjects.length">
          <div v-for="agendaProject in agendaProjects" :key="agendaProject.id" class="mb-8">
            <div class="flex items-center gap-2 mb-3">
              <span class="text-base">🎯</span>
              <NuxtLink :to="`/projects/${agendaProject.id}`" class="text-lg font-semibold text-white hover:text-brand-300 transition-colors">
                {{ agendaProject.name }}
              </NuxtLink>
              <span class="text-xs text-gray-500 font-mono">{{ agendaProject.slug }}</span>
            </div>

            <div v-if="agendaIssuesByProject[agendaProject.id]?.length" class="rounded-xl border border-gray-800 overflow-hidden">
              <table class="w-full text-sm">
                <thead class="bg-gray-900">
                  <tr>
                    <th class="text-left px-4 py-3 text-gray-400 font-medium w-8">#</th>
                    <th class="text-left px-4 py-3 text-gray-400 font-medium">Title</th>
                    <th class="text-left px-4 py-3 text-gray-400 font-medium">Status</th>
                    <th class="text-left px-4 py-3 text-gray-400 font-medium">Priority</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-gray-800">
                  <tr
                    v-for="issue in agendaIssuesByProject[agendaProject.id]"
                    :key="issue.id"
                    class="hover:bg-gray-900/50 transition-colors cursor-pointer"
                    @click="navigateTo(`/projects/${agendaProject.id}/issues/${issue.id}`)"
                  >
                    <td class="px-4 py-3 text-gray-500 text-xs font-mono">{{ issue.number }}</td>
                    <td class="px-4 py-3 text-white">{{ issue.title }}</td>
                    <td class="px-4 py-3">
                      <span :class="issueStatusClass(issue.status)" class="text-xs px-2 py-0.5 rounded-full">{{ issue.status }}</span>
                    </td>
                    <td class="px-4 py-3 text-gray-400 text-xs">{{ issue.priority }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
            <div v-else class="text-gray-500 text-sm py-4">No issues in this agenda project yet.</div>
          </div>
        </template>

        <div v-else class="flex flex-col items-center justify-center py-16 text-center">
          <p class="text-2xl mb-3">🎯</p>
          <p class="text-gray-400 font-medium">No agenda projects yet</p>
          <p class="text-gray-500 text-sm mt-2 max-w-sm">
            Mark a project as the common agenda in its settings to track org-wide goals here.
          </p>
        </div>
      </div>
      <div v-if="activeTab === 'agents'">
        <div class="flex items-center justify-between mb-4">
          <p class="text-gray-400 text-sm">Agents available to all projects in this organization</p>
          <button
            class="flex items-center gap-2 bg-brand-600 hover:bg-brand-700 text-white text-sm font-medium px-3 py-1.5 rounded-lg transition-colors"
            @click="showLinkOrgAgent = true"
          >
            <svg class="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 4v16m8-8H4" />
            </svg>
            Link Agent
          </button>
        </div>

        <div v-if="loadingOrgAgents" class="flex items-center justify-center py-12">
          <div class="w-6 h-6 border-2 border-brand-500 border-t-transparent rounded-full animate-spin" />
        </div>

        <div v-else-if="orgAgents.length" class="rounded-xl border border-gray-800 overflow-hidden">
          <table class="w-full text-sm">
            <thead class="bg-gray-900">
              <tr>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Agent</th>
                <th class="text-left px-4 py-3 text-gray-400 font-medium">Status</th>
                <th class="px-4 py-3" />
              </tr>
            </thead>
            <tbody class="divide-y divide-gray-800">
              <tr v-for="agent in orgAgents" :key="agent.agentId" class="hover:bg-gray-900/50 transition-colors">
                <td class="px-4 py-3 text-white font-medium">{{ agent.name }}</td>
                <td class="px-4 py-3">
                  <span :class="agent.isActive ? 'text-green-400' : 'text-gray-500'" class="text-xs">
                    {{ agent.isActive ? 'Active' : 'Inactive' }}
                  </span>
                </td>
                <td class="px-4 py-3 text-right">
                  <button
                    class="text-xs text-red-400 hover:text-red-300 px-3 py-1.5 rounded-md border border-red-900/30 hover:bg-red-900/20 transition-colors"
                    @click="unlinkOrgAgent(agent.agentId)"
                  >
                    Unlink
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-else class="flex flex-col items-center justify-center py-16 text-center">
          <p class="text-gray-400 font-medium">No agents linked to this organization</p>
          <p class="text-gray-600 text-sm mt-1">Link agents to make them available to all projects</p>
        </div>
      </div>

      <!-- CI/CD Tab -->
      <div v-if="activeTab === 'ci-cd'" class="max-w-2xl space-y-6">
        <!-- Runner Image -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
          <h2 class="font-semibold text-white mb-1">Runner Image</h2>
          <p class="text-sm text-gray-500 mb-4">
            Override the Docker runner image for all projects in this organization. Leave unset to inherit the global default.
          </p>
          <CiCdImageSelector v-model="ciCdForm.actRunnerImage" />
          <p v-if="!ciCdForm.actRunnerImage" class="text-xs text-gray-500 mt-3">
            No override set — inheriting from global default.
          </p>
          <p v-else class="text-xs text-gray-500 mt-3 font-mono">{{ ciCdForm.actRunnerImage }}</p>
        </div>

        <!-- Environment & Secrets -->
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
          <h2 class="font-semibold text-white mb-1">Environment &amp; Secrets</h2>
          <p class="text-sm text-gray-500 mb-4">
            Values passed as <code class="text-gray-300 bg-gray-800 px-1 rounded">--env</code> and
            <code class="text-gray-300 bg-gray-800 px-1 rounded">--secret</code> arguments to
            <code class="text-gray-300 bg-gray-800 px-1 rounded">act</code> for all projects.
            One <code class="text-gray-300 bg-gray-800 px-1 rounded">KEY=VALUE</code> per line. Project-level settings take priority.
          </p>
          <div class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">
                Environment variables
                <span class="text-gray-500 font-normal">(--env KEY=VALUE)</span>
              </label>
              <textarea
                v-model="ciCdForm.actEnv"
                rows="4"
                placeholder="MY_VAR=my_value&#10;NODE_ENV=test"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500 resize-y"
              />
            </div>
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">
                Secrets
                <span class="text-gray-500 font-normal">(--secret KEY=VALUE)</span>
              </label>
              <textarea
                v-model="ciCdForm.actSecrets"
                rows="4"
                placeholder="MY_SECRET=value&#10;API_KEY=key"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500 resize-y"
              />
              <p class="text-xs text-gray-500 mt-1">Secret values are stored as plain text — avoid committing sensitive credentials that can be rotated.</p>
            </div>
          </div>
        </div>

        <!-- Save button -->
        <div class="flex items-center gap-4">
          <button
            :disabled="savingCiCd"
            class="px-4 py-2 bg-brand-600 hover:bg-brand-500 disabled:opacity-50 text-white text-sm font-medium rounded-lg transition-colors"
            @click="saveCiCdSettings"
          >
            {{ savingCiCd ? 'Saving…' : 'Save CI/CD Config' }}
          </button>
          <p v-if="saveCiCdError" class="text-red-400 text-sm">{{ saveCiCdError }}</p>
          <p v-if="savedCiCdOk" class="text-green-400 text-sm">Saved successfully</p>
        </div>
      </div>

      <!-- Settings Tab -->
      <div v-if="activeTab === 'settings'" class="max-w-2xl">
        <div class="bg-gray-900 border border-gray-800 rounded-xl p-6">
          <h2 class="font-semibold text-white mb-1">Runner Options</h2>
          <p class="text-sm text-gray-500 mb-4">Default CI/CD runner limits for all projects in this organization</p>
          <form class="space-y-4" @submit.prevent="saveRunnerSettings">
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-1.5">
                Max concurrent runners
                <span class="text-gray-500 font-normal">(0 = unlimited)</span>
              </label>
              <input v-model.number="runnerSettingsForm.maxConcurrentRunners" type="number" min="0"
                class="w-40 bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-sm text-white placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500" />
              <p class="text-xs text-gray-500 mt-1">Applies across all projects in this organization unless overridden per project</p>
            </div>
            <p v-if="saveRunnerSettingsError" class="text-red-400 text-sm">{{ saveRunnerSettingsError }}</p>
            <button type="submit" :disabled="savingRunnerSettings"
              class="bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium px-4 py-2 rounded-lg transition-colors">
              {{ savingRunnerSettings ? 'Saving…' : 'Save Runner Options' }}
            </button>
          </form>
        </div>
      </div>
    </template>

    <!-- Not found / Error -->
    <div v-else class="flex flex-col items-center justify-center py-20 text-center">
      <p class="text-gray-400 font-medium">{{ orgsStore.error || 'Organization not found' }}</p>
      <NuxtLink to="/orgs" class="mt-3 text-brand-400 hover:text-brand-300 text-sm">← Back to Organizations</NuxtLink>
    </div>

    <!-- Team Create/Edit Modal -->
    <div v-if="showTeamModal" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">{{ editingTeamId ? 'Edit Team' : 'New Team' }}</h2>
        <form class="space-y-4" @submit.prevent="handleTeamSubmit">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Name</label>
            <input
              v-model="teamForm.name"
              type="text"
              required
              placeholder="Engineering"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Slug</label>
            <input
              v-model="teamForm.slug"
              type="text"
              required
              placeholder="engineering"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 font-mono focus:outline-none focus:ring-2 focus:ring-brand-500"
            />
          </div>
          <div class="flex gap-3 pt-1">
            <button
              type="submit"
              :disabled="savingTeam"
              class="flex-1 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium py-2 rounded-lg transition-colors"
            >
              {{ savingTeam ? 'Saving…' : editingTeamId ? 'Update' : 'Create' }}
            </button>
            <button
              type="button"
              class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors"
              @click="closeTeamModal"
            >
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Add Org Member Modal -->
    <div v-if="showAddMember" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">Add Member</h2>
        <form class="space-y-4" @submit.prevent="handleAddMember">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Search User</label>
            <div class="relative">
              <input
                v-model="memberSearchQuery"
                type="text"
                placeholder="Search by username…"
                class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm placeholder-gray-500 focus:outline-none focus:ring-2 focus:ring-brand-500"
                @input="onMemberSearch"
              />
              <div
                v-if="memberSearchResults.length"
                class="absolute z-10 mt-1 w-full bg-gray-800 border border-gray-700 rounded-lg shadow-xl overflow-hidden"
              >
                <button
                  v-for="user in memberSearchResults"
                  :key="user.id"
                  type="button"
                  class="w-full text-left px-3 py-2 hover:bg-gray-700 transition-colors"
                  @click="selectMemberUser(user)"
                >
                  <span class="text-white text-sm font-medium">{{ user.username }}</span>
                  <span class="text-gray-400 text-xs ml-2">{{ user.email }}</span>
                </button>
              </div>
            </div>
            <p v-if="addMemberForm.userId" class="text-xs text-brand-400 mt-1">
              Selected: {{ memberSearchQuery }}
            </p>
          </div>
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Role</label>
            <select
              v-model="addMemberForm.role"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
            >
              <option v-for="(label, val) in OrgRoleLabels" :key="val" :value="Number(val)">{{ label }}</option>
            </select>
          </div>
          <div class="flex gap-3 pt-1">
            <button
              type="submit"
              :disabled="savingMember || !addMemberForm.userId"
              class="flex-1 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium py-2 rounded-lg transition-colors"
            >
              {{ savingMember ? 'Adding…' : 'Add Member' }}
            </button>
            <button
              type="button"
              class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors"
              @click="closeAddMember"
            >
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>

    <!-- Error toast for secondary operations (teams, members) -->
    <ToastError v-if="orgsStore.currentOrg" :error="orgsStore.error || teamsStore.error" />

    <!-- Link Org Agent Modal -->
    <div v-if="showLinkOrgAgent" class="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50">
      <div class="bg-gray-900 border border-gray-700 rounded-xl w-full max-w-md p-6 shadow-xl">
        <h2 class="text-lg font-bold text-white mb-5">Link Agent to Organization</h2>
        <p class="text-sm text-gray-400 mb-4">This agent will be available to all projects in this organization.</p>
        <div class="space-y-4">
          <div>
            <label class="block text-sm font-medium text-gray-300 mb-1.5">Agent</label>
            <select v-model="selectedOrgAgentId"
              class="w-full bg-gray-800 border border-gray-700 rounded-lg px-3 py-2 text-white text-sm focus:outline-none focus:ring-2 focus:ring-brand-500">
              <option value="" disabled>Select agent…</option>
              <option v-for="agent in availableOrgAgents" :key="agent.id" :value="agent.id">{{ agent.name }}</option>
            </select>
          </div>
        </div>
        <div class="flex gap-3 mt-6">
          <button :disabled="!selectedOrgAgentId || linkingOrgAgent" @click="linkOrgAgent"
            class="flex-1 bg-brand-600 hover:bg-brand-700 disabled:opacity-50 text-white text-sm font-medium py-2 rounded-lg transition-colors">
            {{ linkingOrgAgent ? 'Linking…' : 'Link Agent' }}
          </button>
          <button @click="showLinkOrgAgent = false; selectedOrgAgentId = ''"
            class="flex-1 bg-gray-800 hover:bg-gray-700 text-gray-300 text-sm font-medium py-2 rounded-lg transition-colors">
            Cancel
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useOrgsStore } from '~/stores/orgs'
import { useTeamsStore } from '~/stores/teams'
import { useAgentsStore } from '~/stores/agents'
import { OrgRole, OrgRoleLabels } from '~/types'
import type { Team, User, AgentOrg, Project, Issue } from '~/types'

const route = useRoute()
const router = useRouter()
const orgId = route.params.id as string

const orgsStore = useOrgsStore()
const teamsStore = useTeamsStore()
const agentsStore = useAgentsStore()

const api = useApi()

const tabs = [
  { id: 'teams', label: 'Teams' },
  { id: 'members', label: 'Members' },
  { id: 'projects', label: 'Projects' },
  { id: 'agenda', label: '🎯 Agenda' },
  { id: 'agents', label: 'Agents' },
  { id: 'ci-cd', label: 'CI/CD' },
  { id: 'settings', label: 'Settings' },
]
const validTabIds = tabs.map(t => t.id)
const activeTab = ref((validTabIds.includes(route.query.tab as string) ? route.query.tab : 'teams') as string)

function setTab(id: string) {
  activeTab.value = id
  router.replace({ query: { ...route.query, tab: id } })
}

// --- Runner Settings ---
const runnerSettingsForm = reactive({ maxConcurrentRunners: 0 })
const savingRunnerSettings = ref(false)
const saveRunnerSettingsError = ref<string | null>(null)

// --- CI/CD Settings ---
const ciCdForm = reactive({ actRunnerImage: null as string | null, actEnv: '', actSecrets: '' })
const savingCiCd = ref(false)
const saveCiCdError = ref<string | null>(null)
const savedCiCdOk = ref(false)

// --- Org Agents ---
const loadingOrgAgents = ref(false)
const orgAgents = ref<AgentOrg[]>([])
const showLinkOrgAgent = ref(false)
const selectedOrgAgentId = ref('')
const linkingOrgAgent = ref(false)

const availableOrgAgents = computed(() => {
  const linked = new Set(orgAgents.value.map(a => a.agentId))
  return agentsStore.agents.filter(a => !linked.has(a.id))
})

async function fetchOrgAgents() {
  loadingOrgAgents.value = true
  try {
    orgAgents.value = await agentsStore.fetchOrgAgents(orgId)
  } finally {
    loadingOrgAgents.value = false
  }
}

async function linkOrgAgent() {
  if (!selectedOrgAgentId.value) return
  linkingOrgAgent.value = true
  try {
    await agentsStore.linkAgentToOrg(orgId, selectedOrgAgentId.value)
    selectedOrgAgentId.value = ''
    showLinkOrgAgent.value = false
    await fetchOrgAgents()
  } catch {
    // silently ignore
  } finally {
    linkingOrgAgent.value = false
  }
}

async function unlinkOrgAgent(agentId: string) {
  try {
    await agentsStore.unlinkAgentFromOrg(orgId, agentId)
    await fetchOrgAgents()
  } catch {
    // silently ignore
  }
}

// --- Agenda ---
const loadingAgendaIssues = ref(false)
const agendaProjects = computed(() => (orgsStore.orgProjects ?? []).filter((p: Project) => p.isAgenda))
const agendaIssues = ref<Issue[]>([])
const agendaIssuesByProject = computed(() => {
  const map: Record<string, Issue[]> = {}
  for (const issue of agendaIssues.value) {
    if (!map[issue.projectId]) map[issue.projectId] = []
    map[issue.projectId].push(issue)
  }
  return map
})

function issueStatusClass(status: string) {
  const map: Record<string, string> = {
    backlog: 'bg-gray-800 text-gray-400',
    todo: 'bg-blue-900/60 text-blue-300',
    in_progress: 'bg-yellow-900/60 text-yellow-300',
    in_review: 'bg-purple-900/60 text-purple-300',
    done: 'bg-green-900/60 text-green-300',
    cancelled: 'bg-red-900/60 text-red-400',
  }
  return map[status] ?? 'bg-gray-800 text-gray-400'
}

async function fetchAgendaIssues() {
  loadingAgendaIssues.value = true
  try {
    const issueArrays = await Promise.all(
      agendaProjects.value.map((p: Project) =>
        api.get<Issue[]>('/api/issues', { params: { projectId: p.id } })
      )
    )
    agendaIssues.value = issueArrays.flat()
  } catch {
    // silently ignore
  } finally {
    loadingAgendaIssues.value = false
  }
}

watch(activeTab, async (tab) => {
  if (tab === 'agenda') {
    await orgsStore.fetchOrgProjects(orgId)
    await fetchAgendaIssues()
  }
})

onMounted(async () => {
  await Promise.all([
    orgsStore.fetchOrg(orgId),
    teamsStore.fetchTeams(orgId),
    orgsStore.fetchMembers(orgId),
    orgsStore.fetchOrgProjects(orgId),
    agentsStore.fetchAgents(),
  ])
  if (orgsStore.currentOrg) {
    runnerSettingsForm.maxConcurrentRunners = orgsStore.currentOrg.maxConcurrentRunners ?? 0
    ciCdForm.actRunnerImage = orgsStore.currentOrg.actRunnerImage ?? null
    ciCdForm.actEnv = orgsStore.currentOrg.actEnv || ''
    ciCdForm.actSecrets = orgsStore.currentOrg.actSecrets || ''
  }
})

async function saveRunnerSettings() {
  if (!orgsStore.currentOrg) return
  savingRunnerSettings.value = true
  saveRunnerSettingsError.value = null
  try {
    await orgsStore.updateOrg(orgId, {
      name: orgsStore.currentOrg.name,
      slug: orgsStore.currentOrg.slug,
      maxConcurrentRunners: runnerSettingsForm.maxConcurrentRunners,
    })
  } catch (e: unknown) {
    saveRunnerSettingsError.value = e instanceof Error ? e.message : 'Failed to save'
  } finally {
    savingRunnerSettings.value = false
  }
}

async function saveCiCdSettings() {
  if (!orgsStore.currentOrg) return
  savingCiCd.value = true
  saveCiCdError.value = null
  savedCiCdOk.value = false
  try {
    await orgsStore.updateOrg(orgId, {
      name: orgsStore.currentOrg.name,
      slug: orgsStore.currentOrg.slug,
      maxConcurrentRunners: orgsStore.currentOrg.maxConcurrentRunners,
      actRunnerImage: ciCdForm.actRunnerImage,
      actEnv: ciCdForm.actEnv || null,
      actSecrets: ciCdForm.actSecrets || null,
    })
    savedCiCdOk.value = true
    setTimeout(() => { savedCiCdOk.value = false }, 3000)
  } catch (e: unknown) {
    saveCiCdError.value = e instanceof Error ? e.message : 'Failed to save'
  } finally {
    savingCiCd.value = false
  }
}

watch(activeTab, async (tab) => {
  if (tab === 'members') await orgsStore.fetchMembers(orgId)
  if (tab === 'teams') await teamsStore.fetchTeams(orgId)
  if (tab === 'projects') await orgsStore.fetchOrgProjects(orgId)
  if (tab === 'agents') await fetchOrgAgents()
})

// --- Teams ---
const showTeamModal = ref(false)
const editingTeamId = ref<string | null>(null)
const savingTeam = ref(false)
const teamForm = reactive({ name: '', slug: '' })

watch(() => teamForm.name, (val) => {
  if (!editingTeamId.value) {
    teamForm.slug = val.toLowerCase().replace(/\s+/g, '-').replace(/[^a-z0-9-]/g, '')
  }
})

function openCreateTeam() {
  editingTeamId.value = null
  Object.assign(teamForm, { name: '', slug: '' })
  showTeamModal.value = true
}

function openEditTeam(team: Team) {
  editingTeamId.value = team.id
  Object.assign(teamForm, { name: team.name, slug: team.slug })
  showTeamModal.value = true
}

function closeTeamModal() {
  showTeamModal.value = false
  editingTeamId.value = null
}

async function handleTeamSubmit() {
  if (!teamForm.name || !teamForm.slug) return
  savingTeam.value = true
  try {
    if (editingTeamId.value) {
      await teamsStore.updateTeam(orgId, editingTeamId.value, { name: teamForm.name, slug: teamForm.slug })
    } else {
      await teamsStore.createTeam(orgId, { name: teamForm.name, slug: teamForm.slug })
    }
    closeTeamModal()
  } finally {
    savingTeam.value = false
  }
}

function confirmDeleteTeam(id: string, name: string) {
  if (confirm(`Delete team "${name}"? This cannot be undone.`)) {
    teamsStore.deleteTeam(orgId, id)
  }
}

// --- Org Members ---
const showAddMember = ref(false)
const savingMember = ref(false)
const addMemberForm = reactive({ userId: '', role: OrgRole.Member })
const memberSearchQuery = ref('')
const memberSearchResults = ref<User[]>([])

async function onMemberSearch() {
  addMemberForm.userId = ''
  if (!memberSearchQuery.value.trim()) {
    memberSearchResults.value = []
    return
  }
  try {
    const data = await api.get<User[]>(`/api/users/search?q=${encodeURIComponent(memberSearchQuery.value)}`)
    memberSearchResults.value = data
  } catch {
    memberSearchResults.value = []
  }
}

function selectMemberUser(user: User) {
  addMemberForm.userId = user.id
  memberSearchQuery.value = user.username
  memberSearchResults.value = []
}

function closeAddMember() {
  showAddMember.value = false
  memberSearchQuery.value = ''
  memberSearchResults.value = []
  Object.assign(addMemberForm, { userId: '', role: OrgRole.Member })
}

async function handleAddMember() {
  if (!addMemberForm.userId) return
  savingMember.value = true
  try {
    await orgsStore.addMember(orgId, addMemberForm.userId, addMemberForm.role)
    closeAddMember()
  } finally {
    savingMember.value = false
  }
}

async function updateMemberRole(userId: string, role: OrgRole) {
  await orgsStore.updateMember(orgId, userId, role)
}

function confirmRemoveMember(userId: string, name: string) {
  if (confirm(`Remove "${name}" from the organization?`)) {
    orgsStore.removeMember(orgId, userId)
  }
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}
</script>
