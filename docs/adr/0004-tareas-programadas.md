# ADR-0004 - Tareas programadas con despachador unico

- Estado: Aceptado
- Fecha: 2026-09-24
- Deciden: mantenedores de back-template

## Contexto y problema

Todo producto termina necesitando trabajo recurrente (purgas, recordatorios, conciliaciones). Cada uno montado con
su propio `BackgroundService` se ejecuta dos veces con dos replicas, no deja rastro y no se puede pausar.

## Decision

Un despachador unico (`AutomatedTaskDispatcherService`) y un catalogo en codigo (`IAutomatedTask`). La base guarda
configuracion (pausa, intervalo), el cursor `LastCutoffUtc` (solo avanza con Success o Skipped) y el historial.
El reclamo es atomico con `FOR UPDATE SKIP LOCKED` y expira a las 6 h (proceso muerto). Operarlas por API exige
`tasks.manage` **y** pertenecer al tenant operador (`BackgroundJobs:OperatorTenantId`): las tareas son de toda
la plataforma y el Admin de un cliente no debe pausarlas. Sin operador configurado nadie las opera por API.

La tarea de ejemplo purga (soft delete) tokens vencidos y arranca **en seco**.

## Cumplimiento

`AutomatedTasksTests`: reclamo exclusivo y recuperacion de huerfanos, cursor, gate del operador, purga real e idempotente.
