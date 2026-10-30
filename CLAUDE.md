# GlamSpace-Back — instrucciones para agentes de IA

Antes de cualquier tarea en este repositorio:

1. Lee `memoria/MEMORIA.md` (contexto y estado vigente del proyecto) y el sprint actual en `memoria/sprint/`.
2. Sigue `memoria/REGLAS.md`. Lo más importante:
   - Cada cambio de base de datos lleva script idempotente en `Database/`, migración en
     `GlamSpaces.Migrations/Migraciones/`, pruebas, `.\scripts\generar-bd-completa.ps1` (regenera
     `memoria/bd/GlamSpaces_BD_Completa.sql`), ajuste del workflow, evidencias y documentación.
   - Nunca ejecutes nada contra Azure (`glamspaces-db`): solo LocalDB o el contenedor del workflow.
   - Commits `RE_SprintN: <mensaje>` sin `Co-Authored-By` ni menciones a una IA. Push y merge solo si se piden.
   - Sin credenciales en el repo; no subas `GlamSpaces.Api/Properties/`.
3. Al terminar una HU o cambio, actualiza `memoria/sprint/sprintN.md` y la bitácora de `memoria/MEMORIA.md`.

Responde y comenta el código en español.
