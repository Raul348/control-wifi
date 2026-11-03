# Automatización de Tekla Structures con C#

Programas para aprender a automatizar **Tekla Structures 2025** con la **Open API** en C#.
Se avanza por pasos, de lo simple a lo complejo. El código está comentado en español.

| Paso | Programa | Estado |
|---|---|---|
| 1 | **Tekla Piezas**: lee el modelo, lista las piezas, hace el metrado y revisa errores antes de numerar | Listo para probar |
| 2 | Crear piezas: columnas y vigas desde una ventana | Pendiente |
| 3 | Sustitución de perfiles por los disponibles en Perú | Pendiente |
| 4 | Primer plugin: escalera marinera | Pendiente |
| 5 | Uniones: placa base y cartelas | Pendiente |

## Paso 1: Tekla Piezas

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

### Cómo probarlo
1. En GitHub, abra **Actions → Compilar programa → la última ejecución**.
2. Descargue el artefacto **TeklaPiezas-Tekla2025** y descomprímalo en una carpeta, por ejemplo `C:\TeklaPiezas`.
3. Abra Tekla Structures 2025 con un modelo.
4. Ejecute `TeklaPiezas.exe` y pulse **Leer todo el modelo**.

Si Windows muestra *Windows protegió su PC*, pulse **Más información → Ejecutar de todas formas**: el programa no está firmado.

### Compilar usted mismo
Necesita el [SDK de .NET 8](https://dotnet.microsoft.com/download). No hace falta Tekla para compilar: la Open API se descarga de NuGet.

```powershell
.\build.ps1                            # Tekla 2025
.\build.ps1 -TeklaVersion 2024.0.4     # otra versión
```

También puede abrir `TeklaAutomatizacion.sln` en Visual Studio 2022.

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
