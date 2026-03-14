import { defineStore } from 'pinia'
import type { TestHistoryRun, FlakyTest, TestCaseHistoryEntry, TestRunComparison } from '~/types'

export interface TrxImportResult {
  runId: string
  id: string
  artifactName: string
  totalTests: number
  passedTests: number
  failedTests: number
  skippedTests: number
  durationMs: number
}

export const useTestHistoryStore = defineStore('testHistory', () => {
  const history = ref<TestHistoryRun[]>([])
  const branches = ref<string[]>([])
  const flakyTests = ref<FlakyTest[]>([])
  const testCaseHistory = ref<TestCaseHistoryEntry[]>([])
  const comparison = ref<TestRunComparison | null>(null)
  const searchResults = ref<TestCaseHistoryEntry[]>([])

  const loading = ref(false)
  const flakyLoading = ref(false)
  const testCaseLoading = ref(false)
  const compareLoading = ref(false)
  const searchLoading = ref(false)
  const importLoading = ref(false)
  const error = ref<string | null>(null)

  const api = useApi()

  async function fetchHistory(
    projectId: string,
    options: { branch?: string; from?: string; to?: string; take?: number } = {},
  ) {
    loading.value = true
    error.value = null
    try {
      const params = new URLSearchParams({ take: String(options.take ?? 50) })
      if (options.branch) params.set('branch', options.branch)
      if (options.from) params.set('from', options.from)
      if (options.to) params.set('to', options.to)
      history.value = await api.get<TestHistoryRun[]>(
        `/api/projects/${projectId}/test-history?${params}`,
      )
    } catch (e: unknown) {
      error.value = e instanceof Error ? e.message : 'Failed to fetch test history'
    } finally {
      loading.value = false
    }
  }

  async function fetchBranches(projectId: string) {
    try {
      branches.value = await api.get<string[]>(`/api/projects/${projectId}/test-history/branches`)
    } catch {
      branches.value = []
    }
  }

  async function fetchFlakyTests(
    projectId: string,
    options: { branch?: string; minRuns?: number; take?: number } = {},
  ) {
    flakyLoading.value = true
    try {
      const params = new URLSearchParams({
        minRuns: String(options.minRuns ?? 3),
        take: String(options.take ?? 50),
      })
      if (options.branch) params.set('branch', options.branch)
      flakyTests.value = await api.get<FlakyTest[]>(
        `/api/projects/${projectId}/test-history/flaky?${params}`,
      )
    } catch {
      flakyTests.value = []
    } finally {
      flakyLoading.value = false
    }
  }

  async function fetchTestCaseHistory(
    projectId: string,
    fullName: string,
    options: { branch?: string; take?: number } = {},
  ) {
    testCaseLoading.value = true
    try {
      const params = new URLSearchParams({
        fullName,
        take: String(options.take ?? 100),
      })
      if (options.branch) params.set('branch', options.branch)
      testCaseHistory.value = await api.get<TestCaseHistoryEntry[]>(
        `/api/projects/${projectId}/test-history/tests?${params}`,
      )
    } catch {
      testCaseHistory.value = []
    } finally {
      testCaseLoading.value = false
    }
  }

  async function compareCommits(
    projectId: string,
    baseCommit: string,
    headCommit: string,
  ) {
    compareLoading.value = true
    comparison.value = null
    try {
      const params = new URLSearchParams({ baseCommit, headCommit })
      comparison.value = await api.get<TestRunComparison>(
        `/api/projects/${projectId}/test-history/compare?${params}`,
      )
    } catch {
      comparison.value = null
    } finally {
      compareLoading.value = false
    }
  }

  async function searchTests(
    projectId: string,
    query: string,
    options: { branch?: string; outcome?: number; take?: number } = {},
  ) {
    searchLoading.value = true
    try {
      const params = new URLSearchParams({ q: query, take: String(options.take ?? 50) })
      if (options.branch) params.set('branch', options.branch)
      if (options.outcome !== undefined) params.set('outcome', String(options.outcome))
      searchResults.value = await api.get<TestCaseHistoryEntry[]>(
        `/api/projects/${projectId}/test-history/search?${params}`,
      )
    } catch {
      searchResults.value = []
    } finally {
      searchLoading.value = false
    }
  }

  async function importTrx(
    projectId: string,
    file: File,
    options: { commitSha?: string; branch?: string; workflow?: string; artifactName?: string } = {},
  ): Promise<{ success: boolean; message: string; data?: TrxImportResult }> {
    importLoading.value = true
    try {
      const form = new FormData()
      form.append('file', file)
      if (options.commitSha) form.append('commitSha', options.commitSha)
      if (options.branch) form.append('branch', options.branch)
      if (options.workflow) form.append('workflow', options.workflow)
      if (options.artifactName) form.append('artifactName', options.artifactName)
      const data = await api.post<TrxImportResult>(`/api/projects/${projectId}/test-history/import`, form)
      return { success: true, message: 'TRX file imported successfully.', data }
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : 'Failed to import TRX file'
      return { success: false, message: msg }
    } finally {
      importLoading.value = false
    }
  }

  function reset() {
    history.value = []
    branches.value = []
    flakyTests.value = []
    testCaseHistory.value = []
    comparison.value = null
    searchResults.value = []
    error.value = null
  }

  return {
    history,
    branches,
    flakyTests,
    testCaseHistory,
    comparison,
    searchResults,
    loading,
    flakyLoading,
    testCaseLoading,
    compareLoading,
    searchLoading,
    importLoading,
    error,
    fetchHistory,
    fetchBranches,
    fetchFlakyTests,
    fetchTestCaseHistory,
    compareCommits,
    searchTests,
    importTrx,
    reset,
  }
})
