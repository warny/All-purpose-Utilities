using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Utils.Net.DNS
{
    public interface IDNSReader<T>
    {
        DNSHeader Read(T datas);
    }

    public interface IDNSWriter<T>
    {
        T Write(DNSHeader header);
    }

}
