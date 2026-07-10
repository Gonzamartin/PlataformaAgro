# Plataforma AgTech - Control Operativo de Pulverización 🚜🌾

Una aplicación nativa de escritorio para Windows desarrollada en **C# (.NET)**, diseñada específicamente para productores, asesores agronómicos y contratistas rurales. Permite auditar en tiempo real las condiciones meteorológicas antes de una aplicación fitosanitaria para mitigar riesgos ambientales y optimizar la eficiencia del caldo de pulverización.

---

## 🚀 Características Clave

* **Tablero Operativo en Tiempo Real ⏱️:** Muestra indicadores clave como la temperatura del aire y la velocidad del viento mediante lógica microclimática dinámica.
* **Base de Datos de Lotes Frecuentes 🗺️:** Selector desplegable preconfigurado con las coordenadas geográficas de los lotes habituales para agilizar el trabajo diario.
* **Dictamen Agronómico Automatizado 🟢🔴:** Evalúa las variables climáticas de forma instantánea emitiendo una alerta cromática:
  * **APTO (Verde):** Condiciones seguras de trabajo (Viento entre 5 y 15 km/h, temperatura menor a 30°C).
  * **NO APTO (Rojo):** Advierte riesgos críticos como **Deriva** (vientos altos), **Inversión Térmica** (vientos nulos) o **Evaporación** (calor crítico).
* **Exportación Comercial y Trazabilidad (CSV) 💾:** Sistema de almacenamiento incremental local. Con un solo clic, guarda una orden de trabajo con sello de fecha y hora directamente en un archivo compatible con **Microsoft Excel**.

---

## 🛠️ Tecnologías Utilizadas

* **Lenguaje:** C# 10+ (Instrucciones de nivel superior / Top-Level Statements).
* **Framework:** .NET 10.0 Windows Forms (Arquitectura desacoplada sin Designer oculto).
* **Persistencia de Datos:** Escritura secuencial con optimización de codificación estructurada mediante `System.IO`.

---

## 💻 Instrucciones para Ejecución en Desarrollo

Si deseas compilar o realizar modificaciones en el entorno de desarrollo, asegúrate de contar con el SDK de .NET instalado y ejecuta los siguientes comandos en tu terminal de PowerShell dentro de la carpeta del proyecto:

```powershell
# Limpiar el historial de binarios residuales
dotnet clean

# Compilar la estructura del proyecto
dotnet build

# Lanzar la interfaz visual en Windows
dotnet run
```

---

## 📦 Generación de la Aplicación Independiente (.EXE)

Para empaquetar la plataforma como un software comercial autoportante que funcione en cualquier computadora sin necesidad de instalar herramientas de programación, ejecuta el comando de publicación:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true
```
*El ejecutable final optimizado se generará en la ruta de distribución `\bin\Release\net10.0-windows\win-x64\publish\` listo para ser transportado por pendrive o compartido vía web.*

---

## 🗂️ Registro de Auditorías (Trazabilidad Colectada)
El archivo generado `historial_pulverizaciones.csv` estructura de manera automatizada las siguientes métricas de fiscalización por fila:
`Fecha/Hora | Lote Seleccionado | Latitud | Longitud | Temperatura (°C) | Velocidad Viento (km/h) | Condición Operativa`

---
Developed by **Gonzamartin** - *AgTech Solutions*.
