using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Utils.Mathematics;

namespace Utils.Objects;

public static class TypeEx
{
    public static bool IsAssignableFromEx(this Type toBeAssigned, Type toAssign)
    {
        if (toBeAssigned.In(Types.Number) && toAssign.In(Types.Number))
        {
            if (toBeAssigned.In(Types.FloatingPointNumber)) return true;
            return Marshal.SizeOf(toBeAssigned) >= Marshal.SizeOf(toAssign);
        }
        return toBeAssigned.IsAssignableFrom(toAssign);
    }
}
