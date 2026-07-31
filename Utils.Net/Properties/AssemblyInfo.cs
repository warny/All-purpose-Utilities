using System.Runtime.CompilerServices;

// Expose internal types (transport abstraction, network-interface snapshot selection) to the
// test assemblies so DNS/NTP transport logic can be unit-tested without touching real sockets.
[assembly: InternalsVisibleTo("UtilsTest.Unit")]
[assembly: InternalsVisibleTo("UtilsTest.Functional")]
