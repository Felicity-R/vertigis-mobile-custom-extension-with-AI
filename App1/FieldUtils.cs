using Esri.ArcGISRuntime.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace App1
{
    internal static class FieldUtils
    {
        public static string GetFieldsAsStringifiedJSON(IReadOnlyList<Field> fields)
        {
            var json = "{\n";

            foreach (var field in fields)
            {
                var type = GetFieldType(field);

                if (type != "")
                {
                    json += $"\t\"{field.Name}\": {type}\n";
                }
            }

            json = json + "}";

            return json;
        }

        /// <summary>
        /// Get the json type of the field. Unaccepted field types return empty string.
        /// </summary>
        /// <param name="field"></param>
        /// <returns></returns>
        private static string GetFieldType(Field field)
        {
            switch (field.FieldType)
            {
                case FieldType.Text:
                    return "string";
                case FieldType.Int64:
                case FieldType.Int32:
                case FieldType.Int16:
                case FieldType.Float64:
                case FieldType.Float32:
                    return "number";
                default:
                    return "";
            }
        }
    }
}
