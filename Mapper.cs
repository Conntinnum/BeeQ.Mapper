using System.Collections;
using System.Reflection;

namespace BeeQ;

public static class Mapper
{
    private const string ProxyEFNamespaceSegment = ".Proxies.";

    private static Dictionary<(Type ori, Type des), MethodInfo> MapperMethods { get; } = [];

    /// <summary>
    /// Configura los métodos de mapeo existentes en un assembly
    /// </summary>
    public static void Configurate(Assembly? assembly)
    {
        if (assembly == null) return;

        var classes = assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IMapperConfig).IsAssignableFrom(t))
            .ToList();

        foreach (var cls in classes)
        {
            var methods = cls.GetMethods()
                .Where(m => m.IsStatic && m.IsPublic)
                .ToList();

            foreach (var method in methods)
            {
                var parms = method.GetParameters();
                if (!IsValidSignature(parms))
                    continue;

                var key = (parms[0].ParameterType, parms[1].ParameterType);

                if (MapperMethods.ContainsKey(key))
                    throw new ArgumentException($"Existe más de un método para el mapeo desde [{key.Item1.FullName}] hacia [{key.Item2.FullName}]");

                MapperMethods.Add(key, method);
            }
        }
    }

    private static bool IsValidSignature(ParameterInfo[] parms)
    {
        if (parms.Length < 2) return false;
        if (parms.Length > 3) return false;

        static bool IsCollection(Type t)
            => t.IsArray
            || typeof(System.Collections.IEnumerable).IsAssignableFrom(t)
            || (t.IsGenericType && new[]
            {
        typeof(IList<>),
        typeof(ICollection<>),
        typeof(IDictionary<,>)
            }.Contains(t.GetGenericTypeDefinition()));

        // los dos primeros no deben ser colecciones
        if (IsCollection(parms[0].ParameterType)) return false;
        if (IsCollection(parms[1].ParameterType)) return false;

        // si hay 3 parámetros, el último debe ser object
        if (parms.Length == 3 && parms[2].ParameterType != typeof(object))
            return false;

        return true;
    }

    public static object? CopyTo(object? source, object? destiny, dynamic? data = null)
    {
        ArgumentNullException.ThrowIfNull(destiny);
        if (source == null) return null;

        var (tOri, tDes) = NormalizeTypes(source.GetType(), destiny.GetType());

        var pOri = GetReadableProperties(tOri);
        var pDes = GetWritableProperties(tDes);

        foreach (var po in pOri)
        {
            if (HasNoMap(po)) continue;

            var pd = FindMatchingProperty(po, pDes);
            if (pd == null || HasNoMap(pd)) continue;

            CopyProperty(po, pd, source, destiny, data);
        }

        Mapper.ExtraMap(source, destiny, data);
        return destiny;
    }

    #region Auxiliares de CopyTo

    private static (Type Ori, Type Des) NormalizeTypes(Type tOri, Type tDes)
    {
        if (tOri.FullName!.Contains(ProxyEFNamespaceSegment))
            tOri = tOri.BaseType!;
        if (tDes.FullName!.Contains(ProxyEFNamespaceSegment))
            tDes = tDes.BaseType!;
        return (tOri, tDes);
    }

    private static bool HasNoMap(PropertyInfo p) => p.GetCustomAttribute<NoMapAttribute>() != null;

    private static bool IsMapperObj(Type t) => typeof(IMapperObj).IsAssignableFrom(t);

    private static bool IsCollection(Type t) => t != typeof(string) && typeof(IEnumerable).IsAssignableFrom(t);

    private static IEnumerable<PropertyInfo> GetReadableProperties(Type t) => t.GetProperties().Where(p => p.CanRead);

    private static IEnumerable<PropertyInfo> GetWritableProperties(Type t)
    {
        var props = t.GetProperties().Where(p => p.CanWrite).ToList();

        var declared = ((TypeInfo)t).DeclaredProperties
            .Where(p => p.CanWrite && !props.Any(x => x.Name == p.Name));

        return props.Concat(declared);
    }

    private static PropertyInfo? FindMatchingProperty(PropertyInfo po, IEnumerable<PropertyInfo> pDes)
    {
        return pDes
            .OrderBy(p => p.PropertyType == po.PropertyType ? 0 : 1)
            .FirstOrDefault(p => p.Name.Equals(po.Name, StringComparison.CurrentCultureIgnoreCase));
    }

    private static void CopyProperty(PropertyInfo po, PropertyInfo pd, object source, object destiny, dynamic? data)
    {
        var srcValue = po.GetValue(source);

        if (pd.PropertyType.IsAssignableFrom(po.PropertyType))
            pd.SetValue(destiny, srcValue);
        else if (IsMapperObj(po.PropertyType))
            CopySubClass(po, pd, source, destiny, data);
        else if (IsCollection(po.PropertyType))
            CopyCollection(po, pd, source, destiny, data);
    }

    private static void CopySubClass(PropertyInfo po, PropertyInfo pd, object source, object destiny, dynamic? data)
    {
        var sub = (IMapperObj?)po.GetValue(source);
        if (sub == null)
        {
            pd.SetValue(destiny, null);
            return;
        }

        var destValue = pd.GetValue(destiny)
            ?? pd.PropertyType.GetConstructor(Type.EmptyTypes)?.Invoke([]);

        var mapped = Mapper.CopyTo(sub, destValue, data);
        pd.SetValue(destiny, mapped);
    }

    private static void CopyCollection(PropertyInfo po, PropertyInfo pd, object source, object destiny, dynamic? data)
    {
        var values = (IEnumerable?)po.GetValue(source);
        if (values == null)
        {
            pd.SetValue(destiny, null);
            return;
        }

        var itemType = GetCollectionItemType(po, pd);
        if (itemType == null)
        {
            pd.SetValue(destiny, null);
            return;
        }

        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType))!;

        foreach (var item in values)
        {
            var sub = (IMapperObj)item;
            var newItem = itemType.GetConstructor(Type.EmptyTypes)!.Invoke(null);
            newItem = Mapper.CopyTo(sub, newItem, data);
            list.Add(newItem);
        }

        if (pd.PropertyType.IsArray)
        {
            var array = Array.CreateInstance(itemType, list.Count);
            list.CopyTo(array, 0);
            pd.SetValue(destiny, array);
        }
        else
        {
            pd.SetValue(destiny, list);
        }
    }

    private static Type? GetCollectionItemType(PropertyInfo po, PropertyInfo pd)
    {
        if (pd.PropertyType.IsArray)
            return pd.PropertyType.GetElementType();

        var gen = po.PropertyType.GenericTypeArguments;
        if (gen.Length == 1 && typeof(IMapperObj).IsAssignableFrom(gen[0]))
            return pd.PropertyType.GenericTypeArguments.FirstOrDefault();

        return null;
    }

    #endregion

    /// <summary>
    /// Crea una nueva instancia de TDES y copia las propiedades de ori a la nueva instancia
    /// </summary>
    public static TDES Mapping<TDES>(object ori, dynamic? data = null)
        where TDES : class, new()
    {
        return Mapper.CopyTo(ori, new TDES(), data);
    }

    /// <summary>
    /// Ejecuta el método de Mapeo si es que existe
    /// </summary>
    public static void ExtraMap(object? ori, object? des, dynamic? data = null)
    {
        Mapper.ExtraMapAsync(ori, des, data).Wait();
    }

    /// <summary>
    /// Ejecuta el método de Mapeo si es que existe, de forma asincrónica
    /// </summary>
    public static async Task ExtraMapAsync(object? ori, object? des, dynamic? data = null)
    {
        if (ori == null || des == null) return;

        Type tOri = ori.GetType();
        Type tDes = des.GetType();
        // fix para EF
        if (tOri.FullName!.Contains(ProxyEFNamespaceSegment)) tOri = tOri.BaseType!;
        if (tDes.FullName!.Contains(ProxyEFNamespaceSegment)) tDes = tDes.BaseType!;

        var key = (tOri, tDes);
        if (MapperMethods.TryGetValue(key, out MethodInfo? method))
        {
            if (method == null) return;

            object?[] parms = [ori, des];
            if (method.GetParameters().Length == 3) parms = [ori, des, data];

            if (typeof(Task).IsAssignableFrom(method.ReturnType))
                await (Task)method.Invoke(null, parms)!;
            else
                method.Invoke(null, parms);
        }
    }
}
