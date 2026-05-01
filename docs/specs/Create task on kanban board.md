# Create task on kanban board

 Scenario: User creates a task in a project
    - Given the user is on the project list page
    - When the user creates a project named "RonFlow v0.1"
    - And the user opens the project kanban board
    - And the user creates a task titled "Build Kanban Board"
    - Then the task should appear under the workflow initial state
    - And the task should be visible on the kanban board

``` mermaid
flowchart TD
    A["Project List Page"]

    B["Create Project Modal"]
    C["Project Kanban Board"]

    D["Create Task Modal"]
    E["Task Detail Drawer"]

    F["Workflow Settings Page<br/>Future"]
    G["Project Settings Page<br/>Future"]
    H["Reports / Dashboard<br/>Future"]

    A -->|"Click Create Project"| B
    B -->|"Project Created"| C
    A -->|"Open Project"| C

    C -->|"Click Create Task"| D
    D -->|"Task Created"| C

    C -->|"Click Task Card"| E
    E -->|"Close Drawer"| C

    C -. "Future" .-> F
    C -. "Future" .-> G
    C -. "Future" .-> H
```

``` mermaid
sequenceDiagram
    actor User
    participant ProjectList as Project List Page
    participant CreateProject as Create Project Modal
    participant Board as Project Kanban Board
    participant CreateTask as Create Task Modal
    participant Detail as Task Detail Drawer
    participant System as RonFlow System

    User->>ProjectList: Open RonFlow
    ProjectList-->>User: Show project list

    User->>ProjectList: Click "Create Project"
    ProjectList->>CreateProject: Open modal

    User->>CreateProject: Enter project name
    User->>CreateProject: Submit
    CreateProject->>System: CreateProject
    System-->>CreateProject: Project created with default workflow

    CreateProject->>Board: Navigate to project board
    Board-->>User: Show workflow columns

    User->>Board: Click "Create Task"
    Board->>CreateTask: Open modal

    User->>CreateTask: Enter task title
    User->>CreateTask: Submit
    CreateTask->>System: CreateTask
    System-->>CreateTask: Task created in initial state

    CreateTask->>Board: Refresh / update board
    Board-->>User: Show task card in initial column

    User->>Board: Click task card
    Board->>Detail: Open drawer
    Detail-->>User: Show task detail
```