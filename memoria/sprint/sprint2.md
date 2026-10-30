# Sprint 2 — CRUD de salones y paquetes

**Fechas:** hasta 2026-10-09 · **Estado:** ✅ en producción (después reestructurado en el Sprint 2.5)

## Historias

| HU | Qué | Quién | Capa |
|---|---|---|---|
| HU-05 | Modelo de salones, paquetes y fotos | Equipo | BD |
| HU-06 | CRUD de salones y paquetes del administrador | Adrian | Backend |
| HU-07 | Formulario para publicar salón | Erick | Frontend |

## Lo que se hizo

- **Tablas** `Salones`, `Paquetes`, `FotosSalon` (script completo original del equipo, con semilla de 2 salones).
- **HU-06** con EF: crear salón (nace `no_publicado`), obtener detalle con paquetes y fotos, actualizar y
  publicar, agregar/actualizar/eliminar paquetes. Healthcheck `/health`.
- **Reglas de negocio:** solo el dueño modifica su salón (`2002`); publicar exige al menos un paquete
  (`2003`); no se puede borrar el último paquete de un salón publicado (`3002`); precio y capacidad > 0.
- **HU-07 (front):** el admin llena datos y paquetes y publica (crear salón → crear paquetes → actualizar a
  `publicado`), con reintento sin duplicar.

## PRs

| PR | Ramas | Qué |
|---|---|---|
| #11 | Sprint2_Adrian → develop | HU-06 |
| #12, #13 | develop → release → main | Modelos y DbContext de Salones y Paquetes |

## Pendientes que dejó

- El profesor pidió reestructurar todo el backend → Sprint 2.5.
