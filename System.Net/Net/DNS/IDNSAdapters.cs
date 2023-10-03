using System;
using System.Collections.Generic;
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
