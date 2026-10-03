// Las pruebas comparten una base de datos PostgreSQL: si dos clases se
// ejecutan a la vez, una limpia las filas que la otra acaba de insertar.
// Desactivar el paralelismo de colecciones hace la ejecucion determinista.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
