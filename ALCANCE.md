# Alcance — Comparador de Bases de Datos

## Objetivo

Aplicación de escritorio en .NET para comparar la estructura de una base de datos origen con una base de datos destino y generar un script SQL de actualización. La primera versión estará orientada a SQL Server.

La herramienta **solo genera el script**: no ejecuta cambios directamente sobre la base de datos destino.

## Conexiones

La interfaz permitirá configurar dos conexiones independientes:

- **Base origen:** representa la estructura de referencia.
- **Base destino:** representa la estructura que se evaluará y, si corresponde, actualizará mediante el script generado.

Cada conexión incluirá servidor, usuario, contraseña y base de datos. La aplicación usará siempre autenticación de SQL Server mediante usuario y contraseña. Antes de continuar, el usuario podrá probar cada conexión y seleccionar el esquema correspondiente.

## Estructura que se compara

El alcance se limita a la definición de las tablas y sus columnas:

- Tablas presentes en la base origen y ausentes en la base destino.
- Columnas presentes en una tabla origen y ausentes en su equivalente destino.
- Nombre de columna.
- Posición u orden de columna.
- Tipo de dato, longitud, precisión y escala.
- Nulabilidad (`NULL` o `NOT NULL`).
- Propiedad `IDENTITY`, cuando exista.

El usuario podrá activar o desactivar de forma independiente la detección de tablas faltantes y columnas faltantes. Solo las opciones seleccionadas se incluirán en el resultado y el script generado.

## Filtro de tablas

Antes de comparar, el usuario podrá excluir tablas de ambas bases mediante patrones de nombre separados por comas. El selector permitirá aplicar cada patrón según una de estas condiciones:

- El nombre **empieza con** el patrón.
- El nombre **termina con** el patrón.
- El nombre **contiene** el patrón.

Por ejemplo, con los patrones `rh, pl` y la condición **empieza con**, se omitirán `rh_empleados` y `pl_planillas`. La coincidencia no distinguirá mayúsculas de minúsculas y se eliminarán espacios alrededor de cada patrón. Las tablas excluidas no se considerarán para detectar tablas o columnas faltantes, ni para generar instrucciones SQL o avisos. El resultado mostrará el total de tablas omitidas, separado por base origen y destino. Si no se ingresan patrones, se compararán todas las tablas.

## Fuera de alcance

La primera versión no comparará ni generará scripts para:

- Índices.
- Claves primarias, foráneas o únicas.
- Diferencias de valores por defecto existentes.
- Triggers.
- Vistas, procedimientos almacenados, funciones ni permisos.
- Datos distintos entre ambas bases.
- Eliminación de tablas o columnas que existan solo en la base destino.
- Ejecución directa de los scripts generados.

## Resultado generado

El resultado será exclusivamente un archivo o texto SQL, listo para copiar o guardar con extensión `.sql`.

Todo script generado se envolverá en una transacción y tendrá control de errores:

```sql
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    -- Cambios generados por el comparador.

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
```

## Estrategias de generación

### 1. Agregar columnas

Para diferencias que no requieran mantener el orden de columnas, el script usará `ALTER TABLE ... ADD`.

```sql
ALTER TABLE [dbo].[Pedido]
ADD [FechaEnvio] DATETIME2 NULL;
```

Esta alternativa agrega las columnas al final de la tabla.

Cuando una columna nueva es `NOT NULL`, el generador toma su `DEFAULT` desde la base origen y lo aplica con `WITH VALUES`. Si la columna origen no tiene `DEFAULT`, el generador usa una constante compatible con el tipo (por ejemplo, `0`, `''` o `N''`) y deja un aviso en el script. Las columnas `IDENTITY` también se agregan con `ALTER TABLE`; SQL Server asignará sus valores a las filas existentes sin garantizar su orden.

### 2. Reconstruir tabla para conservar el orden

Para igualar exactamente el orden de columnas de la base origen, la tabla destino se reconstruye: se crea una tabla auxiliar con la estructura objetivo, se copian los datos, se renombra la tabla original como respaldo temporal, se renombra la nueva tabla y finalmente se elimina el respaldo.

Esta estrategia conserva nombre, tipo, nulabilidad y valores por defecto. No replica `IDENTITY`, índices, claves, triggers ni otros atributos. Si existen columnas solo en la base destino, la tabla se omite para no eliminarlas de forma implícita.

## Interfaz de usuario base

La ventana principal se organizará en tres áreas:

1. **Conexiones:** tarjetas lado a lado para base origen y base destino, con prueba de conexión.
2. **Alcance de comparación:** selección de esquema origen y destino, filtro opcional de tablas por nombre y método de generación.
3. **Resultado SQL:** panel de previsualización del script generado, con acciones para copiarlo o guardarlo como `.sql`.

La interfaz no ejecutará scripts ni mostrará un mecanismo de sincronización automática.
