# Formato de paquete para importar problemas

Especificación del `.zip` que acepta `POST /api/problems/import`. Basado en el
[ICPC Problem Package Format](https://icpc.io/problem-package-format), con
extensiones propias documentadas abajo. Ver también `ARCHITECTURE.md` (sección
de la API) para el resumen de brechas frente al estándar puro.

## Endpoint

- `POST /api/problems/import` — multipart/form-data, campo `file` con el `.zip`.
- Rol requerido: **Administrador** (más estricto que el resto de `/problems`,
  que también permite Docente/Auxiliar).
- Tamaño máximo: 200 MB.
- El import **siempre crea un problema nuevo** — no hay modo "sobrescribir un
  problema existente". Si el zip fue exportado desde este mismo sistema, el
  resultado es un duplicado con un `problem_id` distinto.

## Estructura del zip

```
problem.yaml          (recomendado)
metadata.json         (opcional — propio de este sistema, ver abajo)
data/
  sample/
    1.in
    1.out            (o 1.ans)
    2.in
    2.out
  secret/
    1.in
    1.out            (o 1.ans)
    ...
statement/
  es/
    problem.html      (opcional — propio de este sistema)
    problem.md         (opcional, usado solo si no hay metadata.json)
    img/
      1.png
      ...
```

Ningún archivo es estrictamente obligatorio: un zip vacío produce un problema
con título "Imported problem", sin samples, límites por defecto (1s / 128MB) y
sin datos de prueba. En la práctica siempre se espera al menos `problem.yaml`
y `data/sample/`.

## Dos modos de lectura

El importador decide el modo según la presencia de `metadata.json`:

### 1. Paquete propio (con `metadata.json`)

Es el modo usado al reimportar un `.zip` exportado por este mismo sistema
(`GET /api/problems/{id}/export`). `metadata.json` es la fuente de verdad —
se ignora cualquier statement HTML/Markdown para poblar el problema, y se usan
directamente estos campos:

| Campo (`metadata.json`)  | Tipo         | Va a                        | Si falta        |
|---------------------------|--------------|------------------------------|------------------|
| `title`                   | string       | `Problem.Title`              | "Imported problem" |
| `description`             | string (HTML)| `Problem.Description`        | vacío            |
| `input`                   | string (HTML)| `Problem.Input`               | vacío            |
| `output`                  | string (HTML)| `Problem.Output`              | vacío            |
| `hint`                    | string (HTML)| `Problem.Hint`                | vacío            |
| `source`                  | string       | `Problem.Source`              | vacío            |
| `originSource`            | string       | `Problem.OriginSource`        | vacío            |
| `defunct`                 | "Y"/"N"      | `Problem.Defunct`             | "N"              |
| `timeLimit`               | int (segundos)| `Problem.TimeLimit`          | 1                |
| `memoryLimit`             | int (MB)     | `Problem.MemoryLimit`         | 128              |
| `classificationIds`       | int[]        | `Problem.Classifications`     | `[]`             |

`statement/es/problem.html` y `problem.md` se ignoran por completo en este
modo (son solo una vista de respaldo para lectores humanos/herramientas ICPC,
no se reimportan). El problema importado siempre queda con `Spj = "N"` — no
existe forma de importar un juez especial.

### 2. Paquete ICPC genuino (sin `metadata.json`)

Para zips de terceros que siguen el estándar ICPC real, sin la extensión
propia. Se leen, best-effort, campos limitados:

| Origen                              | Va a               | Si falta          |
|--------------------------------------|--------------------|--------------------|
| `problem.yaml` → `name`              | `Problem.Title`    | "Imported problem" |
| `problem.yaml` → `source`            | `Problem.Source`   | "ICPC import"      |
| `problem.yaml` → `limits.time_limit` | `Problem.TimeLimit`| 1                  |
| `problem.yaml` → `limits.memory`     | `Problem.MemoryLimit` | 128             |
| `statement/<lang>/problem.md`        | `Problem.Description` | vacío           |

`Input`, `Output`, `Hint` y clasificaciones quedan vacíos siempre en este
modo — el formato ICPC estándar no separa el statement en esas secciones. El
parser de `problem.yaml` es intencionalmente simple (no es un parser YAML
completo): solo entiende `name`, `source`, y `limits.time_limit`/`limits.memory`
en el nivel exacto que produce el propio exportador. Un `problem.yaml` con
estructura distinta a la mostrada arriba puede no leerse correctamente.

## `data/sample/` y `data/secret/`

- Los archivos se emparejan por nombre: `<n>.in` con `<n>.out` **o** `<n>.ans`
  (se acepta cualquiera de los dos; si ambos existen, gana `.out`).
- El orden de los samples es numérico por el nombre del archivo (`1.in`,
  `2.in`, ..., `10.in`), no alfabético.
- `data/sample/` se convierte en los `ProblemSample` del problema (visibles
  públicamente). `data/secret/` se escribe tal cual al almacenamiento de casos
  de prueba del juez (`{problem_id}/<n>.in|.out`) — si el archivo original
  traía extensión `.ans`, se renombra a `.out` al guardarlo.
- Cualquier archivo dentro de `data/secret/` que no siga el patrón `<n>.in`/
  `<n>.out`/`<n>.ans` se copia igual, sin validación de contenido.

## `statement/<lang>/img/`

Solo se usan cuando el zip trae `metadata.json` **no** aplica (ver arriba: en
modo paquete propio las imágenes ya están embebidas en el HTML de
`metadata.json` como corresponda). En modo ICPC genuino, las imágenes
referenciadas por `problem.md` no se reprocesan — el importador no reescribe
rutas relativas a `img/`.

## Restricciones

- **Nunca** se puede importar directamente un binario `spj` (juez especial) —
  el campo `Spj` del problema resultante siempre es `"N"`. Si necesitas un
  juez especial, configúralo manualmente después de importar.
- El import no valida que `data/secret/` tenga al menos un caso de prueba, ni
  que los límites de tiempo/memoria sean razonables — es responsabilidad de
  quien prepara el zip.
- Si `problem.yaml`/`metadata.json` tienen JSON o YAML mal formado, el import
  falla con un error 400 (no crea un problema parcial).

## Exportar para volver a importar

La forma más confiable de generar un zip válido es exportar un problema
existente (`GET /api/problems/{id}/export`, rol Administrador) y usar ese
mismo zip como plantilla — garantiza la estructura exacta que el importador
espera en modo "paquete propio". Problemas con `Spj=Y` no se pueden exportar
(brecha conocida, ver `ARCHITECTURE.md`).
