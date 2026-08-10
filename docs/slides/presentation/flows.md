# MyBudget — Diagramas Mermaid

Fuente editable de los diagramas usados en `MyBudget.pptx`. Cada bloque se renderiza a PNG
(`pnpm run render-diagrams` desde `Project/frontend`, ver `outline.md`) y se inserta como imagen
en el PPTX — PowerPoint no renderiza mermaid nativamente.

Editar aquí, volver a renderizar, volver a insertar en el deck. No editar los PNG directamente.

---

## 1. El problema — hoja de cálculo actual

Usado en slide 2. Fuente: `AnalisisInicial/SituacionActual.txt`.

```mermaid
flowchart TB
    subgraph Hoja1["Hoja 1 — Ejecución presupuestaria"]
        H1A["Un rubro por fila,<br/>por período (mes)<br/>— se copia a mano<br/>cada período"]
        H1B["Monto presupuestado<br/>(último valor visible,<br/>sin auditoría de cambios)"]
        H1C["Ejecuciones registradas<br/>manualmente por fecha"]
        H1D["Cálculo manual de<br/>tipo de cambio Q/USD"]
    end

    subgraph Hoja2["Hoja 2 — Proyectos"]
        H2A["Fases y montos<br/>presupuestados"]
        H2B["Ejecuciones con<br/>archivos adjuntos<br/>(carpeta aparte)"]
    end

    subgraph Hoja3["Hoja 3 — Historial y Situación Actual"]
        H3A["Saldos de cuentas<br/>(copiados a mano)"]
        H3B["Proyección de ingresos<br/>del siguiente período"]
        H3C["Tabla de snapshots<br/>(recalculada<br/>manualmente cada vez)"]
    end

    H1D ~~~ H2A
    H2A ~~~ H3A

    H1C -.->|"totales referenciados<br/>a mano"| H3C
    H2B -.->|"referencia manual"| H3C
    H3A -.->|"tipo de cambio<br/>ingresado a mano"| H3C

    classDef pain fill:#fecaca,stroke:#dc2626,color:#7f1d1d
    class H1A,H1B,H1D,H3A,H3C pain
```

**Puntos de dolor** (rojo): re-creación manual por período, pérdida de historial de cambios, tipo
de cambio manual, reconciliación manual de snapshots — sin control de acceso ni auditoría.

---

## 2. Arquitectura — Vertical Slice Architecture

Usado en slide 5.

```mermaid
flowchart LR
    subgraph Cliente["Cliente"]
        FE["Vue 3 + TypeScript<br/>Pinia + vue-router"]
    end

    subgraph Gateway["Gateway (opcional)"]
        GW["YARP<br/>reverse proxy"]
    end

    subgraph API["MyBudget.Api"]
        EP["Minimal API endpoint<br/>(1 por caso de uso)"]
    end

    subgraph Slice["Vertical Slice"]
        REQ["Request + Handler"]
        VAL["FluentValidation<br/>Validator"]
        DTO["Request/Response<br/>DTOs"]
    end

    subgraph Pipeline["Mediator Pipeline"]
        direction TB
        P1["ValidationBehaviour"]
        P2["LoggingBehaviour"]
        P3["CachingBehaviour"]
        P1 --> P2 --> P3
    end

    subgraph Datos["Persistencia"]
        DAP["Dapper (reads)"]
        EF["EF Core (writes)"]
        PG[("PostgreSQL 16")]
    end

    FE -->|"HTTP/JSON"| GW --> EP
    EP --> REQ
    REQ --> Pipeline
    Pipeline --> DAP & EF
    DAP --> PG
    EF --> PG

    classDef slice fill:#ede9fe,stroke:#7c3aed,color:#4c1d95
    class REQ,VAL,DTO slice
```

**Regla clave**: los slices nunca se referencian entre sí directamente — tipos compartidos solo
pasan a `SharedKernel` cuando los usan 3+ slices genuinamente.

---

## 3. Flujo de invitación de usuario

Usado en slide 8.

```mermaid
sequenceDiagram
    actor Admin
    actor Invitado
    participant API
    participant Email as Servicio de correo
    participant DB as PostgreSQL

    Admin->>API: POST /budgets/{id}/invitations<br/>{email, rol}
    API->>DB: crear InvitationToken (hash)
    API->>Email: enviar enlace de invitación
    Email-->>Invitado: correo con enlace

    Invitado->>API: GET /invitations/accept?token=...
    alt no autenticado
        API-->>Invitado: redirige a /login?redirect=...
        Invitado->>API: login
    end
    Invitado->>API: POST /auth/invitations/accept<br/>{token}
    API->>DB: validar token (no usado, no expirado)
    API->>DB: crear BudgetMembership (rol asignado)
    API-->>Invitado: 200 — acceso concedido al presupuesto
```

**Roles asignables**: Owner (100%), Admin, Operator (opera, no configura), Read-only (solo lectura).

---

## 4. Jerarquía del dominio

Usado en slide 11.

```mermaid
erDiagram
    Budget ||--o{ Cycle : "tiene"
    Budget ||--o{ BudgetLine : "tiene (nivel presupuesto)"
    Budget ||--o{ BudgetMembership : "otorga acceso vía"
    Cycle ||--o{ Period : "se divide en"
    CategoryGroup ||--o{ Category : "agrupa"
    BudgetLine }o--|| CategoryGroup : "pertenece a"
    BudgetLine }o--o| Category : "pertenece a (opcional)"
    BudgetLine ||--o{ BudgetLineRevision : "historial de montos"
    BudgetLine ||--o{ ExecutionRecord : "registra gasto real en"
    Period ||--o{ ExecutionRecord : "ocurre dentro de"

    Budget {
        string name
        bool isDeleted
    }
    Cycle {
        string name
        date startDate
        date endDate
        guid defaultCurrencyId
        guid alternateCurrencyId
        decimal exchangeRate
    }
    Period {
        string name
        date startDate
        date endDate
        enum status "open | closed"
    }
    BudgetLine {
        string name
        enum lineType "Expense | LongTermSavings | PreventiveSavings"
        date startDate
        date endDate "nullable — vigencia"
    }
    BudgetLineRevision {
        decimal amount
        date validFrom
        date validTo "nullable — gapless"
    }
    ExecutionRecord {
        enum entryType "Expense | CreditNote | DebitNote"
        decimal amount
        date operationDate
        guid currencyId
        decimal exchangeRate
    }
```

**Nota de diseño**: `BudgetLine` es a nivel de Budget (no de Period) desde el rediseño — vive con
un rango de fechas propio (`StartDate`/`EndDate`) y su monto planificado cambia a través de
`BudgetLineRevision`, un historial *append-only* sin huecos (invariante "gapless").

---

## 5. Flujo de registro de ejecución

Usado en slide 20.

```mermaid
flowchart TD
    A["Usuario abre la matriz<br/>de ejecución (período activo)"] --> B["Doble-click en celda<br/>'Ejecutado' de un rubro"]
    B --> C["Se abre el modal<br/>de ejecución"]
    C --> D["Selecciona tipo:<br/>Gasto / Nota crédito / Nota débito"]
    D --> E["Ingresa monto, moneda,<br/>nota (siempre requerida)"]
    E --> F{"¿Válido?<br/>nota presente,<br/>monto > 0,<br/>fecha dentro del período"}
    F -->|No| G["Error de validación<br/>en el formulario"]
    G --> E
    F -->|Sí| H{"¿Período cerrado?"}
    H -->|Sí| I["409 PERIOD_CLOSED —<br/>rechazado"]
    H -->|No| J["POST .../executions"]
    J --> K["Se persiste ExecutionRecord"]
    K --> L["Matriz recalcula<br/>Ejecutado / Diferencia<br/>para ese rubro y período"]
    L --> M["Toast de éxito"]

    classDef error fill:#fecaca,stroke:#dc2626,color:#7f1d1d
    classDef success fill:#bbf7d0,stroke:#16a34a,color:#14532d
    class G,I error
    class M success
```
