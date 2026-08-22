using System.Runtime.CompilerServices;

// Exposes internal runtime members to the runtime test assembly. The parity tests
// in MarchingCubesExtractorParityTests need MarchingCubesExtractor.ExtractLegacy
// (the Slice 1 reference oracle) until direct edge ownership lands in Slice 2.
[assembly: InternalsVisibleTo("ProceduralCreature.Tests.Runtime")]
