using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.ComponentModel;

namespace XGDTool.Lib.Util;

public static class EnumExt
{
    public static string GetDescription(Enum value)
    {
        FieldInfo? field = value.GetType().GetField(value.ToString());
        if (field == null)
            return value.ToString();

        DescriptionAttribute? attr =
            field.GetCustomAttribute<DescriptionAttribute>();

        return attr?.Description ?? value.ToString();
    }

    public static T FromDescription<T>(string description) where T : Enum
    {
        foreach (var field in typeof(T).GetFields())
        {
            if (Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) is DescriptionAttribute attribute)
            {
                if (attribute.Description == description)
                    return (T)field.GetValue(null)!;
            }
            else
            {
                if (field.Name == description)
                    return (T)field.GetValue(null)!;
            }
        }

        throw new ArgumentException($"No enum value with description '{description}' found in {typeof(T)}.");
    }
}
