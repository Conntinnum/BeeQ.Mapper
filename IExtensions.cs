namespace BeeQ.Mapper;

public static class IMapperExtensions
{
    public static IEnumerable<TDES> SelectMapping<TDES>(this IEnumerable<IMapperObj> collection)
        where TDES : class, new() => collection.Select(ori => ori.Mapping<TDES>()).AsEnumerable<TDES>();

    public static IEnumerable<TDES> SelectMapping<TDES>(this IEnumerable<IMapperObj> collection, dynamic? data)
        where TDES : class, new() => collection.Select(ori => ori.Mapping<TDES>((object?)data)).AsEnumerable<TDES>();

    public static IEnumerable<TDES> SelectMapping<TDES>(this IEnumerable<IMapperObj> collection, Action<TDES> extraMap, dynamic? data = null)
        where TDES : class, new() => collection.Select(ori => ori.Mapping<TDES>(extraMap, (object?)data)).AsEnumerable<TDES>();

    public static TDES Mapping<TDES>(this IMapperObj ori)
        where TDES : class, new() => Mapper.Mapping<TDES>(ori, null);

    public static TDES Mapping<TDES>(this IMapperObj ori, dynamic? data)
        where TDES : class, new() => Mapper.Mapping<TDES>(ori, data);

    public static TDES Mapping<TDES>(this IMapperObj ori, Action<TDES> extraMap, dynamic? data = null)
        where TDES : class, new()
    {
        var des = Mapper.Mapping<TDES>(ori, data);
        extraMap(des);
        return des;
    }
}
