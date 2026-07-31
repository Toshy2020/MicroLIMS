# MicroLIMS Workflow Diagrams

Companion to `BusinessRules.md` — same rules, visual form. Each diagram
matches its `*WorkflowEngine.cs` file exactly.

## Product Workflow

```mermaid
flowchart TD
    A[Receive Sample] --> B{Item has assigned tests?}
    B -- No --> Z[Throw: configuration required]
    B -- Yes --> C[Generate one TestOrder per assigned test]
    C --> D[Waiting]
    D --> E[Running - result entry]
    E --> F[Ready - result entered]
    F --> G[Reviewed]
    G --> H[Approved / Rejected / Retest.../ Investigation]
```

## Pathogen Engine — Universal Chain

```mermaid
flowchart TD
    A[TSB] --> B{Growth?}
    B -- No --> C[Absent - chain closed]
    B -- Yes --> D[Detection Media]
    D --> E{Growth?}
    E -- Yes --> F[Detected]
    E -- No --> C
```

## Pathogen Engine — Salmonella Exception

```mermaid
flowchart TD
    A[TSB] --> B{Growth?}
    B -- No --> Z[Absent - chain closed]
    B -- Yes --> C[RVS]
    C --> D{Growth?}
    D -- No --> Z
    D -- Yes --> E[XLD + TSI]
    E --> F{Growth?}
    F -- Yes --> G[Detected]
    F -- No --> Z
```

## Water Calculation Engine

```mermaid
flowchart TD
    A[Enter raw readings] --> B[Calculate average]
    B --> C{Average > Spec Limit?}
    C -- Yes --> D[OutOfSpecification]
    C -- No --> E{Average > Action Limit?}
    E -- Yes --> F[ActionLimitExceeded]
    E -- No --> G{Average > Alert Limit?}
    G -- Yes --> H[AlertLimitExceeded]
    G -- No --> I[WithinLimits]
```

## EM (Environmental Monitoring) Engine

```mermaid
flowchart TD
    A[Room Selection] --> B[Start Step 1 Incubation]
    B --> C[Complete Step 1 - enter count]
    C --> D[Start Step 2 Incubation]
    D --> E[Complete Step 2 - enter count]
    E --> F{Step1Count + Step2Count > ActionLimit?}
    F -- Yes --> G[Out Of Trend]
    F -- No --> H[Within Trend]
    G --> I[Ready for Review]
    H --> I
```

## After Cleaning Engine

```mermaid
flowchart TD
    A[Select Machine] --> B[Select Parts]
    B --> C{Any part configured for collective pathogen sample?}
    C -- Yes --> D[Create ONE collective pathogen TestOrder]
    C -- No --> E[Skip collective sample]
    D --> F[Create ONE individual TAMC TestOrder per selected part]
    E --> F
```

## GPT (Growth Promotion Test) Engine

```mermaid
flowchart TD
    A[Preparation] --> B[Sterility]
    B --> C[Recovery]
    C --> D[Record challenge organism results]
    D --> E{All organisms passed?}
    E -- Yes --> F[Release - usable in routine testing]
    E -- No --> G[Rejected - cannot be released]
```

## Review → Approval (shared across all domains)

```mermaid
flowchart TD
    A[ResultEntered] --> B{Review Mode}
    B -- Detailed --> C[Full workflow history + individual review]
    B -- Quick Table --> D[Batch review - skips ineligible orders]
    C --> E[Reviewed]
    D --> E
    E --> F{Section Head Decision}
    F --> G[Approve]
    F --> H[Reject]
    F --> I[Retest Retained Sample]
    F --> J[New Sample Request]
    F --> K[Investigation *comment required*]
    F --> L[OOS Investigation *comment required*]
```
