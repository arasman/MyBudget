# MyBudget — Outline de la presentación (PPTX)

Documento de alineación antes de generar el `.pptx`. Idioma: **español** (versión en inglés después, si se solicita, sin re-derivar contenido — solo traducción).

**Entrega asíncrona** — sin límite de tiempo de defensa; se comparten 4 URLs: repositorio GitHub, despliegue + credenciales demo, slides (inicialmente en `docs/slides/presentation/` dentro del repo, evaluando luego Google Drive u otro), y video (5-10 min, script a definir en una fase posterior basándose en estos slides).

**Fuentes usadas**: `AnalisisInicial/PlanteamientoDeProyecto.txt`, `AnalisisInicial/SituacionActual.txt`, `README.md`, `openspec/ROADMAP.md`, y las 89 capturas en `docs/slides/flows/*/`.

**Estado**: ✅ Aprobado por el usuario — decisiones incorporadas:
1. Sin restricción de cantidad de slides (entrega asíncrona, no hay límite de tiempo de defensa presencial).
2. Cifras exactas de pruebas + complemento cualitativo.
3. Portada con datos reales del autor y del programa del máster (placeholders `[COMPLETAR: ...]` abajo, a rellenar antes de generar el .pptx).
4. Uso amplio de las 89 capturas — casi todas se incluyen, organizadas en slides de 2-6 imágenes por paso del flujo (no solo 2-4 por feature).
5. Diagramas mermaid en archivo `.md` editable aparte (`presentation/flows.md`), insertados como PNG en el PPTX.
6. "Cierre" sin framing de Q&A en vivo (no hay defensa presencial).
7. Reorganización de `docs/slides/` en `flows/` (capturas E2E) + `presentation/` (outline, diagramas, .pptx) — ver `docs/slides/README.md`.

**Revisión 2** (tras primera generación) — ajustes de densidad de imágenes, aplicados directamente en
`Project/frontend/scripts/build-pptx.mjs` (fuente definitiva del contenido; el detalle slide-por-slide
de abajo queda como plan original, no se reescribió 1:1 tras esta revisión):
- Registro: 4 imágenes en una slide → 2 slides de 2 (el formulario es alto, se aplastaba en grid 2×2).
- Dashboard: grids de 3-4 imágenes → 1 imagen grande por slide (contenido multi-gráfico, ilegible reducido).
- Matriz de ejecución (3 slides de 3-4 img) → 6 slides de 1-2 img (texto pequeño en celdas de la matriz).
- Cuentas bancarias — creación (1 slide de 5 img) → 3 slides de 1-2 img.
- Gestión de presupuestos (1 slide de 6 img) → 3 slides de 2 img.
- `addImageGridSlide` capa el tamaño máximo de celda y centra el grid — slides con 1-2 imágenes ya no
  las estira para llenar todo el ancho (ej. Invitaciones — resultado, 2 imágenes).
- Observabilidad + Despliegue (2 slides sueltas, cada una con mucho espacio en blanco) → 1 slide con
  2 secciones lado a lado.
- Total: ~39 → 51 slides.

---

## Estructura propuesta (≈40 slides)

### Bloque 1 — Portada y contexto (3 slides)

**1. Portada**
- Título: MyBudget — Gestión de presupuesto familiar
- Autor: `[COMPLETAR: nombre completo como aparece en el campus]`
- Programa: `[COMPLETAR: nombre exacto del máster]`
- Año: 2026
- Sin imagen; branding simple (paleta violeta/verde de la app, coherente con las capturas)

**2. El problema: la hoja de cálculo**
- Basado en `SituacionActual.txt`: presupuesto familiar gestionado en Excel con 3 hojas (Ejecución presupuestaria, Proyectos, Historial y Situación Actual)
- Dolores concretos: un rubro por fila obliga a re-crear cada período manualmente; el historial de cambios de monto presupuestado se pierde o se vuelve difícil de rastrear; cálculo manual de tipo de cambio Q/USD; sin control de acceso — cualquiera con el archivo edita todo
- Diagrama mermaid #1: las 3 hojas de Excel y sus dependencias manuales

**3. La propuesta**
- Basado en `PlanteamientoDeProyecto.txt`: aplicación web responsive (Vue + .NET + PostgreSQL), múltiples presupuestos por propietario, roles (Owner/Admin/Operator/Read-only), ciclos y períodos, rubros con tipos (Gasto, Ahorro largo plazo, Ahorro preventivo), snapshots de situación actual, gráficas comparativas
- Alcance MVP A (construido) vs. MVP B (diferido: proyectos, deudas/cuotas, import/export) — deja explícito qué se entregó en este TFM

---

### Bloque 2 — Arquitectura y proceso (4 slides)

**4. Stack tecnológico**
- Tabla resumida: backend .NET 10 + ASP.NET Core Minimal APIs + Mediator + Dapper/EF Core + PostgreSQL 16; frontend Vue 3.5 + TypeScript + Pinia + Tailwind v4 + daisyUI + Chart.js; infraestructura Docker Compose
- Fuente: README.md, tabla "Tech Stack"

**5. Arquitectura — Vertical Slice Architecture**
- Cada caso de uso es una carpeta autocontenida (`Features/<Área>/<CasoDeUso>/`) con 4 archivos (request+handler, validator, endpoint, DTOs)
- Diagrama mermaid #2: capas VSA

**6. Proceso de desarrollo — Spec-Driven Development**
- Cada feature pasó por: exploración → propuesta → spec → diseño → tasks → implementación → verificación → archivo
- 23 cambios (`openspec/changes/archive/`) documentados de extremo a extremo
- Uso de agentes/IA como herramienta dentro del proceso SDD, dirigido por el desarrollador (no autónomo)

**7. Línea de tiempo — de 0 a MVP A**
- 2026-07-07 (`foundation`, scaffold completo) → 2026-08-04 (`dashboard`, última pieza de MVP A)
- Hitos: `auth` (07-08), `budget-structure` + UI (07-10/11), `budget-execution` + UI (07-13/14), `current-situation` (07-29), `dashboard` (08-04)
- Total: 23 cambios archivados, ~1 mes de desarrollo

---

### Bloque 3 — Funcionalidades principales (≈23 slides, con capturas amplias)

Cada área de funcionalidad tiene: 1 slide de descripción (+ diagrama mermaid donde aplica), seguida de 1-3 slides de "recorrido visual" con las capturas reales de los flujos E2E, mostrando el camino feliz y los casos de error/borde (nombre duplicado, validación, confirmación de borrado, restauración).

**8. Cuentas y acceso — descripción**
- Registro, login JWT, invitación por email con rol, 4 roles por presupuesto, recuperación de contraseña
- Diagrama mermaid #3: flujo de invitación de usuario

**9. Registro** — 4 imágenes: `flows/auth/01-register-empty`, `02-register-filled`, `03-register-success`, `04-register-duplicate-error`

**10. Login y Logout** — 5 imágenes: `flows/auth/05-login-empty`, `06-login-success`, `07-login-invalid-error`, `08-logout-menu`, `09-logout-success`

**11. Estructura de presupuesto — descripción**
- Ciclos → Períodos → Rubros, agrupados por categorías; historial de revisiones de monto sin perder el rastro de auditoría
- Diagrama mermaid #4: jerarquía del dominio

**12. Ciclos — creación** — 4 imágenes: `flows/budget-structure-cycles/01-list-empty`, `02-create-form`, `03-create-success`, `04-create-duplicate-error`

**13. Ciclos — edición y ciclo de vida** — 5 imágenes: `05-edit-form`, `06-edit-success`, `07-set-active-success`, `08-delete-confirm`, `09-delete-success`

**14. Categorías — grupos** — 5 imágenes: `flows/budget-structure-categories/01-list-empty`, `02-create-group-form`, `03-create-group-success`, `04-create-group-duplicate-error`

**15. Categorías — categorías y ciclo de vida** — 5 imágenes: `05-create-category-form`, `06-create-category-success`, `07-create-category-duplicate-error`, `09-delete-category-success`, `10-restore-category-success`

**16. Períodos — creación** — 4 imágenes: `flows/budget-structure-periods-lines/01-period-list-empty`, `02-period-create-form`, `03-period-create-success`, `04-period-create-duplicate-error`

**17. Períodos — estado y eliminación** — 4 imágenes: `05-period-status-form`, `06-period-status-success`, `07-period-delete-confirm`, `08-period-delete-success`

**18. Rubros — creación** — 4 imágenes: `09-line-list-empty`, `10-line-create-form`, `11-line-create-success`, `12-line-create-duplicate-error`

**19. Rubros — edición y eliminación** — 4 imágenes: `13-line-edit-inline`, `14-line-edit-success`, `15-line-delete-confirm`, `16-line-delete-success`

**20. Ejecución (gasto real) — descripción**
- Matriz multi-período con CRUD en línea, notas de crédito/débito, tipo de cambio por entrada, toggle de moneda
- Diagrama mermaid #5: flujo de registro de ejecución

**21. Matriz de ejecución** — 4 imágenes: `flows/budget-execution/01-matrix-view`, `02-open-execution-modal`, `03-create-validation-error`, `04-create-form-filled`

**22. Registro y actualización de la matriz** — 3 imágenes: `05-create-success`, `06-matrix-updated`, `09-collapse-group`

**23. Moneda y eliminación** — 4 imágenes: `07-currency-toggle-usd`, `08-currency-toggle-gtq`, `10-delete-confirm`, `11-delete-success`

**24. Situación actual — el "corte" — descripción**
- Catálogo de cuentas bancarias, snapshot diario de saldos vs. presupuestado/ejecutado, multi-moneda

**25. Cuentas bancarias** — 5 imágenes: `flows/bank-accounts/01-list-empty`, `02-create-form`, `03-create-success`, `04-create-duplicate-error`, `09-show-deleted-toggle`

**26. Cuentas bancarias — edición y ciclo de vida** — 4 imágenes: `05-edit-form`, `06-edit-success`, `07-delete-confirm`, `10-restore-success`

**27. Corte — captura** — 4 imágenes: `flows/current-situation/01-draft-form`, `02-form-filled`, `03-save-error`, `04-save-success`

**28. Corte — eliminación segura** — 3 imágenes: `05-delete-confirm-empty`, `06-delete-confirm-typed`, `07-delete-success`

**29. Dashboard analítico — descripción**
- Tendencia histórica, banda de comportamiento promedio, comparación de rubros por período/ciclo, con manejo explícito de tipo de cambio (congelado vs. transaccional)

**30. Dashboard — tendencia histórica** — 3 imágenes: `flows/dashboard/01-lifetime-trend`, `02-series-picker-empty`, `06-insufficient-history`

**31. Dashboard — comparación de rubros y responsive** — 4 imágenes: `03-budget-line-empty`, `04-budget-line-selected`, `05-cross-cycle-mode`, `07-mobile-viewport`

**32. Gestión de presupuestos múltiples y equipo** — 6 imágenes: `flows/budget-management/01-budget-list`, `02-create-form`, `03-create-duplicate-error`, `04-create-success`, `07-show-deleted-toggle`, `08-restore-success`

**33. Invitaciones — resultado** — 2 imágenes: `flows/budget-management/09-invite-accept-success`, `10-invite-accept-error`

---

### Bloque 4 — Calidad y despliegue (4 slides)

**34. Pruebas automatizadas**
- Cifras exactas por capa (backend unit/integration xUnit+Postgres real, frontend unit Vitest, E2E Playwright), tomadas del último estado documentado en `openspec/ROADMAP.md` (ej. `dashboard`: 488 unit + 619 frontend + 275/278 integration + 115 E2E)
- Complemento cualitativo: TDD estricto en todo el ciclo, 0 issues CRITICAL en las verificaciones finales de cada cambio

**35. Observabilidad**
- Logs estructurados (Serilog + Seq), trazas distribuidas (OpenTelemetry + Jaeger), auditoría de mutaciones + eventos de seguridad con retención de 90 días

**36. Despliegue en producción**
- Hetzner + Caddy + Brevo (correo real); URL pública: mybudget-aras.duckdns.org
- Mención breve de lecciones aprendidas (`docs/Deployment-LessonLearned.md`) sin entrar en detalle técnico

**37. Seguridad y control de acceso**
- RBAC de 4 niveles, JWT de corta duración (15 min) + refresh rotativo (7 días), bloqueo de cuenta tras 5 intentos fallidos, historial de últimas 5 contraseñas

---

### Bloque 5 — Cierre (2 slides)

**38. Estado y alcance futuro (MVP B)**
- MVP A completo (23 cambios); MVP B planeado: proyectos, compromisos financieros, cuotas/deudas, import/export
- Deja claro qué quedó fuera de este entregable y por qué (alcance controlado para el TFM)

**39. Cierre**
- Enlaces: repositorio GitHub, app en vivo, agradecimientos
- Sin sección de preguntas en vivo (entrega asíncrona) — se invita a contactar por [correo/canal a definir]

---

## Diagramas Mermaid a crear (`docs/slides/presentation/flows.md`)

| # | Nombre | Tipo | Usado en slide |
|---|--------|------|----------------|
| 1 | Hoja de cálculo actual (el problema) | flowchart | 2 |
| 2 | Capas VSA (arquitectura) | flowchart | 5 |
| 3 | Flujo de invitación de usuario | sequenceDiagram | 8 |
| 4 | Jerarquía del dominio (Budget→Cycle→Period→BudgetLine) | erDiagram | 11 |
| 5 | Flujo de registro de ejecución | flowchart | 20 |

Cada diagrama se renderiza a PNG (vía `@mermaid-js/mermaid-cli`, no requiere el stack de la app corriendo) y se inserta en el PPTX igual que las capturas de pantalla.

---

## Notas de alcance

- **Video**: fuera de alcance de esta fase — su script se definirá después, basándose en estos slides, cuando se aborde específicamente esa entrega (5-10 min sugeridos).
- **Versión en inglés**: se genera después de aprobar/ajustar la versión en español, como traducción del mismo contenido, no como trabajo nuevo.
