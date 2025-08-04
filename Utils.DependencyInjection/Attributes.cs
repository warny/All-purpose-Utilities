﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace QueryOData.Injection;

[AttributeUsage(AttributeTargets.Interface)]
public class InjectableAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class)]
public abstract class InjectableClassAttribute : Attribute
{
	public string? Domain { get; }

	public InjectableClassAttribute(string? domain)
	{
		this.Domain = domain;
	}
}

[AttributeUsage(AttributeTargets.Class)]
public class SingletonAttribute : InjectableClassAttribute
{
	public SingletonAttribute() : base(null) { }
	public SingletonAttribute(string domain) : base(domain) { }
}

[AttributeUsage(AttributeTargets.Class)]
public class ScopedAttribute : InjectableClassAttribute
{
	public ScopedAttribute() : base(null) { }
	public ScopedAttribute(string domain) : base(domain) { }
}

[AttributeUsage(AttributeTargets.Class)]
public class TransientAttribute : InjectableClassAttribute
{
	public TransientAttribute() : base(null) { }
	public TransientAttribute(string domain) : base(domain) { }
}
