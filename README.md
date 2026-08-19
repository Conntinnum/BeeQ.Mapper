# BeeQ.Mapper
Librería de automapeo entre clases de C#
Facil de usar y facil de entender.

## Utilidad de la Herramienta
Es de común uso tener que mover la información de una clase a otra, ya se por ejemplo para abstraer el objeto de la base de datos, al objeto de negocio e incluso el objeto que se devuelve vía interfaz.
Esto implica un mapeo de propiedades que se vuelve engorroso y repetitivo, para esto se creó esta librería que permite mapear de manera sencilla y rápida.
Vamos a suponer las siguientes clases de ejemplo:
``` csharp
public class ClienteDbModel : IMapperObj
{
    public virtual long Id { get; set; }
    public virtual string? Name { get; set; }
    public virtual DateTime DeletedAt { get; set; }
}

public class ClienteDto : IMapperObj
{
    public virtual long Id { get; set; }
    public virtual string? Name { get; set; }
    public virtual bool Active { get; set; }
}
```

Para mapear entre una clase y otra, simplemente se debe invocar el método `Mapping` de la clase que implementa la interfaz `IMapperObj`, pasando como parámetro el tipo de clase al que se desea mapear. Por ejemplo, para mapear un objeto de tipo `ClienteDbModel` a un objeto de tipo `ClienteDto`, se puede hacer lo siguiente:
``` csharp
public void ConvertToDto(ClienteDbModel cliente)
{
    return cliente.Mapping<ClienteDto>();
}
```

Ahora bien, en este caso nos quedó la propiedad `Active` sin mapear ya que el mapeo automático se realiza comparando los nombres de las propiedades.
para esta tarea de mapear campos especiales tenemos 2 opciones:

### Método explícito
Podemos agregar la funcionalidad directamente en la llamada al Mapping para customizarla.
``` csharp
public ClienteDto ConvertToDTO(ClienteDbModel cliente)
{
    return this.Mapping<ClienteDto>(dto =>
    {
        dto.Active = !cliente.DeletedAt.HasValue;
    });
}
```
Notar que el mapeo que el mapeo automático no es necesario de realizar.

### Método por Configuración
La librería permite crear una clase donde se creen todos los mappings especiales, de forma de tener cerntralizada la configuración de los mapeos.
Basta con utilizar la interfaz `IMapperConfig` en la clase que contendrá los métodos estáticos de mapeo custom.

``` csharp
public class Mapper : IMapperConfig
{
    public static void Map(ClienteDbModel ori, ClienteDto des)
    {
        dto.Active = !cliente.DeletedAt.HasValue;
    }
    
    public static void Map(ProveedorModel ori, ProveedorDto des)
    {
        ...
    }

    ...
}
```


## Datos Extra
Es conocido que muchas veces el mapeo necesita de datos externos para realizar un mapeo especial, en nuestro caso de método explícito, la propiedad `Active` de `ClienteDto` depende de la propiedad `DeletedAt` de la clase `ClienteDbModel`, pero no siempre el dato se obtiene del objeto origen.
En el siguiente caso vemos como necesitamos una variable extra `disabled` para obtener el valor de la propiedad `Active`. 
``` csharp
public ClienteDto ConvertToDTO(ClienteDbModel cliente, bool disabled)
{
    return this.Mapping<ClienteDto>(dto =>
    {
        dto.Active = disabled || !cliente.DeletedAt.HasValue;
    });
}
```

Y si bien se puede resolver como se muestra en el ejemplo anterior, el Método por Configuración requiere un parámetro extra al cual podemos enviar todos los datos necesarios.

``` csharp
public class Mapper : IMapperConfig
{
    public static void Map(ClienteDbModel ori, ClienteDto des, dynamic data)
    {
        dto.Active = data.Disabled || !cliente.DeletedAt.HasValue;
    }
}

public ClienteDto ConvertToDTO(ClienteDbModel cliente, bool disabled)
{
    return cliente.Mapping<ClienteDto>(new { Disabled = disabled });
}
```
Esto enviará `new { Disabled = disabled }` como `data` en el mapper y de esta forma se puede utilizar información externa al objeto de origen del mapeo.

### NoMap
existe el atributo `[NoMap]` que se puede aplicar a un campo de la clase de destino para evitar que se mapee. Es valido y será tomado en cuenta para ambas clases tanto de origen del mapeo como la clase de destino, con que alguna tenga el `NoMap`, no se mapeará automáticamente la columna.
``` csharp
public class ClienteDto : IMapperObj
{
    public virtual long Id { get; set; }
    public virtual string? Name { get; set; }
    [NoMap]
    public virtual bool Active { get; set; }
}
```

## Uso Genérico
Puedes utilizar la librería para mapear cualquier tipo de objeto

``` csharp
public ClienteDto ConvertToDTO(ClienteDbModel cliente, bool disabled)
{
    return BeeQ.Mapper.Mapping<ClienteDto>(cliente, new { Disabled = disabled });
}
```
o bien, en caso de no estar fuertemente tipado
``` csharp
public object? ConvertToDTO(object? ori, object? des, bool disabled)
{
    BeeQ.Mapper.CopyTo(cliente, des, new { Disabled = disabled });
    return des;
}
```


Si sólo desea ejecutar el ExtraMap configurado vcía `IMapperConfig`:

``` csharp
public object? ConvertToDTO(object? ori, object? des, bool disabled)
{
    BeeQ.Mapper.ExtraMap(cliente, des, new { Disabled = disabled });
    return des;
}

//o bien async
public async Task<object?> ConvertToDTO(object? ori, object? des, bool disabled)
{
    await BeeQ.Mapper.ExtraMapAsync(cliente, des, new { Disabled = disabled });
    return des;
}
```


## Modo de Uso
En el archivo Program.cs de su proyecto, se debe registrar el mapeador de la siguiente manera:
donde `typeof(MapperConfig).Assembly` es el assembly del proyecto donde se encuentran los mapeos de clases custom `IMapperConfig`.
``` csharp
...
var app = builder.Build();
...
BeeQ.Mapper.Configurate(typeof(MapperConfig).Assembly);
...
app.Run()
```
