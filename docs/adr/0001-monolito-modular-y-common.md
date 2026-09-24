# ADR-0001 - Monolito modular sobre la libreria Common

- Estado: Aceptado
- Fecha: 2026-09-24
- Deciden: mantenedores de back-template

## Contexto y problema

La plantilla es el punto de partida de SaaS nuevos. Necesita limites de modulo que no dependan de la disciplina
de nadie, y un patron de caso de uso comun con el resto de los productos que usan la libreria compartida Common.

## Opciones consideradas

1. Microservicios desde el dia uno: aislamiento fuerte, pero costo operativo que un producto nuevo no paga.
2. Proyecto unico con carpetas: barato, pero los limites se rompen sin que el compilador diga nada.
3. Monolito modular: un proceso, una base, modulos de 5 proyectos con dependencias forzadas por el compilador.

## Decision

Opcion 3. Cada modulo es Contracts / Domain / Application / Infrastructure / Presentation; la comunicacion entre
modulos solo por Contracts. Se usa el patron de Common (Request / Handler / Responses / Presenter / Controller +
ResultViewModel) con su mediador propio; no MediatR. Common entra como submodulo git fijado a un commit.

## Consecuencias

- Positivas: extraer un modulo a servicio es mover proyectos, no reescribir; mismo patron que el resto del ecosistema.
- Negativas: mas proyectos y registro manual de handlers y presenters (a proposito: sin escaneo, nada aparece "solo").
- Actualizar Common es un cambio explicito de commit del submodulo.

## Cumplimiento

`Architecture.Tests/LayerBoundaryTests` (capas y modulos por ensamblado). `CompositionTests` verifica que todo
handler tenga su presenter registrado.
