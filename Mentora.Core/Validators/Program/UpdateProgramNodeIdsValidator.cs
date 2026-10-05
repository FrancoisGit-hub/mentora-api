using FluentValidation;
using Mentora.Core.DTOs.Program.Body;

namespace Mentora.Core.Validators.Program;

/// <summary>Node ids of the program being updated, loaded by ProgramService before validation.</summary>
public record ExistingProgramNodeIds(
    IReadOnlySet<Guid> BlockIds,
    IReadOnlySet<Guid> SessionIds,
    IReadOnlySet<Guid> CircuitIds,
    IReadOnlySet<Guid> ExerciseIds);

/// <summary>
/// Validates the optional node ids of the PUT training-program tree. The tree rules themselves
/// (levels, positions, types, ...) stay in ProgramTemplateBodyValidator.
/// </summary>
/// <remarks>
/// Requires <c>RootContextData["ExistingNodeIds"]</c> (<see cref="ExistingProgramNodeIds"/>), set by
/// the caller. An id unknown, belonging to another program, or of another node kind gets the same
/// message — nothing tells whether it exists elsewhere. Failure keys use the request path
/// ("Body.Blocks[1].Sessions[2].Id", positions as indexes, like the tree validator).
/// </remarks>
public class UpdateProgramNodeIdsValidator : AbstractValidator<UpdateProgramBody>
{
    public UpdateProgramNodeIdsValidator()
    {
        RuleFor(x => x).Custom(Validate);
    }

    private static void Validate(UpdateProgramBody body, ValidationContext<UpdateProgramBody> context)
    {
        if (!context.RootContextData.TryGetValue("ExistingNodeIds", out var value)
            || value is not ExistingProgramNodeIds existing)
            throw new InvalidOperationException(
                "UpdateProgramNodeIdsValidator requires ValidationContext.RootContextData[\"ExistingNodeIds\"] " +
                "to be set before validation runs.");

        var nodes = new List<(Guid Id, string Kind, IReadOnlySet<Guid> Known, string Path)>();
        CollectBlocks(body?.Blocks, "Body.Blocks", existing, nodes);

        var seen = new HashSet<Guid>();
        foreach (var node in nodes)
        {
            if (!seen.Add(node.Id))
                context.AddFailure(node.Path, $"Id {node.Id} appears more than once.");
            else if (!node.Known.Contains(node.Id))
                context.AddFailure(node.Path, $"Id {node.Id} is not a {node.Kind} of this program.");
        }
    }

    private static void CollectBlocks(
        List<UpdateProgramBlock>? blocks, string path, ExistingProgramNodeIds existing,
        List<(Guid, string, IReadOnlySet<Guid>, string)> nodes)
    {
        if (blocks is null)
            return;

        foreach (var block in blocks)
        {
            var blockPath = $"{path}[{block.Position}]";
            if (block.Id is { } blockId)
                nodes.Add((blockId, "block", existing.BlockIds, $"{blockPath}.Id"));

            CollectBlocks(block.Blocks, $"{blockPath}.Blocks", existing, nodes);

            foreach (var session in block.Sessions ?? [])
            {
                var sessionPath = $"{blockPath}.Sessions[{session.Position}]";
                if (session.Id is { } sessionId)
                    nodes.Add((sessionId, "session", existing.SessionIds, $"{sessionPath}.Id"));

                foreach (var circuit in session.Circuits ?? [])
                {
                    var circuitPath = $"{sessionPath}.Circuits[{circuit.Position}]";
                    if (circuit.Id is { } circuitId)
                        nodes.Add((circuitId, "circuit", existing.CircuitIds, $"{circuitPath}.Id"));

                    foreach (var exercise in circuit.Exercises ?? [])
                    {
                        if (exercise.Id is { } exerciseId)
                            nodes.Add((exerciseId, "exercise", existing.ExerciseIds,
                                $"{circuitPath}.Exercises[{exercise.Position}].Id"));
                    }
                }
            }
        }
    }
}
