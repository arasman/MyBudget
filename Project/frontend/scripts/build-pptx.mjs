// Builds docs/slides/presentation/MyBudget.pptx from the approved outline
// (docs/slides/presentation/outline.md). Slide content is hardcoded here as
// data, mirroring the outline 1:1 — this script is the executable version
// of that plan, not an independent source of truth.
// Run with: pnpm build-pptx (after pnpm render-diagrams and pnpm e2e:slides).
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import pptxgen from 'pptxgenjs'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const REPO_ROOT = path.resolve(__dirname, '../../..')
const FLOWS = path.join(REPO_ROOT, 'docs/slides/flows')
const DIAGRAMS = path.join(REPO_ROOT, 'docs/slides/presentation/diagrams')
const OUT = path.join(REPO_ROOT, 'docs/slides/presentation/MyBudget.pptx')

const AUTHOR = {
  name: 'Alejandro Rafael Alfaro Soto',
  program: 'Master de Desarrollo con IA',
  githubUrl: 'https://github.com/arasman/MyBudget',
  contact: 'arasman@gmail.com',
}

const COLOR = {
  primary: '7C3AED',
  primaryDark: '4C1D95',
  accent: '10B981',
  error: 'DC2626',
  text: '1F2937',
  textLight: '6B7280',
  bg: 'FFFFFF',
  bgAlt: 'F9FAFB',
  border: 'E5E7EB',
}
const FONT = 'Arial'

const pptx = new pptxgen()
pptx.defineLayout({ name: 'WIDE', width: 13.33, height: 7.5 })
pptx.layout = 'WIDE'
pptx.author = AUTHOR.name
pptx.title = 'MyBudget — TFM'

const W = 13.33
const H = 7.5
const MARGIN = 0.6

function flow(dir, file) {
  return path.join(FLOWS, dir, file)
}
function diagram(file) {
  return path.join(DIAGRAMS, file)
}

// ---------------------------------------------------------------------------
// Slide helpers
// ---------------------------------------------------------------------------

function baseSlide() {
  const slide = pptx.addSlide()
  slide.background = { color: COLOR.bg }
  return slide
}

function addHeader(slide, title, kicker) {
  if (kicker) {
    slide.addText(kicker.toUpperCase(), {
      x: MARGIN, y: 0.35, w: W - MARGIN * 2, h: 0.3,
      fontFace: FONT, fontSize: 12, color: COLOR.primary, bold: true, charSpacing: 1,
    })
  }
  slide.addText(title, {
    x: MARGIN, y: kicker ? 0.62 : 0.4, w: W - MARGIN * 2, h: 0.7,
    fontFace: FONT, fontSize: 28, color: COLOR.text, bold: true,
  })
  slide.addShape('rect', {
    x: MARGIN, y: kicker ? 1.28 : 1.05, w: 0.9, h: 0.05,
    fill: { color: COLOR.primary }, line: { type: 'none' },
  })
}

function addTitleSlide() {
  const slide = baseSlide()
  slide.background = { color: COLOR.primaryDark }
  slide.addShape('rect', { x: 0, y: 0, w: W, h: 0.15, fill: { color: COLOR.accent }, line: { type: 'none' } })
  slide.addText('MyBudget', {
    x: 0, y: 2.5, w: W, h: 1.2, align: 'center',
    fontFace: FONT, fontSize: 54, color: 'FFFFFF', bold: true,
  })
  slide.addText('Gestión de presupuesto familiar', {
    x: 0, y: 3.65, w: W, h: 0.6, align: 'center',
    fontFace: FONT, fontSize: 22, color: 'D8B4FE',
  })
  slide.addText('Trabajo Fin de Máster', {
    x: 0, y: 5.0, w: W, h: 0.4, align: 'center',
    fontFace: FONT, fontSize: 16, color: 'FFFFFF',
  })
  slide.addText(AUTHOR.name, {
    x: 0, y: 5.45, w: W, h: 0.4, align: 'center',
    fontFace: FONT, fontSize: 16, color: 'FFFFFF', italic: true,
  })
  slide.addText(AUTHOR.program, {
    x: 0, y: 5.85, w: W, h: 0.4, align: 'center',
    fontFace: FONT, fontSize: 14, color: 'D8B4FE', italic: true,
  })
  slide.addText('2026', {
    x: 0, y: 6.6, w: W, h: 0.4, align: 'center',
    fontFace: FONT, fontSize: 14, color: 'D8B4FE',
  })
}

function addBulletSlide({ kicker, title, bullets, diagramFile, sourceNote }) {
  const slide = baseSlide()
  addHeader(slide, title, kicker)

  const hasDiagram = !!diagramFile
  const textW = hasDiagram ? 5.6 : W - MARGIN * 2

  slide.addText(
    bullets.map((b) => ({ text: b, options: { bullet: { code: '2022' }, breakLine: true, paraSpaceAfter: 14 } })),
    {
      x: MARGIN, y: 1.6, w: textW, h: H - 2.1,
      fontFace: FONT, fontSize: 16, color: COLOR.text, valign: 'top', lineSpacing: 22,
    },
  )

  if (hasDiagram) {
    const boxW = W - MARGIN - 0.2 - (MARGIN + textW + 0.2)
    const boxX = MARGIN + textW + 0.2
    slide.addImage({
      path: diagram(diagramFile),
      x: boxX, y: 1.6, w: boxW, h: H - 2.3,
      sizing: { type: 'contain', w: boxW, h: H - 2.3 },
    })
  }

  if (sourceNote) {
    slide.addText(sourceNote, {
      x: MARGIN, y: H - 0.45, w: W - MARGIN * 2, h: 0.3,
      fontFace: FONT, fontSize: 9, color: COLOR.textLight, italic: true,
    })
  }
  return slide
}

/** Two side-by-side bullet sections on one slide — for merging two short topics. */
function addDualBulletSlide({ kicker, title, sectionA, sectionB }) {
  const slide = baseSlide()
  addHeader(slide, title, kicker)

  const colW = (W - MARGIN * 2 - 0.5) / 2
  const cols = [
    { section: sectionA, x: MARGIN },
    { section: sectionB, x: MARGIN + colW + 0.5 },
  ]

  cols.forEach(({ section, x }) => {
    slide.addText(section.heading, {
      x, y: 1.6, w: colW, h: 0.4,
      fontFace: FONT, fontSize: 17, color: COLOR.primary, bold: true,
    })
    slide.addText(
      section.bullets.map((b) => ({ text: b, options: { bullet: { code: '2022' }, breakLine: true, paraSpaceAfter: 12 } })),
      {
        x, y: 2.15, w: colW, h: H - 2.7,
        fontFace: FONT, fontSize: 14.5, color: COLOR.text, valign: 'top', lineSpacing: 20,
      },
    )
  })
  return slide
}

function addFullDiagramSlide({ kicker, title, diagramFile, caption }) {
  const slide = baseSlide()
  addHeader(slide, title, kicker)
  const boxY = 1.5
  const boxH = H - boxY - (caption ? 0.7 : 0.3)
  slide.addImage({
    path: diagram(diagramFile),
    x: MARGIN, y: boxY, w: W - MARGIN * 2, h: boxH,
    sizing: { type: 'contain', w: W - MARGIN * 2, h: boxH },
  })
  if (caption) {
    slide.addText(caption, {
      x: MARGIN, y: H - 0.55, w: W - MARGIN * 2, h: 0.35, align: 'center',
      fontFace: FONT, fontSize: 11, color: COLOR.textLight, italic: true,
    })
  }
  return slide
}

/**
 * images: [{ dir, file, caption }] — dir is the flow folder, file the PNG, caption a short Spanish label.
 * Cell size is capped (maxCellW/maxCellH) and the resulting grid centered — otherwise 1-2 image
 * slides would stretch each image to fill the whole slide, looking artificially blown up.
 * imageScale (0-1) further shrinks the image within its cell, leaving visible padding around it —
 * useful when even a capped cell still looks like it's forcing the image to fill the frame.
 */
function addImageGridSlide({ kicker, title, images, maxCellW = 6.4, maxCellH = 4.7, imageScale = 1 }) {
  const slide = baseSlide()
  addHeader(slide, title, kicker)

  const top = 1.55
  const bottom = 0.35
  const availH = H - top - bottom
  const availW = W - MARGIN * 2
  const n = images.length
  const cols = n <= 2 ? n : n <= 4 ? 2 : 3
  const rows = Math.ceil(n / cols)
  const gap = 0.25
  const captionH = 0.32

  const cellW = Math.min((availW - gap * (cols - 1)) / cols, maxCellW)
  const cellH = Math.min((availH - gap * (rows - 1)) / rows, maxCellH)
  const gridW = cellW * cols + gap * (cols - 1)
  const gridH = cellH * rows + gap * (rows - 1)
  const startX = MARGIN + (availW - gridW) / 2
  const startY = top + (availH - gridH) / 2
  const imgH = cellH - captionH

  images.forEach((img, i) => {
    const col = i % cols
    const row = Math.floor(i / cols)
    const x = startX + col * (cellW + gap)
    const y = startY + row * (cellH + gap)

    slide.addShape('rect', {
      x, y, w: cellW, h: imgH,
      fill: { color: COLOR.bgAlt }, line: { color: COLOR.border, width: 1 },
    })
    const innerW = (cellW - 0.1) * imageScale
    const innerH = (imgH - 0.1) * imageScale
    const imgX = x + 0.05 + (cellW - 0.1 - innerW) / 2
    const imgY = y + 0.05 + (imgH - 0.1 - innerH) / 2
    slide.addImage({
      path: flow(img.dir, img.file),
      x: imgX, y: imgY, w: innerW, h: innerH,
      sizing: { type: 'contain', w: innerW, h: innerH },
    })
    slide.addText(img.caption, {
      x, y: y + imgH, w: cellW, h: captionH, align: 'center',
      fontFace: FONT, fontSize: 10.5, color: COLOR.textLight,
    })
  })
  return slide
}

/** One large image per slide — for screenshots too content-dense to shrink into a grid cell. */
function addSingleImageSlide({ kicker, title, dir, file, caption }) {
  const slide = baseSlide()
  addHeader(slide, title, kicker)

  const top = 1.5
  const bottom = caption ? 0.9 : 0.3
  const boxW = W - MARGIN * 2
  const boxH = H - top - bottom

  slide.addShape('rect', {
    x: MARGIN, y: top, w: boxW, h: boxH,
    fill: { color: COLOR.bgAlt }, line: { color: COLOR.border, width: 1 },
  })
  slide.addImage({
    path: flow(dir, file),
    x: MARGIN + 0.1, y: top + 0.1, w: boxW - 0.2, h: boxH - 0.2,
    sizing: { type: 'contain', w: boxW - 0.2, h: boxH - 0.2 },
  })
  if (caption) {
    slide.addText(caption, {
      x: MARGIN, y: H - 0.7, w: boxW, h: 0.5, align: 'center',
      fontFace: FONT, fontSize: 13, color: COLOR.textLight, italic: true,
    })
  }
  return slide
}

function addTableSlide({ kicker, title, headers, rows, note }) {
  const slide = baseSlide()
  addHeader(slide, title, kicker)

  const tableRows = [
    headers.map((h) => ({ text: h, options: { bold: true, color: 'FFFFFF', fill: { color: COLOR.primary } } })),
    ...rows.map((r) => r.map((c) => ({ text: String(c) }))),
  ]
  slide.addTable(tableRows, {
    x: MARGIN, y: 1.6, w: W - MARGIN * 2,
    fontFace: FONT, fontSize: 13, color: COLOR.text,
    border: { type: 'solid', color: COLOR.border, pt: 1 },
    autoPage: false,
  })
  if (note) {
    slide.addText(note, {
      x: MARGIN, y: H - 0.6, w: W - MARGIN * 2, h: 0.4,
      fontFace: FONT, fontSize: 12, color: COLOR.textLight, italic: true,
    })
  }
  return slide
}

function addClosingSlide() {
  const slide = baseSlide()
  slide.background = { color: COLOR.primaryDark }
  slide.addText('Gracias', {
    x: 0, y: 2.5, w: W, h: 1, align: 'center',
    fontFace: FONT, fontSize: 44, color: 'FFFFFF', bold: true,
  })
  slide.addText(
    [
      { text: 'Repositorio: ', options: { bold: true } },
      { text: `${AUTHOR.githubUrl}\n`, options: {} },
      { text: 'App en vivo: ', options: { bold: true } },
      { text: 'mybudget-aras.duckdns.org\n', options: {} },
      { text: 'Contacto: ', options: { bold: true } },
      { text: AUTHOR.contact, options: {} },
    ],
    {
      x: 0, y: 4.0, w: W, h: 1.5, align: 'center',
      fontFace: FONT, fontSize: 16, color: 'E9D5FF', lineSpacing: 28,
    },
  )
}

// ---------------------------------------------------------------------------
// Deck content — mirrors outline.md, with the density fixes requested after
// the first review (splitting saturated grids, single-image dashboard
// slides, capped/centered sizing for small grids, merged sparse bullet
// slides).
// ---------------------------------------------------------------------------

addTitleSlide()

// Bloque 1
addBulletSlide({
  kicker: 'Contexto',
  title: 'El problema: la hoja de cálculo',
  bullets: [
    'Presupuesto familiar gestionado en Excel con 3 hojas: Ejecución presupuestaria, Proyectos, e Historial y Situación Actual.',
    'Un rubro por fila obliga a re-crear cada período manualmente.',
    'El historial de cambios de monto presupuestado se pierde o es difícil de rastrear.',
    'Cálculo manual de tipo de cambio Quetzal/Dólar en cada registro.',
    'Sin control de acceso: cualquiera con el archivo puede editar todo.',
  ],
  diagramFile: '01-el-problema-hoja-de-calculo-actual.png',
  sourceNote: 'Fuente: AnalisisInicial/SituacionActual.txt',
})

addBulletSlide({
  kicker: 'Contexto',
  title: 'La propuesta',
  bullets: [
    'Aplicación web responsive: Vue en el frontend, .NET en el backend, PostgreSQL como base de datos.',
    'Múltiples presupuestos por propietario, con roles: Owner, Admin, Operator, Read-only.',
    'Ciclos y períodos, rubros tipados (Gasto, Ahorro largo plazo, Ahorro preventivo).',
    'Snapshots de situación actual (el "corte") y gráficas comparativas.',
    'MVP A construido en este TFM; MVP B (proyectos, deudas/cuotas, import/export) queda diferido — alcance controlado.',
  ],
})

// Bloque 1bis — Funcionalidades y valor (complemento consolidado, antes del recorrido visual del Bloque 3)
addDualBulletSlide({
  kicker: 'Funcionalidades',
  title: 'Qué hace MyBudget (1/3)',
  sectionA: {
    heading: 'Cuentas y acceso',
    bullets: [
      'Múltiples presupuestos por usuario, invitación de otros usuarios por email.',
      '4 roles (Owner/Admin/Operator/Read-only) → colaboración familiar sin perder control de quién edita qué.',
      'Login JWT, recuperación de contraseña, bloqueo tras intentos fallidos → seguridad sin fricción para uso diario.',
    ],
  },
  sectionB: {
    heading: 'Estructura de presupuesto',
    bullets: [
      'Ciclos y períodos con tipo de cambio propio por ciclo → refleja presupuestos anuales/mensuales reales, multi-moneda.',
      'Rubros con historial de revisiones sin perder la auditoría → resuelve el dolor #1 de la hoja de cálculo.',
      'Categorías y grupos reordenables → organización flexible, no columnas fijas como en Excel.',
    ],
  },
})

addDualBulletSlide({
  kicker: 'Funcionalidades',
  title: 'Qué hace MyBudget (2/3)',
  sectionA: {
    heading: 'Ejecución (gasto real)',
    bullets: [
      'Matriz multi-período con CRUD en línea → registrar gasto real sin salir de la vista comparativa.',
      'Notas de crédito/débito, tipo de cambio por entrada → maneja casos reales que una hoja de cálculo no modela bien.',
    ],
  },
  sectionB: {
    heading: 'Situación actual',
    bullets: [
      'Catálogo de cuentas bancarias + "corte" diario de saldos → saldo real vs. presupuestado/ejecutado, de un vistazo.',
      'Multi-moneda con tipo de cambio congelado (histórico) vs. transaccional (actual) → evita comparar cifras no comparables.',
    ],
  },
})

addBulletSlide({
  kicker: 'Funcionalidades',
  title: 'Qué hace MyBudget (3/3)',
  bullets: [
    'Dashboard analítico: tendencia histórica, banda de comportamiento promedio, comparación de rubros por período/ciclo → convierte datos crudos en decisión.',
    'Auditoría completa de mutaciones + log de seguridad con retención de 90 días → trazabilidad total, no negociable al manejar dinero compartido.',
    'Localización completa ES/EN → usable por toda la familia, no solo por quien lo construyó.',
    'Fuera de alcance de este TFM (MVP B): proyectos, compromisos financieros, cuotas/deudas, import/export.',
  ],
})

// Bloque 2
addTableSlide({
  kicker: 'Arquitectura y proceso',
  title: 'Stack tecnológico',
  headers: ['Capa', 'Tecnología'],
  rows: [
    ['Backend', '.NET 10 · ASP.NET Core Minimal APIs · Mediator · Dapper + EF Core · FluentValidation'],
    ['Frontend', 'Vue 3.5 (Composition API) · TypeScript · Pinia · Tailwind v4 + daisyUI · Chart.js'],
    ['Base de datos', 'PostgreSQL 16'],
    ['Pruebas', 'xUnit + NSubstitute (unit) · WebApplicationFactory (integration) · Vitest · Playwright (E2E)'],
    ['Infraestructura', 'Docker Compose (Postgres, Redis, Mailpit, Seq, Jaeger)'],
  ],
  note: 'Fuente: README.md — tabla "Tech Stack"',
})

addFullDiagramSlide({
  kicker: 'Arquitectura y proceso',
  title: 'Arquitectura — Vertical Slice Architecture',
  diagramFile: '02-arquitectura-vertical-slice-architecture.png',
  caption: 'Cada caso de uso vive en Features/<Área>/<CasoDeUso>/ con 4 archivos: request+handler, validator, endpoint, DTOs.',
})

addBulletSlide({
  kicker: 'Arquitectura y proceso',
  title: 'Proceso de desarrollo — Spec-Driven Development',
  bullets: [
    'Cada feature: exploración → propuesta → spec → diseño → tasks → implementación → verificación → archivo.',
    '23 cambios documentados de extremo a extremo en openspec/changes/archive/.',
    'Agentes de IA como herramienta dentro del proceso, dirigidos por el desarrollador — no autónomos.',
  ],
})

addBulletSlide({
  kicker: 'Arquitectura y proceso',
  title: 'Línea de tiempo — de 0 a MVP A',
  bullets: [
    '2026-07-07 — foundation: scaffold completo de la aplicación.',
    '2026-07-08 — auth: registro, login, invitaciones, RBAC.',
    '2026-07-10/11 — budget-structure + UI: ciclos, períodos, rubros, categorías.',
    '2026-07-13/14 — budget-execution + UI: matriz de ejecución.',
    '2026-07-29 — current-situation: cuentas bancarias y el "corte".',
    '2026-08-04 — dashboard: última pieza de MVP A.',
    'Total: 23 cambios archivados, ~1 mes de desarrollo.',
  ],
})

// Bloque 3 — Funcionalidades
addBulletSlide({
  kicker: 'Funcionalidades — Cuentas y acceso',
  title: 'Cuentas y acceso',
  bullets: [
    'Registro, login JWT (15 min) con refresh token rotativo (7 días).',
    'Invitación por email con rol asignado por el administrador.',
    '4 roles por presupuesto: Owner, Admin, Operator, Read-only.',
    'Recuperación de contraseña por correo, con TTL configurable.',
  ],
  diagramFile: '03-flujo-de-invitacion-de-usuario.png',
})

addImageGridSlide({
  kicker: 'Cuentas y acceso',
  title: 'Registro — formulario',
  images: [
    { dir: 'auth', file: '01-register-empty.png', caption: 'Formulario vacío' },
    { dir: 'auth', file: '02-register-filled.png', caption: 'Formulario completado' },
  ],
})

addImageGridSlide({
  kicker: 'Cuentas y acceso',
  title: 'Registro — resultado',
  images: [
    { dir: 'auth', file: '03-register-success.png', caption: 'Registro exitoso' },
    { dir: 'auth', file: '04-register-duplicate-error.png', caption: 'Error: correo duplicado' },
  ],
})

addImageGridSlide({
  kicker: 'Cuentas y acceso',
  title: 'Login y Logout',
  images: [
    { dir: 'auth', file: '05-login-empty.png', caption: 'Login — vacío' },
    { dir: 'auth', file: '06-login-success.png', caption: 'Login exitoso' },
    { dir: 'auth', file: '07-login-invalid-error.png', caption: 'Credenciales inválidas' },
    { dir: 'auth', file: '08-logout-menu.png', caption: 'Menú de usuario' },
    { dir: 'auth', file: '09-logout-success.png', caption: 'Logout exitoso' },
  ],
})

addBulletSlide({
  kicker: 'Funcionalidades — Estructura de presupuesto',
  title: 'Estructura de presupuesto',
  bullets: [
    'Ciclos (ej. un año) divididos en Períodos (ej. meses).',
    'Rubros a nivel de presupuesto, con rango de vigencia propio (fecha inicio/fin).',
    'Historial de revisiones de monto (BudgetLineRevision) — sin huecos, sin perder el rastro de auditoría.',
    'Agrupados por CategoryGroup → Category.',
  ],
  diagramFile: '04-jerarquia-del-dominio.png',
})

addImageGridSlide({
  kicker: 'Estructura de presupuesto',
  title: 'Ciclos — creación',
  images: [
    { dir: 'budget-structure-cycles', file: '01-list-empty.png', caption: 'Lista vacía' },
    { dir: 'budget-structure-cycles', file: '02-create-form.png', caption: 'Formulario de creación' },
    { dir: 'budget-structure-cycles', file: '03-create-success.png', caption: 'Creación exitosa' },
    { dir: 'budget-structure-cycles', file: '04-create-duplicate-error.png', caption: 'Error: nombre duplicado' },
  ],
})

addImageGridSlide({
  kicker: 'Estructura de presupuesto',
  title: 'Ciclos — edición y ciclo de vida',
  images: [
    { dir: 'budget-structure-cycles', file: '05-edit-form.png', caption: 'Formulario de edición' },
    { dir: 'budget-structure-cycles', file: '06-edit-success.png', caption: 'Edición exitosa' },
    { dir: 'budget-structure-cycles', file: '07-set-active-success.png', caption: 'Ciclo activado' },
    { dir: 'budget-structure-cycles', file: '08-delete-confirm.png', caption: 'Confirmación de borrado' },
    { dir: 'budget-structure-cycles', file: '09-delete-success.png', caption: 'Borrado exitoso' },
  ],
})

addImageGridSlide({
  kicker: 'Estructura de presupuesto',
  title: 'Categorías — grupos',
  images: [
    { dir: 'budget-structure-categories', file: '01-list-empty.png', caption: 'Lista vacía' },
    { dir: 'budget-structure-categories', file: '02-create-group-form.png', caption: 'Crear grupo' },
    { dir: 'budget-structure-categories', file: '03-create-group-success.png', caption: 'Creación exitosa' },
    { dir: 'budget-structure-categories', file: '04-create-group-duplicate-error.png', caption: 'Error: nombre duplicado' },
  ],
})

addImageGridSlide({
  kicker: 'Estructura de presupuesto',
  title: 'Categorías — categorías y ciclo de vida',
  images: [
    { dir: 'budget-structure-categories', file: '05-create-category-form.png', caption: 'Crear categoría' },
    { dir: 'budget-structure-categories', file: '06-create-category-success.png', caption: 'Creación exitosa' },
    { dir: 'budget-structure-categories', file: '07-create-category-duplicate-error.png', caption: 'Error: nombre duplicado' },
    { dir: 'budget-structure-categories', file: '09-delete-category-success.png', caption: 'Borrado exitoso' },
    { dir: 'budget-structure-categories', file: '10-restore-category-success.png', caption: 'Restauración exitosa' },
  ],
})

addImageGridSlide({
  kicker: 'Estructura de presupuesto',
  title: 'Períodos — creación',
  images: [
    { dir: 'budget-structure-periods-lines', file: '01-period-list-empty.png', caption: 'Lista vacía' },
    { dir: 'budget-structure-periods-lines', file: '02-period-create-form.png', caption: 'Formulario de creación' },
    { dir: 'budget-structure-periods-lines', file: '03-period-create-success.png', caption: 'Creación exitosa' },
    { dir: 'budget-structure-periods-lines', file: '04-period-create-duplicate-error.png', caption: 'Error: nombre duplicado' },
  ],
})

addImageGridSlide({
  kicker: 'Estructura de presupuesto',
  title: 'Períodos — estado y eliminación',
  images: [
    { dir: 'budget-structure-periods-lines', file: '05-period-status-form.png', caption: 'Cambio de estado' },
    { dir: 'budget-structure-periods-lines', file: '06-period-status-success.png', caption: 'Estado actualizado' },
    { dir: 'budget-structure-periods-lines', file: '07-period-delete-confirm.png', caption: 'Confirmación de borrado' },
    { dir: 'budget-structure-periods-lines', file: '08-period-delete-success.png', caption: 'Borrado exitoso' },
  ],
})

addImageGridSlide({
  kicker: 'Estructura de presupuesto',
  title: 'Rubros — creación',
  images: [
    { dir: 'budget-structure-periods-lines', file: '09-line-list-empty.png', caption: 'Lista vacía' },
    { dir: 'budget-structure-periods-lines', file: '10-line-create-form.png', caption: 'Formulario de creación' },
    { dir: 'budget-structure-periods-lines', file: '11-line-create-success.png', caption: 'Creación exitosa' },
    { dir: 'budget-structure-periods-lines', file: '12-line-create-duplicate-error.png', caption: 'Error: nombre duplicado' },
  ],
})

addImageGridSlide({
  kicker: 'Estructura de presupuesto',
  title: 'Rubros — edición y eliminación',
  images: [
    { dir: 'budget-structure-periods-lines', file: '13-line-edit-inline.png', caption: 'Edición en línea' },
    { dir: 'budget-structure-periods-lines', file: '14-line-edit-success.png', caption: 'Edición exitosa' },
    { dir: 'budget-structure-periods-lines', file: '15-line-delete-confirm.png', caption: 'Confirmación de borrado' },
    { dir: 'budget-structure-periods-lines', file: '16-line-delete-success.png', caption: 'Borrado exitoso' },
  ],
})

addBulletSlide({
  kicker: 'Funcionalidades — Ejecución',
  title: 'Ejecución (gasto real)',
  bullets: [
    'Matriz multi-período con CRUD en línea sobre cada rubro.',
    'Notas de crédito y débito, además de gastos regulares.',
    'Tipo de cambio por entrada — cada ejecución guarda su propia tasa.',
    'Toggle de moneda para ver la matriz completa en GTQ o USD.',
  ],
  diagramFile: '05-flujo-de-registro-de-ejecucion.png',
})

addImageGridSlide({
  kicker: 'Ejecución',
  title: 'Matriz — vista y apertura',
  images: [
    { dir: 'budget-execution', file: '01-matrix-view.png', caption: 'Vista de la matriz' },
    { dir: 'budget-execution', file: '02-open-execution-modal.png', caption: 'Modal de ejecución' },
  ],
})

addImageGridSlide({
  kicker: 'Ejecución',
  title: 'Matriz — validación',
  images: [
    { dir: 'budget-execution', file: '03-create-validation-error.png', caption: 'Error de validación' },
    { dir: 'budget-execution', file: '04-create-form-filled.png', caption: 'Formulario completado' },
  ],
})

addImageGridSlide({
  kicker: 'Ejecución',
  title: 'Matriz — creación exitosa',
  images: [
    { dir: 'budget-execution', file: '05-create-success.png', caption: 'Creación exitosa' },
    { dir: 'budget-execution', file: '06-matrix-updated.png', caption: 'Matriz actualizada' },
  ],
})

addImageGridSlide({
  kicker: 'Ejecución',
  title: 'Matriz — organización',
  images: [
    { dir: 'budget-execution', file: '09-collapse-group.png', caption: 'Grupo colapsado' },
  ],
})

addImageGridSlide({
  kicker: 'Ejecución',
  title: 'Matriz — moneda',
  images: [
    { dir: 'budget-execution', file: '07-currency-toggle-usd.png', caption: 'Vista en USD' },
    { dir: 'budget-execution', file: '08-currency-toggle-gtq.png', caption: 'Vista en GTQ' },
  ],
})

addImageGridSlide({
  kicker: 'Ejecución',
  title: 'Matriz — eliminación',
  images: [
    { dir: 'budget-execution', file: '10-delete-confirm.png', caption: 'Confirmación de borrado' },
    { dir: 'budget-execution', file: '11-delete-success.png', caption: 'Borrado exitoso' },
  ],
})

addBulletSlide({
  kicker: 'Funcionalidades — Situación actual',
  title: 'Situación actual — el "corte"',
  bullets: [
    'Catálogo de cuentas bancarias por presupuesto.',
    'Snapshot diario de saldos vs. presupuestado/ejecutado — el "corte".',
    'Soporte multi-moneda con tipo de cambio por corte.',
    'Los totales se persisten al momento de guardar — no se recalculan retroactivamente si cambian ejecuciones posteriores.',
  ],
})

addImageGridSlide({
  kicker: 'Situación actual',
  title: 'Cuentas bancarias — creación',
  images: [
    { dir: 'bank-accounts', file: '01-list-empty.png', caption: 'Lista vacía' },
    { dir: 'bank-accounts', file: '02-create-form.png', caption: 'Formulario de creación' },
  ],
})

addImageGridSlide({
  kicker: 'Situación actual',
  title: 'Cuentas bancarias — resultado',
  images: [
    { dir: 'bank-accounts', file: '03-create-success.png', caption: 'Creación exitosa' },
    { dir: 'bank-accounts', file: '04-create-duplicate-error.png', caption: 'Error: alias duplicado' },
  ],
})

addImageGridSlide({
  kicker: 'Situación actual',
  title: 'Cuentas bancarias — mostrar eliminadas',
  images: [
    { dir: 'bank-accounts', file: '09-show-deleted-toggle.png', caption: 'Mostrar eliminadas' },
  ],
})

addImageGridSlide({
  kicker: 'Situación actual',
  title: 'Cuentas bancarias — edición y ciclo de vida',
  images: [
    { dir: 'bank-accounts', file: '05-edit-form.png', caption: 'Formulario de edición' },
    { dir: 'bank-accounts', file: '06-edit-success.png', caption: 'Edición exitosa' },
    { dir: 'bank-accounts', file: '07-delete-confirm.png', caption: 'Confirmación de borrado' },
    { dir: 'bank-accounts', file: '10-restore-success.png', caption: 'Restauración exitosa' },
  ],
})

addImageGridSlide({
  kicker: 'Situación actual',
  title: 'Corte — borrador y formulario',
  images: [
    { dir: 'current-situation', file: '01-draft-form.png', caption: 'Borrador' },
    { dir: 'current-situation', file: '02-form-filled.png', caption: 'Formulario completado' },
  ],
})

addImageGridSlide({
  kicker: 'Situación actual',
  title: 'Corte — guardado',
  images: [
    { dir: 'current-situation', file: '03-save-error.png', caption: 'Error al guardar' },
    { dir: 'current-situation', file: '04-save-success.png', caption: 'Guardado exitoso' },
  ],
})

addImageGridSlide({
  kicker: 'Situación actual',
  title: 'Corte — confirmar eliminación',
  images: [
    { dir: 'current-situation', file: '05-delete-confirm-empty.png', caption: 'Confirmación (vacía)' },
    { dir: 'current-situation', file: '06-delete-confirm-typed.png', caption: 'Fecha escrita' },
  ],
})

addImageGridSlide({
  kicker: 'Situación actual',
  title: 'Corte — eliminación exitosa',
  images: [
    { dir: 'current-situation', file: '07-delete-success.png', caption: 'Borrado exitoso' },
  ],
})

addBulletSlide({
  kicker: 'Funcionalidades — Dashboard',
  title: 'Dashboard analítico',
  bullets: [
    'Tendencia histórica de los totales del "corte" a lo largo del tiempo.',
    'Banda de comportamiento promedio (mínimo/máximo/promedio por período).',
    'Comparación de rubros por período y por ciclo, dentro y entre ciclos.',
    'Manejo explícito de tipo de cambio: congelado al corte vs. transaccional — nunca mezclados.',
  ],
})

addSingleImageSlide({
  kicker: 'Dashboard',
  title: 'Tendencia histórica',
  dir: 'dashboard', file: '01-lifetime-trend.png',
  caption: 'Tendencia histórica de los totales del corte, con selector de series.',
})

addSingleImageSlide({
  kicker: 'Dashboard',
  title: 'Selector de series — estado vacío',
  dir: 'dashboard', file: '02-series-picker-empty.png',
  caption: 'Al deseleccionar todas las series, el gráfico muestra un estado vacío explícito — prueba de que el selector controla el gráfico.',
})

addSingleImageSlide({
  kicker: 'Dashboard',
  title: 'Historial insuficiente',
  dir: 'dashboard', file: '06-insufficient-history.png',
  caption: 'Con menos de 2 cortes, se muestra un estado vacío explícito en vez de una banda calculada engañosa.',
})

addSingleImageSlide({
  kicker: 'Dashboard',
  title: 'Comparación de rubros — sin selección',
  dir: 'dashboard', file: '03-budget-line-empty.png',
  caption: 'Estado vacío antes de elegir un rubro y períodos a comparar.',
})

addSingleImageSlide({
  kicker: 'Dashboard',
  title: 'Comparación de rubros — seleccionado',
  dir: 'dashboard', file: '04-budget-line-selected.png',
  caption: 'Rubro y 2 períodos seleccionados — comparación período a período dentro del ciclo.',
})

addSingleImageSlide({
  kicker: 'Dashboard',
  title: 'Modo entre ciclos',
  dir: 'dashboard', file: '05-cross-cycle-mode.png',
  caption: 'El selector de períodos cambia por un selector de ciclos para comparar entre ciclos distintos.',
})

addImageGridSlide({
  kicker: 'Funcionalidades — Gestión de presupuestos',
  title: 'Presupuestos — lista y creación',
  images: [
    { dir: 'budget-management', file: '01-budget-list.png', caption: 'Lista de presupuestos' },
    { dir: 'budget-management', file: '02-create-form.png', caption: 'Formulario de creación' },
  ],
})

addImageGridSlide({
  kicker: 'Gestión de presupuestos',
  title: 'Presupuestos — validación y éxito',
  images: [
    { dir: 'budget-management', file: '03-create-duplicate-error.png', caption: 'Error: nombre duplicado' },
    { dir: 'budget-management', file: '04-create-success.png', caption: 'Creación exitosa' },
  ],
})

addImageGridSlide({
  kicker: 'Gestión de presupuestos',
  title: 'Presupuestos — eliminados y restauración',
  images: [
    { dir: 'budget-management', file: '07-show-deleted-toggle.png', caption: 'Mostrar eliminados' },
    { dir: 'budget-management', file: '08-restore-success.png', caption: 'Restauración exitosa' },
  ],
})

addImageGridSlide({
  kicker: 'Gestión de presupuestos',
  title: 'Invitaciones — resultado',
  images: [
    { dir: 'budget-management', file: '09-invite-accept-success.png', caption: 'Invitación aceptada' },
    { dir: 'budget-management', file: '10-invite-accept-error.png', caption: 'Token inválido' },
  ],
  imageScale: 0.6,
})

// Bloque 4
addTableSlide({
  kicker: 'Calidad y despliegue',
  title: 'Pruebas automatizadas',
  headers: ['Capa', 'Cifras (ejemplo: cambio dashboard)'],
  rows: [
    ['Backend — unitarias', '488 pruebas (xUnit + NSubstitute + Shouldly)'],
    ['Backend — integración', '275/278 pruebas (Postgres real vía WebApplicationFactory)'],
    ['Frontend — unitarias', '619 pruebas (Vitest + Testing Library)'],
    ['End-to-end', '115 pruebas (Playwright, navegador real)'],
  ],
  note: 'Complemento cualitativo: TDD estricto en todo el ciclo, 0 issues CRITICAL en las verificaciones finales de cada uno de los 23 cambios. Fuente: openspec/ROADMAP.md.',
})

addDualBulletSlide({
  kicker: 'Calidad y despliegue',
  title: 'Observabilidad y despliegue',
  sectionA: {
    heading: 'Observabilidad',
    bullets: [
      'Logs estructurados: Serilog + Seq.',
      'Trazas distribuidas: OpenTelemetry + Jaeger.',
      'Auditoría de mutaciones (AuditLog) y eventos de seguridad (SecurityAuditLog), con retención de 90 días.',
    ],
  },
  sectionB: {
    heading: 'Despliegue en producción',
    bullets: [
      'Hetzner (VPS) + Caddy (reverse proxy / TLS automático) + Brevo (correo transaccional real).',
      'URL pública: mybudget-aras.duckdns.org',
      'Lecciones aprendidas documentadas en docs/Deployment-LessonLearned.md.',
    ],
  },
})

addBulletSlide({
  kicker: 'Calidad y despliegue',
  title: 'Seguridad y control de acceso',
  bullets: [
    'RBAC de 4 niveles: Owner, Admin, Operator, Read-only — por presupuesto.',
    'JWT de corta duración (15 min) + refresh token rotativo (7 días).',
    'Bloqueo de cuenta tras 5 intentos fallidos de login (30 min).',
    'Historial de las últimas 5 contraseñas — no se pueden reutilizar.',
  ],
})

// Bloque 5
addBulletSlide({
  kicker: 'Cierre',
  title: 'Estado y alcance futuro (MVP B)',
  bullets: [
    'MVP A completo: 23 cambios shippeados y archivados.',
    'MVP B planeado (no iniciado): proyectos, compromisos financieros, cuotas/deudas, import/export.',
    'Alcance controlado deliberadamente para este TFM — MVP B queda como trabajo futuro.',
  ],
})

addClosingSlide()

// ---------------------------------------------------------------------------

fs.mkdirSync(path.dirname(OUT), { recursive: true })
await pptx.writeFile({ fileName: OUT })
console.log(`Wrote ${OUT}`)
