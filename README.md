# Descripción del Código

Este código implementa un servidor TCP en C# que se encarga del procesamiento de audio mediante servicios de Amazon Web Services (AWS) como Transcribe y Polly.

## Funcionalidades Principales

- **Recepción de Audio**: El servidor espera conexiones TCP y recibe archivos de audio desde un cliente Unity.
- **Procesamiento de Texto**: Envía el texto transcribido a un servidor HTTP externo para su procesamiento adicional.
- **Generación de Audio**: Utiliza Polly de AWS para generar audio a partir del texto procesado y lo envía de vuelta al cliente.

## Uso del Código

El archivo `Program.cs` contiene la lógica principal del servidor TCP, incluyendo métodos para la recepción de audio, transcripción, procesamiento de texto y generación de audio.

## Configuración

Antes de ejecutar la aplicación, asegúrate de configurar las variables de entorno necesarias, como las Claves de AWS y la región.

## Requisitos

Para ejecutar este código, necesitarás:
- Visual Studio o un entorno de desarrollo compatible con C#.
- Cuenta en AWS con acceso a los servicios Transcribe y Polly.
- Conexión a Internet para acceder a los servicios de AWS.

## Contribución

Si deseas contribuir a este proyecto, puedes abrir un pull request con tus cambios propuestos o informar sobre problemas encontrados.