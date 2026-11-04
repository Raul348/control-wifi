# Automatización de Tekla Structures y Revit con C#

Programas para aprender a automatizar **Tekla Structures** (Open API) y **Revit** (Revit API) en C#.

| Programa | Para | Qué hace |
|---|---|---|
| [Tekla Piezas](#tekla-piezas) | Tekla Structures 2023 a 2026 | Lee el modelo, lista las piezas, hace el metrado y revisa errores antes de numerar |
| [Perfiles AISC](#perfiles-aisc-para-revit) | Revit 2023 a 2027 | Catálogo de perfiles de acero con la interfaz de Tekla, y colocación de vigas y columnas |

## Perfiles AISC para Revit

Complemento de Revit con la misma ventana **Seleccionar perfil** de Tekla:

- **Árbol de perfiles** ordenado como en Tekla: *Perfiles I → W → W6 → W6X8.5*, con HP, W, M, S; ángulos L y 2L;
  canales C y MC; perfiles T (WT, MT, ST); tubos redondos (HSS y Pipe) y tubos rectangulares (HSS).
- **Nombre del perfil** con autocompletar y **filtro** con comodines (`W6X*`, `*X1/4`).
- **Pestaña General**: tipo y subtipo, área, **figura de la sección a escala** con las letras h, b, s, t y r1,
  y tabla de medidas: altura, ancho, espesor de alma, espesor de ala, radio, k, centro de gravedad...
- **Pestaña Análisis**: área, peso por metro, inercias, módulos elásticos y plásticos, radios de giro, J y Cw.
- **Unidades**: milímetros o pulgadas.
- **Colocar viga** y **Colocar columna**: elija los puntos en el modelo; Esc termina y vuelve a la ventana.
  Si el tipo no existe en el proyecto, el programa lo **crea con las medidas del catálogo**; si la familia no está cargada,
  la busca en la biblioteca de Revit.
- **Colocar viga curva**: dos extremos y la flecha del arco. Arco **vertical** (techo curvo) u **horizontal** (curva en planta).
- **Copiar datos**: copia las tablas al portapapeles para pegarlas en Excel.

### Nave curva
El botón **Nave curva** genera una nave industrial completa con cubierta en arco a partir de:
luz, altura de columnas, flecha del arco, número de pórticos y separación.

- **Tipo de arco**: **viga curva** (un arco por pórtico, para perfiles curvados), **tramos rectos** (perfiles W que siguen el arco)
  o **celosía en arco** (cordón superior e inferior paralelos, montantes verticales y diagonales), con el canto que indique.
- **Correas** sobre el arco, **vigas de amarre** entre las cabezas de las columnas y **arriostres en cruz** en los vanos extremos,
  en las paredes y en la cubierta.
- **Perfiles** de cada grupo elegidos del catálogo: columnas, arco o cordones, correas y amarres, diagonales y arriostres.
- La figura muestra el pórtico tipo con sus cotas, y abajo el resumen de barras y longitudes.
- En el modelo se eligen dos puntos: la primera columna y la dirección a lo largo de la nave. Al final se muestra el
  resumen con el peso aproximado del acero.

Los datos son la base de perfiles **AISC edición 13** (1.575 perfiles), incrustada en el programa: no necesita archivos aparte.

### Instalación
1. En GitHub, abra **Actions → Compilar programas → la última ejecución**.
2. Descargue el artefacto de su versión, por ejemplo `PerfilesAisc-Revit2025`.
3. Copie su contenido en `%AppData%\Autodesk\Revit\Addins\2025\` para que quede así:
   ```
   Addins\2025\PerfilesAisc.addin
   Addins\2025\PerfilesAisc\PerfilesAisc.dll
   Addins\2025\PerfilesAisc\PerfilesAisc.Core.dll
   ```
4. Abra Revit y acepte **Cargar siempre**. El botón **Perfiles AISC** aparece en la pestaña **ACERO**.

Para compilarlo usted mismo: `.\build-revit.ps1` y `.\install-revit.ps1` (igual que en ACERO Refuerzo).
Revit 2023 y 2024 usan .NET Framework 4.8; 2025 y 2026, .NET 8; 2027, .NET 10.

### Estructura
```
src/PerfilesAisc.Core/   Catálogo: lectura de datos, árbol, unidades y filtro (se prueba sin Revit)
  Datos/perfiles_aisc.csv  Base AISC generada con tools/convertir_aisc.py desde el Excel de AISC
src/PerfilesAisc/        Complemento de Revit
  App.cs                 Botón en la cinta
  CmdPerfiles.cs         Abre la ventana y coloca las piezas
  UI/PerfilForm.cs       Ventana "Seleccionar perfil"
  UI/Seccion.cs          Dibujo de la sección con sus cotas
  UI/NaveForm.cs         Ventana de la nave curva con la figura del pórtico
  Revit/Colocador.cs     Busca o crea el tipo en Revit; coloca vigas, vigas curvas, columnas y la nave completa
src/PerfilesAisc.Core/Nave.cs   Geometría de la nave: arco, tramos, celosía, correas y arriostres (con pruebas)
```

## Tekla Piezas

Se compila para Tekla 2023, 2024, 2025 y 2026.
Se avanza por pasos, de lo simple a lo complejo. El código está comentado en español.

| Paso | Programa | Estado |
|---|---|---|
| 1 | Lee el modelo, lista las piezas, hace el metrado y revisa errores antes de numerar | Listo para probar |
| 2 | Crear piezas: columnas y vigas desde una ventana | Pendiente |
| 3 | Sustitución de perfiles por los disponibles en Perú | Pendiente |
| 4 | Primer plugin: escalera marinera | Pendiente |
| 5 | Uniones: placa base y cartelas | Pendiente |

### Paso 1

Es una **aplicación externa**: un `.exe` que se conecta al Tekla Structures que está abierto en el mismo equipo.

**Pestañas**
- **Piezas**: cada pieza con nombre, perfil, material, clase, prefijos, marcas, fase, longitud, peso y área.
- **Resumen por perfil**: metrado agrupado por perfil y material, con cantidad, longitud total, peso total y área de pintura.
- **Revisión**: errores que conviene corregir antes de numerar:
  - piezas sin prefijo de pieza o de conjunto, o sin nombre;
  - piezas con peso cero, que suelen indicar un perfil que no existe en el catálogo;
  - un mismo prefijo usado en perfiles de tipos distintos, por ejemplo vigas y ángulos;
  - materiales que no están en el catálogo (después de pulsar *Leer catálogo*).
- **Catálogo**: materiales, pernos y cantidad de perfiles del entorno de Tekla abierto.

**Botones**
- **Leer todo el modelo** o **Leer selección**.
- **Seleccionar en Tekla**: selecciona en el modelo las piezas de las filas marcadas. También funciona con doble clic.
  En el resumen selecciona todas las piezas de ese perfil.
- **Exportar a Excel**: guarda la pestaña activa en CSV y lo abre en Excel.
- **Solo acero**: oculta concreto, madera y otros materiales.
- **Diagnóstico**: si no conecta, muestra la versión de Tekla abierta, la versión del programa y la causa más probable.

### Si dice que no hay conexión con Tekla
La Open API solo se conecta si se cumplen dos condiciones:
- **Misma versión**: el programa de la carpeta `Tekla2025` solo funciona con Tekla 2025, y así con cada versión.
- **Mismos permisos**: Tekla y el programa deben abrirse los dos como administrador, o ninguno.

Pulse **Diagnóstico** y el programa le dirá cuál de las dos falla.

### Cómo probarlo
1. En GitHub, abra **Actions → Compilar programas → la última ejecución**.
2. Descargue el artefacto de su versión, por ejemplo **TeklaPiezas-Tekla2025.0.0**, y descomprímalo en una carpeta, por ejemplo `C:\TeklaPiezas`.
3. Abra Tekla Structures con un modelo.
4. Ejecute `TeklaPiezas.exe` y pulse **Leer todo el modelo**.

Si Windows muestra *Windows protegió su PC*, pulse **Más información → Ejecutar de todas formas**: el programa no está firmado.

### Compilar usted mismo
Necesita el [SDK de .NET 8](https://dotnet.microsoft.com/download). No hace falta Tekla para compilar: la Open API se descarga de NuGet.

```powershell
.\build.ps1                            # Tekla 2025
.\build.ps1 -TeklaVersion 2024.0.4     # otra versión
```

También puede abrir `Automatizacion.sln` en Visual Studio 2022.

## Estructura del código

```
src/TeklaPiezas.Core/   Lógica sin Tekla: datos de la pieza, resumen, revisión y CSV (se prueba sin Tekla)
src/TeklaPiezas/        Programa de Windows
  TeklaLector.cs        Todo lo que se hace con la Open API: conectar, leer piezas, catálogo y seleccionar
  MainForm.cs           Ventana con las pestañas
tests/                  Pruebas automáticas de la lógica
```

**Para aprender**, empiece por `TeklaLector.cs`. Ahí está, comentado, cómo se conecta a Tekla,
cómo se recorren los objetos del modelo y cómo se leen sus propiedades.

## Notas
- Las propiedades de informe de Tekla vienen en milímetros, kilogramos y mm². El programa muestra el área en m².
- El CSV usa el separador de lista y el decimal de la configuración regional de Windows, para que Excel lo abra bien.
- Probado solo con pruebas automáticas de la lógica. La conexión con Tekla se prueba en su equipo.
