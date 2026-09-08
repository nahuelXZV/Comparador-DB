# Comparador de Bases de Datos

Aplicación de escritorio WPF en .NET 10 para comparar la estructura de una base de datos origen con una base destino y generar un script SQL transaccional.

La aplicación genera el script, pero **no lo ejecuta** contra la base destino.

## Estado actual

Las dos estrategias de generación están implementadas: **Actualización segura** y **Reconstruir tabla**.

La interfaz permite:

- Configurar una conexión origen y una conexión destino.
- Conectarse mediante usuario y contraseña.
- Cargar las bases de datos disponibles y los esquemas de cada conexión.
- Comparar tablas y columnas entre los esquemas seleccionados.
- Elegir si se comparan tablas faltantes, columnas faltantes o ambas opciones.
- Omitir tablas por patrones de nombre, seleccionando si el nombre empieza, termina o contiene cada patrón.
- Elegir el método de generación del script.
- Copiar o guardar el script como archivo `.sql`.

## Actualización segura

Este modo genera un script con `TRY/CATCH`, `BEGIN TRANSACTION`, `COMMIT` y `ROLLBACK`.

Comportamiento:

- Genera `CREATE TABLE` para tablas que existen en origen y no en destino.
- Genera `ALTER TABLE ... ADD` para columnas faltantes en tablas existentes.
- Conserva tipo, longitud, precisión, escala, nulabilidad e `IDENTITY` de las columnas leídas desde origen.
- Si una columna nueva es `NOT NULL`, usa el `DEFAULT` de la columna origen y aplica `WITH VALUES` para completar las filas existentes.
- Si esa columna no tiene `DEFAULT`, crea una constante compatible con su tipo, por ejemplo `0`, `''`, `N''`, `0x` o una fecha base. El script agrega un aviso para que el usuario lo revise.
- Si una columna nueva es `IDENTITY`, SQL Server asigna valores a las filas existentes. El script muestra un aviso porque el orden de esos valores no está garantizado.

No se comparan ni sincronizan índices, claves, triggers, permisos, vistas, procedimientos almacenados ni diferencias de datos.

## Filtro de tablas

En **Alcance de comparación**, el campo **Patrones** permite excluir tablas de ambas bases antes de comparar. Escriba uno o más valores separados por comas, por ejemplo `rh, pl`.

Seleccione cómo debe coincidir el nombre de la tabla:

- **Empieza con:** `rh` omite `rh_empleados`.
- **Termina con:** `log` omite `ventas_log`.
- **Contiene:** `temp` omite `clientes_temp_historial`.

Las coincidencias no distinguen mayúsculas de minúsculas. Las tablas omitidas no producen diferencias, instrucciones SQL ni avisos; el resumen informa el total excluido en origen y destino. Si el campo está vacío, se comparan todas las tablas como antes.

## Estrategias de generación

La generación usa el patrón Strategy:

```text
IScriptGenerationStrategy
├── SafeUpdateScriptGenerationStrategy      Implementada
└── RebuildTableScriptGenerationStrategy    Implementada
```

`SafeUpdateScriptGenerationStrategy` genera cambios aditivos. `RebuildTableScriptGenerationStrategy` crea una tabla auxiliar con el orden de origen, copia los datos y sustituye la tabla anterior dentro de la transacción. Este segundo modo conserva únicamente nombre, tipo, nulabilidad y valores por defecto; no replica `IDENTITY`, índices, claves ni triggers.

## Estructura

```text
src/
├── Application/      Comparación, modelos de aplicación y estrategias SQL
├── Domain/           Definiciones de tablas y columnas
├── Infrastructure/   Lectura de metadatos y conexiones a SQL Server
└── Desktop/          Interfaz WPF y ViewModels
```

## Requisitos

- Windows.
- .NET 10 SDK.
- SQL Server accesible desde el equipo.
- Un usuario con permiso de conexión y lectura de metadatos en ambas bases.

Para usar el diseñador visual WPF se requiere Visual Studio con la carga de trabajo **Desarrollo de escritorio con .NET** y el componente de .NET 10.

## Compilar y ejecutar

Desde la carpeta raíz del proyecto:

```powershell
dotnet build Comparador.slnx
dotnet run --project src/Desktop/Desktop.csproj
```

También se puede abrir `Comparador.slnx` en Visual Studio y establecer `Desktop` como proyecto de inicio.

## Limitaciones conocidas

- La reconstrucción se omite si la tabla destino contiene columnas que no existen en origen, para no eliminarlas silenciosamente.
- Los valores automáticos para columnas `NOT NULL` deben revisarse antes de ejecutar el script en un entorno productivo.
- La aplicación no almacena contraseñas: solo se mantienen en memoria durante la sesión.

Consulta [ALCANCE.md](ALCANCE.md) para la definición funcional completa.
