using System.Text.Json;
using System.Text.Json.Serialization;
using TaskFlow.Api.Models;

namespace TaskFlow.Api.Data;

/// <summary>
/// Acepta el enum de estado tanto por su nombre ("Completada") como por su
/// forma en camel case ("completada"), que es como lo envia el cliente.
/// Sin esta conversion, un estado valido llega como 400.
/// </summary>
public class EstadoTareaJsonConverter : JsonConverter<EstadoTarea>
{
    /// <summary>Escribe el estado con su nombre.</summary>
    /// <param name="writer">Escritor JSON.</param>
    /// <param name="value">Estado a escribir.</param>
    /// <param name="options">Opciones de serializacion.</param>
    public override void Write(Utf8JsonWriter writer, EstadoTarea value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }

    /// <summary>Lee el estado probando las variantes con y sin tilde.</summary>
    /// <param name="reader">Lector JSON.</param>
    /// <param name="typeToConvert">Tipo a convertir.</param>
    /// <param name="options">Opciones de deserializacion.</param>
    /// <returns>El estado leido.</returns>
    /// <exception cref="JsonException">Si el texto no corresponde a un estado.</exception>
    public override EstadoTarea Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var texto = reader.GetString();
        if (string.IsNullOrWhiteSpace(texto))
        {
            throw new JsonException("El estado no puede ser vacio.");
        }

        var limpio = texto.Replace(" ", string.Empty, StringComparison.Ordinal);
        if (Enum.TryParse<EstadoTarea>(limpio, ignoreCase: true, out var estado))
        {
            return estado;
        }

        throw new JsonException($"'{texto}' no es un estado valido. Valores: {string.Join(", ", Enum.GetNames<EstadoTarea>())}");
    }
}