# Distribucion

Esta carpeta contiene los scripts para crear las dos formas de entrega de la aplicacion para Windows de 64 bits.

El archivo `logo.ico` se usa como icono del ejecutable, del instalador y de sus accesos directos.

## Requisitos

- Windows.
- .NET 10 SDK para compilar y publicar la aplicacion.
- [Inno Setup 6](https://jrsoftware.org/isdl.php) solo para crear el instalador.

## Version portable

Ejecute `generar-portable.bat` con doble clic o desde PowerShell:

```powershell
.\Distribucion\generar-portable.bat
```

La aplicacion autocontenida se crea en `Distribucion\salida\portable`. Comprima **el contenido** de esa carpeta en un ZIP para compartir la version portable. El usuario no necesita tener .NET instalado.

## Instalador

Instale Inno Setup 6 y ejecute:

```powershell
.\Distribucion\generar-instalador.bat
```

El script genera primero la version portable y despues crea `Distribucion\salida\instalador\Comparador-Setup.exe`.

El instalador se ejecuta con permisos de administrador, instala la aplicacion en `Program Files`, crea una entrada para desinstalarla y permite crear un acceso directo en el Escritorio.

## Antes de una nueva entrega

Actualice `MyAppVersion` en `Comparador.iss`. Para distribuir publicamente, tambien es recomendable firmar el `Setup.exe` con un certificado de firma de codigo.
