namespace BeeQ.Mapper;

/// <summary>
/// Indica que el atributo no se mapea con el AutoMapper
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class NoMapAttribute : Attribute 
{ 
}
