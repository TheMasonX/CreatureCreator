using System.Runtime.CompilerServices;

// Exposes internal runtime members to the runtime test assembly.
[assembly: InternalsVisibleTo("ProceduralCreature.Tests.Runtime")]

// TSK-0125: the editor preview ownership tests fabricate the (now immutable)
// GeneratedCreature output model through its internal construction path
// (AddGeometry) so they can drive the editor preview controller in isolation
// without coupling to the full generator. Granting the editor test assembly
// internal visibility mirrors the sibling Editor -> Tests.Editor grant.
[assembly: InternalsVisibleTo("ProceduralCreature.Tests.Editor")]
