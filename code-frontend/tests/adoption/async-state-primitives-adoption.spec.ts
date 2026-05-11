import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const currentDirectory = dirname(fileURLToPath(import.meta.url))

function readSource(relativePath: string) {
  return readFileSync(resolve(currentDirectory, '..', '..', relativePath), 'utf8')
}

describe('async state primitive adoption', () => {
  it('uses BaseErrorState for page-level async error UI in App', () => {
    const source = readSource('src/App.vue')

    expect(source).toContain("import BaseErrorState from './components/bases/BaseErrorState.vue'")
    expect(source).toContain('<BaseErrorState v-if="pageError" scope="page" :message="pageError" />')
  })

  it('uses BaseLoadingState for project list loading UI in ProjectSidebar', () => {
    const source = readSource('src/components/ProjectSidebar.vue')

    expect(source).toContain("import BaseLoadingState from './bases/BaseLoadingState.vue'")
    expect(source).toContain('<BaseLoadingState v-if="isLoadingProjects" message="正在載入專案列表..." />')
  })

  it('uses BaseLoadingState for board loading UI in ProjectBoard', () => {
    const source = readSource('src/components/ProjectBoard.vue')

    expect(source).toContain("import BaseLoadingState from './bases/BaseLoadingState.vue'")
    expect(source).toContain('<BaseLoadingState v-if="isLoadingBoard" message="正在載入專案看板..." />')
  })

  it('uses shared primitives for task detail loading and error UI in TaskDetailModal', () => {
    const source = readSource('src/components/TaskDetailModal.vue')

    expect(source).toContain("import BaseErrorState from './bases/BaseErrorState.vue'")
    expect(source).toContain("import BaseLoadingState from './bases/BaseLoadingState.vue'")
    expect(source).toContain('<BaseLoadingState message="正在載入任務詳細資訊..." />')
    expect(source).toContain('<BaseErrorState :message="errorMessage" />')
  })
})