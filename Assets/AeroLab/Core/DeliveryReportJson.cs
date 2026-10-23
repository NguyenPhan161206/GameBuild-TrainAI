using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;
namespace AeroLab.Core
{
    // Field-only DTO serializer shared by Unity and standalone tools; no Unity dependency.
    public static class DeliveryReportJson
    {
        public static string Serialize(object report) {var output=new StringBuilder();Write(output,report);return output.ToString();}
        private static void Write(StringBuilder output,object value)
        {
            if(value==null){output.Append("null");return;}
            if(value is string || value is Enum){Quote(output,value.ToString());return;}
            if(value is bool){output.Append((bool)value?"true":"false");return;}
            if(value is float || value is double)
            {double number=Convert.ToDouble(value,CultureInfo.InvariantCulture);if(double.IsNaN(number)||double.IsInfinity(number))throw new ArgumentException("Nonfinite report value");output.Append(number.ToString("R",CultureInfo.InvariantCulture));return;}
            if(value is int || value is long){output.Append(Convert.ToString(value,CultureInfo.InvariantCulture));return;}
            var sequence=value as IEnumerable;
            if(sequence!=null){output.Append('[');bool comma=false;foreach(var item in sequence){if(comma)output.Append(',');Write(output,item);comma=true;}output.Append(']');return;}
            output.Append('{');bool first=true;
            foreach(var field in value.GetType().GetFields(BindingFlags.Public|BindingFlags.Instance))
            {if(!first)output.Append(',');Quote(output,field.Name);output.Append(':');Write(output,field.GetValue(value));first=false;}
            output.Append('}');
        }
        private static void Quote(StringBuilder output,string value)
        {output.Append('"');foreach(char c in value){switch(c){case '"':output.Append("\\\"");break;case '\\':output.Append("\\\\");break;case '\n':output.Append("\\n");break;case '\r':output.Append("\\r");break;case '\t':output.Append("\\t");break;default:if(c<32)output.Append("\\u"+((int)c).ToString("x4"));else output.Append(c);break;}}output.Append('"');}
    }
}
