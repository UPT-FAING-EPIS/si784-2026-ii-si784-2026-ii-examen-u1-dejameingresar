using System.Data.Common;
using TaskFlow.Api.Data;

/// <summary>
/// Normaliza las fechas antes de que Npgsql las envie a PostgreSQL.
/// PostgreSQL guarda las columnas de fecha en <c>timestamptz</c>, que solo
/// acepta <see cref="DateTime"/> con <see cref="DateTimeKind.Utc"/> o
/// <see cref="DateTimeKind.Local"/>. Un <see cref="DateTimeKind.Unspecified"/>
/// hace que Npgsql lance una excepcion al resolver el conversor, y la peticion
/// termina en un 500 sin detalle.
/// </summary>
public static class DateTimeConverter
{
    /// <summary>Convierte una fecha a UTC y le marca la zona.</summary>
    /// <param name="value">Fecha sin zona definite.</param>
    /// <returns>Fecha en UTC con zona marcada.</returns>
    public static DateTime ToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    /// <summary>Convierte una fecha a UTC si es anulable.</</summary>
    /// <param name="value">Fecha sin zona definite, o nula.</param>
    /// <returns>Fecha en UTC con zona marcada, o nula.</returns>
    public static DateTime? ToUtc(DateTime? value)
    {
        return value.HasValue ? ToUtc(value.Value) : null;
    }
}