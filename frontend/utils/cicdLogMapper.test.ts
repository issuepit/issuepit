import { describe, it, expect } from 'vitest'
import { buildGraphJobIndexes, resolveLogJobId, matrixLabel } from '../utils/cicdLogMapper'
import type { WorkflowJobNode } from '../types'

// ── helpers ────────────────────────────────────────────────────────────────────

function job(id: string, name: string, workflowFile?: string, callerWorkflowFile?: string): WorkflowJobNode {
  return { id, name, needs: [], runsOn: undefined, workflowFile, callerWorkflowFile }
}

function resolve(logId: string, jobs: WorkflowJobNode[]): string {
  return resolveLogJobId(logId, buildGraphJobIndexes(jobs))
}

// ── resolveLogJobId ────────────────────────────────────────────────────────────

describe('resolveLogJobId', () => {
  describe('single-file graph (no workflow prefix)', () => {
    const jobs = [
      job('build', 'Build'),
      job('test', 'Run Tests'),
      job('deploy', 'Deploy'),
    ]

    it('matches by exact YAML key id', () => {
      expect(resolve('build', jobs)).toBe('build')
    })

    it('matches by display name', () => {
      expect(resolve('Run Tests', jobs)).toBe('test')
    })

    it('is case-insensitive', () => {
      expect(resolve('BUILD', jobs)).toBe('build')
      expect(resolve('run tests', jobs)).toBe('test')
    })

    it('returns original logId when no match', () => {
      expect(resolve('nonexistent', jobs)).toBe('nonexistent')
    })
  })

  describe('multi-file graph (prefixed IDs)', () => {
    const jobs = [
      job('backend/build', 'Backend / Build', 'backend.yml', 'ci.yml'),
      job('backend/check-migrations', 'Backend / Check Pending Migrations', 'backend.yml', 'ci.yml'),
      job('pages/build', 'Build', 'pages.yml'),
      job('pages/deploy', 'Deploy', 'pages.yml'),
      job('docker/push', 'Build & Push', 'docker.yml'),
    ]

    it('matches by prefixed YAML key id', () => {
      expect(resolve('backend/build', jobs)).toBe('backend/build')
    })

    it('matches by compound display name with spaces around slash', () => {
      expect(resolve('Backend / Build', jobs)).toBe('backend/build')
    })

    it('matches by compound display name without spaces (act format)', () => {
      expect(resolve('Backend/Build', jobs)).toBe('backend/build')
    })

    it('matches pages/build by its plain name "Build"', () => {
      expect(resolve('Build', jobs)).toBe('pages/build')
    })

    it('does not confuse pages/build with backend/build via bare last segment', () => {
      // "Build" (plain name) maps to pages/build, but "Backend / Build" maps to backend/build
      expect(resolve('Build', jobs)).toBe('pages/build')
      expect(resolve('Backend / Build', jobs)).toBe('backend/build')
    })

    it('matches docker/push by act display name', () => {
      expect(resolve('Build & Push', jobs)).toBe('docker/push')
    })
  })

  describe('3-segment path: Backend/Backend CI/Build', () => {
    // pages.yml has a plain "Build" job; backend.yml's Build job is called via ci.yml
    // and gets the compound name "Backend / Build" after substitution.
    const jobs = [
      job('backend/build', 'Backend / Build', 'backend.yml', 'ci.yml'),
      job('pages/build', 'Build', 'pages.yml'),
      job('pages/deploy', 'Deploy', 'pages.yml'),
    ]

    it('resolves "Deploy GitHub Pages/Build" to pages/build', () => {
      // act emits <workflow-name>/<job-name> for pages.yml's Build job
      expect(resolve('Deploy GitHub Pages/Build', jobs)).toBe('pages/build')
    })

    it('resolves "Backend/Backend CI/Build" to backend/build (NOT pages/build)', () => {
      // act emits <caller-name>/<called-workflow-name>/<job-name>
      // "Backend" is the first segment and matches the "backend" workflow file stem
      expect(resolve('Backend/Backend CI/Build', jobs)).toBe('backend/build')
    })

    it('resolves "CI/Backend CI/Build" to backend/build', () => {
      // "CI" = caller workflow name (ci.yml), first segment matches callerWorkflowFile stem
      // Requires callerWorkflowFile-based indexing (callerWorkflowFile="ci.yml" → stem "ci")
      const jobsWithCaller = [
        job('backend/build', 'Backend / Build', 'backend.yml', 'ci.yml'),
        job('pages/build', 'Build', 'pages.yml'),
        job('pages/deploy', 'Deploy', 'pages.yml'),
      ]
      expect(resolve('CI/Backend CI/Build', jobsWithCaller)).toBe('backend/build')
    })

    it('resolves "Backend/Backend CI/Check Pending Migrations" correctly', () => {
      const jobsWithMigrations = [
        ...jobs,
        job('backend/check-migrations', 'Backend / Check Pending Migrations', 'backend.yml', 'ci.yml'),
      ]
      expect(resolve('Backend/Backend CI/Check Pending Migrations', jobsWithMigrations)).toBe('backend/check-migrations')
    })
  })

  describe('matrix job suffixes', () => {
    const jobs = [
      job('docker/push', 'Build & Push', 'docker.yml'),
      job('backend/build', 'Backend / Build', 'backend.yml'),
    ]

    it('strips "-N" matrix suffix', () => {
      expect(resolve('Build & Push-1', jobs)).toBe('docker/push')
      expect(resolve('Build & Push-2', jobs)).toBe('docker/push')
    })

    it('strips "(value)" matrix suffix', () => {
      expect(resolve('Build & Push (ubuntu-latest)', jobs)).toBe('docker/push')
    })

    it('handles compound path with matrix suffix', () => {
      expect(resolve('Docker Build & Push/Build & Push-2', jobs)).toBe('docker/push')
    })
  })

  describe('ambiguous last-segment suppression', () => {
    // Two different compound-name nodes that both end in "build"
    const jobs = [
      job('backend/build', 'Backend / Build', 'backend.yml'),
      job('frontend/build', 'Frontend / Build', 'frontend.yml'),
    ]

    it('does not map bare "build" when multiple compound nodes share that last segment', () => {
      // "build" is ambiguous — neither backend/build nor frontend/build should be returned
      const result = resolve('build', jobs)
      expect(result).toBe('build') // returns original log ID
    })

    it('still matches by full compound name', () => {
      expect(resolve('Backend / Build', jobs)).toBe('backend/build')
      expect(resolve('Frontend / Build', jobs)).toBe('frontend/build')
    })
  })

  describe('Windows path normalisation', () => {
    const jobs = [job('backend/build', 'Backend / Build', 'backend.yml')]

    it('normalises backslashes to forward slashes', () => {
      expect(resolve('backend\\build', jobs)).toBe('backend/build')
    })
  })

  describe('matrix template expression names (${{ matrix.xxx }})', () => {
    // Jobs with unresolved template expressions in their names (as they appear in the YAML graph).
    // act resolves these at runtime and emits log IDs with the actual values, e.g. "CI/Build (version 1)-1".
    const jobs = [
      job('ci/build', 'Build (version ${{ matrix.version }})', 'ci.yml'),
    ]
    const jobsNested = [
      job('reusable/build', 'Build (version ${{ matrix.version }})', 'reusable.yml', 'ci.yml'),
    ]

    it('resolves matrix instance logs for a job with template name in workflowFile', () => {
      expect(resolve('CI/Build (version 1)-1', jobs)).toBe('ci/build')
      expect(resolve('CI/Build (version 2)-2', jobs)).toBe('ci/build')
    })

    it('resolves matrix instance logs for a nested job with template name and callerWorkflowFile', () => {
      expect(resolve('CI/Build (version 1)-1', jobsNested)).toBe('reusable/build')
      expect(resolve('CI/Build (version 2)-2', jobsNested)).toBe('reusable/build')
    })
  })
})

// ── matrixLabel ────────────────────────────────────────────────────────────────

describe('matrixLabel', () => {
  it('extracts "Deploy GitHub Pages" from "Deploy GitHub Pages/Build"', () => {
    expect(matrixLabel('Deploy GitHub Pages/Build', 'Build')).toBe('Deploy GitHub Pages')
  })

  it('extracts last prefix segment "Backend CI" from "Backend/Backend CI/Build"', () => {
    expect(matrixLabel('Backend/Backend CI/Build', 'Build')).toBe('Backend CI')
  })

  it('extracts "Backend CI-2" from "Backend/Backend CI/Build-2"', () => {
    expect(matrixLabel('Backend/Backend CI/Build-2', 'Build')).toBe('Backend CI-2')
  })

  it('returns numeric suffix for simple matrix "-N" rawIds', () => {
    // "Build & Push-1" with job name "Build & Push" → suffix "1" after stripping
    // (This falls into the old fallback path since there's no "/" before the leaf name)
    const label = matrixLabel('Build & Push-1', 'Build & Push')
    expect(label).toBe('1')
  })

  it('handles compound job display names', () => {
    // "Docker Build & Push/Build & Push-2" → prefix "Docker Build & Push", suffix "-2"
    // prefix last segment = "Docker Build & Push"
    expect(matrixLabel('Docker Build & Push/Build & Push-2', 'Build & Push')).toBe('Docker Build & Push-2')
  })

  it('returns the prefix segment when no matrix suffix', () => {
    expect(matrixLabel('SomeCaller/Build', 'Build')).toBe('SomeCaller')
  })
})
