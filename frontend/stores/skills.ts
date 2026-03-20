import { defineStore } from 'pinia'
import type { Skill } from '~/types'

export const useSkillsStore = defineStore('skills', () => {
  const skills = ref<Skill[]>([])
  const currentSkill = ref<Skill | null>(null)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchSkills() {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<Skill[]>('/api/skills')
      skills.value = data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch skills'
    } finally {
      loading.value = false
    }
  }

  async function fetchSkill(id: string) {
    loading.value = true
    error.value = null
    try {
      const data = await api.get<Skill>(`/api/skills/${id}`)
      currentSkill.value = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch skill'
    } finally {
      loading.value = false
    }
  }

  async function createSkill(payload: Partial<Skill> & { gitAuthToken?: string }) {
    loading.value = true
    error.value = null
    try {
      const data = await api.post<Skill>('/api/skills', payload)
      skills.value.push(data)
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to create skill'
    } finally {
      loading.value = false
    }
  }

  async function updateSkill(id: string, payload: Partial<Skill> & { gitAuthToken?: string }) {
    loading.value = true
    error.value = null
    try {
      const data = await api.put<Skill>(`/api/skills/${id}`, payload)
      const idx = skills.value.findIndex(s => s.id === id)
      if (idx !== -1) skills.value[idx] = data
      if (currentSkill.value?.id === id) currentSkill.value = data
      return data
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to update skill'
    } finally {
      loading.value = false
    }
  }

  async function deleteSkill(id: string) {
    loading.value = true
    error.value = null
    try {
      await api.del(`/api/skills/${id}`)
      skills.value = skills.value.filter(s => s.id !== id)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to delete skill'
    } finally {
      loading.value = false
    }
  }

  // --- Agent links ---

  async function linkAgent(skillId: string, agentId: string) {
    error.value = null
    try {
      await api.post(`/api/skills/${skillId}/agents/${agentId}`, {})
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to link skill to agent'
      throw e
    }
  }

  async function unlinkAgent(skillId: string, agentId: string) {
    error.value = null
    try {
      await api.del(`/api/skills/${skillId}/agents/${agentId}`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to unlink skill from agent'
      throw e
    }
  }

  // --- Project links ---

  async function linkProject(skillId: string, projectId: string) {
    error.value = null
    try {
      await api.post(`/api/skills/${skillId}/projects/${projectId}`, {})
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to link skill to project'
      throw e
    }
  }

  async function unlinkProject(skillId: string, projectId: string) {
    error.value = null
    try {
      await api.del(`/api/skills/${skillId}/projects/${projectId}`)
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to unlink skill from project'
      throw e
    }
  }

  return {
    skills,
    currentSkill,
    loading,
    error,
    fetchSkills,
    fetchSkill,
    createSkill,
    updateSkill,
    deleteSkill,
    linkAgent,
    unlinkAgent,
    linkProject,
    unlinkProject,
  }
})
